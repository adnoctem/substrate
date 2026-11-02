using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AdNoctem.Substrate.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AdNoctem.Substrate.Documents;

/// <summary>Extracts schema-declared content controls from a Word package into the semantic content model.</summary>
/// <remarks>The source is copied into memory and opened read-only; it is never saved, and no fields, links or external
/// content are refreshed. Author-correctable problems are returned as diagnostics, as many as can be safely collected.
/// Headers, footers and pictures outside fields are template decoration and are not extracted. Comments are reported and
/// excluded; tracked changes block extraction.</remarks>
public sealed class WordContentReader
{
    public const long DefaultMaximumBytes = 64L * 1024 * 1024;

    private const string WordNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly HashSet<string> RevisionElements = new HashSet<string>(
        StringComparer.Ordinal
    )
    {
        "ins",
        "del",
        "moveFrom",
        "moveTo",
        "moveFromRangeStart",
        "moveToRangeStart",
        "pPrChange",
        "rPrChange",
        "sectPrChange",
        "tblPrChange",
        "tblPrExChange",
        "trPrChange",
        "tcPrChange",
        "tblGridChange",
        "numberingChange",
        "cellIns",
        "cellDel",
        "cellMerge",
        "customXmlInsRangeStart",
        "customXmlDelRangeStart",
        "customXmlMoveFromRangeStart",
        "customXmlMoveToRangeStart",
    };

    public long MaximumBytes { get; }

    /// <summary>When true, Word templates (.dotx) are accepted, for example to inspect an authoring template.</summary>
    public bool AllowTemplates { get; }

    public WordContentReader(long maximumBytes = DefaultMaximumBytes, bool allowTemplates = false)
    {
        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        MaximumBytes = maximumBytes;
        AllowTemplates = allowTemplates;
    }

    /// <remarks>The file is opened for reading with read sharing. IO failures, including a locked file, are exceptions.</remarks>
    public ContentExtraction Read(
        FileSystemPath path,
        ContentSchema schema,
        CancellationToken cancellationToken = default
    )
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        using (
            var stream = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read)
        )
            return Read(stream, schema, cancellationToken);
    }

    /// <remarks>Reads the stream from its current position to the end. The stream is not closed.</remarks>
    public ContentExtraction Read(
        Stream source,
        ContentSchema schema,
        CancellationToken cancellationToken = default
    )
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        var bytes = Copy(source, cancellationToken);
        var diagnostics = new DiagnosticList();
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = new Dictionary<string, FieldContent>(StringComparer.Ordinal);

        WordprocessingDocument document;

        try
        {
            document = WordprocessingDocument.Open(
                new MemoryStream(bytes, false),
                false,
                new OpenSettings { AutoSave = false }
            );
        }
        catch (Exception exception) when (IsPackageFailure(exception, true))
        {
            diagnostics.Error(
                DocumentDiagnosticCode.InvalidPackage,
                null,
                null,
                "The file is not a readable Word document.",
                "Open it in Word, save it as a Word document (.docx), and try again."
            );
            return new ContentExtraction(properties, fields, diagnostics.Items);
        }

        using (document)
        {
            try
            {
                var main = document.MainDocumentPart;

                if (main?.Document?.Body == null)
                {
                    diagnostics.Error(
                        DocumentDiagnosticCode.InvalidPackage,
                        null,
                        null,
                        "The package has no Word document body.",
                        "Save the document again from Word as a Word document (.docx)."
                    );
                    return new ContentExtraction(properties, fields, diagnostics.Items);
                }

                if (!CheckDocumentType(document, main, diagnostics))
                    return new ContentExtraction(properties, fields, diagnostics.Items);

                foreach (
                    var property in document.CustomFilePropertiesPart?.Properties?.ChildElements
                        ?? Enumerable.Empty<OpenXmlElement>()
                )
                    if (
                        property
                            .GetAttributes()
                            .FirstOrDefault(item => item.LocalName == "name")
                            .Value
                            is string name
                        && !properties.ContainsKey(name)
                    )
                        properties.Add(name, property.InnerText);

                ReportRevisions(main, schema, diagnostics);
                ReportComments(main, diagnostics);
                cancellationToken.ThrowIfCancellationRequested();
                new Extraction(main, schema, diagnostics, fields, cancellationToken).Execute();
            }
            catch (Exception exception) when (IsPackageFailure(exception, false))
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.InvalidPackage,
                    null,
                    null,
                    "A part of the Word document is damaged or unreadable.",
                    "Open it in Word, save it as a Word document (.docx), and try again."
                );
                fields.Clear();
            }
        }

        if (diagnostics.HasErrors)
            fields.Clear();

        return new ContentExtraction(properties, fields, diagnostics.Items);
    }

    private byte[] Copy(Stream source, CancellationToken cancellationToken)
    {
        using (var buffer = new MemoryStream())
        {
            var block = new byte[81920];
            int read;

            while ((read = source.Read(block, 0, block.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (buffer.Length + read > MaximumBytes)
                    throw new InvalidDataException(
                        "The document exceeds the configured maximum size."
                    );

                buffer.Write(block, 0, read);
            }

            return buffer.ToArray();
        }
    }

    // Package and XML failures differ between the .NET Framework and System.IO.Packaging implementations, so the
    // packaging exception is matched by name instead of binding this assembly to either implementation. Opening a
    // package additionally reports malformed content-type and relationship data as invalid operations.
    private static bool IsPackageFailure(Exception exception, bool opening) =>
        exception is OpenXmlPackageException
        || exception is InvalidDataException
        || exception is System.Xml.XmlException
        || exception is UriFormatException
        || (opening && exception is InvalidOperationException)
        || exception.GetType().Name == "FileFormatException";

    private bool CheckDocumentType(
        WordprocessingDocument document,
        MainDocumentPart main,
        DiagnosticList diagnostics
    )
    {
        var type = document.DocumentType;

        if (
            type == WordprocessingDocumentType.MacroEnabledDocument
            || type == WordprocessingDocumentType.MacroEnabledTemplate
            || main.VbaProjectPart != null
        )
        {
            diagnostics.Error(
                DocumentDiagnosticCode.MacroEnabledDocument,
                null,
                null,
                "Macro-enabled documents are not accepted.",
                "Create the document from the macro-free template and save it as a Word document (.docx)."
            );
            return false;
        }

        if (type == WordprocessingDocumentType.Template && !AllowTemplates)
        {
            diagnostics.Error(
                DocumentDiagnosticCode.UnsupportedDocumentType,
                null,
                null,
                "The file is a Word template, not a document.",
                "Open the template to create a new document, then save that document as .docx."
            );
            return false;
        }

        return true;
    }

    private static void ReportRevisions(
        MainDocumentPart main,
        ContentSchema schema,
        DiagnosticList diagnostics
    )
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var element in main.Document!.Descendants())
            if (IsRevision(element))
            {
                var field = FieldOf(element, schema) ?? "";
                counts[field] = counts.TryGetValue(field, out var count) ? count + 1 : 1;
            }

        var others = main
            .HeaderParts.Select(part => (OpenXmlPartRootElement?)part.Header)
            .Concat(main.FooterParts.Select(part => (OpenXmlPartRootElement?)part.Footer))
            .Concat(
                new OpenXmlPartRootElement?[]
                {
                    main.StyleDefinitionsPart?.Styles,
                    main.NumberingDefinitionsPart?.Numbering,
                    main.FootnotesPart?.Footnotes,
                    main.EndnotesPart?.Endnotes,
                }
            )
            .Where(root => root != null)
            .Sum(root => root!.Descendants().Count(IsRevision));

        if (others != 0)
            counts[""] = (counts.TryGetValue("", out var count) ? count : 0) + others;

        foreach (var pair in counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            diagnostics.Error(
                DocumentDiagnosticCode.UnresolvedRevision,
                pair.Key.Length == 0 ? null : pair.Key,
                null,
                pair.Value.ToString(CultureInfo.InvariantCulture)
                    + " unresolved tracked change(s) were found.",
                "In Word, accept or reject all changes (Review > Accept/Reject) and save. Turning tracking off is not enough."
            );
    }

    private static void ReportComments(MainDocumentPart main, DiagnosticList diagnostics)
    {
        var count = main.WordprocessingCommentsPart?.Comments?.Elements<Comment>().Count() ?? 0;

        if (count != 0)
            diagnostics.Warning(
                DocumentDiagnosticCode.CommentsPresent,
                null,
                null,
                count.ToString(CultureInfo.InvariantCulture)
                    + " comment(s) were found. Comments are never published; the commented text is.",
                "Resolve or delete the comments if they request changes to the release."
            );
    }

    private static bool IsRevision(OpenXmlElement element) =>
        element.NamespaceUri == WordNamespace && RevisionElements.Contains(element.LocalName);

    private static string? FieldOf(OpenXmlElement element, ContentSchema schema)
    {
        string? outer = null;

        for (var current = element.Parent; current != null; current = current.Parent)
            if (
                current is SdtElement sdt
                && Tag(sdt) is string tag
                && schema.AllFields.ContainsKey(tag)
            )
                outer = schema.Parents.TryGetValue(tag, out var parent) ? parent.Tag : tag;

        return outer;
    }

    internal static string? Tag(SdtElement sdt) =>
        sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

    // One extraction pass over the body. Field state is local to this instance, so sequential reads never share content.
    private sealed class Extraction
    {
        private readonly MainDocumentPart main;
        private readonly ContentSchema schema;
        private readonly DiagnosticList diagnostics;
        private readonly IDictionary<string, FieldContent> fields;
        private readonly CancellationToken cancellationToken;
        private readonly WordStyles styles;
        private readonly WordNumbering numbering;
        private readonly Dictionary<string, int> occurrences = new Dictionary<string, int>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, FieldState> states = new Dictionary<string, FieldState>(
            StringComparer.Ordinal
        );

        internal Extraction(
            MainDocumentPart main,
            ContentSchema schema,
            DiagnosticList diagnostics,
            IDictionary<string, FieldContent> fields,
            CancellationToken cancellationToken
        )
        {
            this.main = main;
            this.schema = schema;
            this.diagnostics = diagnostics;
            this.fields = fields;
            this.cancellationToken = cancellationToken;
            styles = new WordStyles(main);
            numbering = new WordNumbering(main, styles);
        }

        internal void Execute()
        {
            WalkOutside(main.Document!.Body!);

            foreach (var field in schema.Fields)
            {
                states.TryGetValue(field.Tag, out var state);

                if (!field.Required || state == FieldState.Content || state == FieldState.Duplicate)
                    continue;

                if (state == FieldState.Absent)
                    diagnostics.Error(
                        DocumentDiagnosticCode.MissingRequiredField,
                        field.Tag,
                        null,
                        "This required field is missing; its content control was deleted or never created.",
                        "Copy the field from a new document created with the current template, or start from the template again."
                    );
                else if (state == FieldState.Placeholder)
                    diagnostics.Error(
                        DocumentDiagnosticCode.PlaceholderContent,
                        field.Tag,
                        null,
                        "This required field still shows its placeholder text.",
                        "Replace the placeholder with the release content."
                    );
                else if (state == FieldState.Empty)
                    diagnostics.Error(
                        DocumentDiagnosticCode.EmptyField,
                        field.Tag,
                        null,
                        "This required field is empty.",
                        "Enter the release content in the field."
                    );
            }
        }

        // Outside fields, only whitespace, decorative pictures and instruction controls may appear.
        private void WalkOutside(OpenXmlElement container)
        {
            foreach (var element in container.ChildElements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (element)
                {
                    case SdtElement sdt:
                        Control(sdt, () => WalkOutside(Content(sdt)));
                        break;
                    case Paragraph paragraph:
                        WalkOutsideParagraph(paragraph);
                        break;
                    case Table or TableRow or TableCell or CustomXmlElement:
                        WalkOutside(element);
                        break;
                    case AltChunk:
                        diagnostics.Error(
                            DocumentDiagnosticCode.UnboundContent,
                            null,
                            null,
                            "Embedded external content was found outside the fields.",
                            "Remove it, or paste its text into the correct field."
                        );
                        break;
                }
            }
        }

        private void WalkOutsideParagraph(OpenXmlElement paragraph)
        {
            var text = new StringBuilder();

            void Visit(OpenXmlElement element)
            {
                foreach (var child in element.ChildElements)
                    switch (child)
                    {
                        case SdtElement sdt:
                            Control(sdt, () => Visit(Content(sdt)));
                            break;
                        case Text:
                            text.Append(child.InnerText);
                            break;
                        default:
                            Visit(child);
                            break;
                    }
            }

            Visit(paragraph);

            if (text.ToString().Trim().Length != 0)
                diagnostics.Error(
                    DocumentDiagnosticCode.UnboundContent,
                    null,
                    null,
                    "Text was found outside every field: \""
                        + DiagnosticList.Excerpt(text.ToString())
                        + "\".",
                    "Move the text into the correct field, or delete it. Removing a field's control while keeping its text causes this."
                );
        }

        // Classifies a control found outside any field: instruction, top-level field, misplaced child, unknown, or container.
        private void Control(SdtElement sdt, Action transparent)
        {
            var tag = Tag(sdt);

            if (tag != null && schema.InstructionTags.Contains(tag))
                return;

            if (tag == null || !tag.StartsWith(schema.TagPrefix, StringComparison.Ordinal))
            {
                transparent();
                return;
            }

            if (!schema.AllFields.TryGetValue(tag, out var field))
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.UnknownField,
                    tag,
                    null,
                    "This field is not part of the document schema.",
                    "Remove the control or use a template matching this schema version."
                );
                return;
            }

            if (schema.Parents.TryGetValue(tag, out var parent))
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.InvalidStructure,
                    tag,
                    null,
                    "This field was found outside its section " + parent.Tag + ".",
                    "Move it back into its section, or recreate the section from the template."
                );
                return;
            }

            occurrences[tag] = occurrences.TryGetValue(tag, out var count) ? count + 1 : 1;

            if (occurrences[tag] == 2)
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.DuplicateField,
                    tag,
                    null,
                    "This field occurs more than once.",
                    "Delete the accidental copy so the field occurs exactly once."
                );
                fields.Remove(tag);
                states[tag] = FieldState.Duplicate;
            }

            if (occurrences[tag] != 1)
                return;

            var (state, content) = Parse(field, sdt);
            states[tag] = state;

            if (content != null)
                fields[tag] = content;
        }

        private (FieldState, FieldContent?) Parse(FieldDefinition field, SdtElement sdt)
        {
            if (sdt.SdtProperties?.GetFirstChild<ShowingPlaceholder>() != null)
                return (FieldState.Placeholder, null);

            if (field.Kind != FieldKind.PlainText && sdt is not SdtBlock)
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.InvalidStructure,
                    field.Tag,
                    null,
                    "This field's content control must surround whole paragraphs.",
                    "Recreate the field from the template."
                );
                return (FieldState.Content, null);
            }

            return field.Kind switch
            {
                FieldKind.PlainText => ParseText(field, sdt),
                FieldKind.RichText => ParseRich(field, sdt),
                FieldKind.Table => ParseTable(field, sdt),
                _ => ParseGroup(field, sdt),
            };
        }

        private (FieldState, FieldContent?) ParseText(FieldDefinition field, SdtElement sdt)
        {
            var context = new InlineContext(field.Tag, null, false, true);
            var lines = new List<string>();

            foreach (var (paragraph, inlines) in Paragraphs(Content(sdt), context))
            {
                if (Numbered(paragraph))
                    diagnostics.Error(
                        DocumentDiagnosticCode.UnsupportedContent,
                        field.Tag,
                        null,
                        "This field must not be formatted as a list.",
                        "Remove the list formatting from the field."
                    );

                var text = Inlines.ToPlainText(inlines);

                if (text.Trim().Length != 0)
                    lines.Add(text);
            }

            if (context.Placeholder && lines.Count == 0)
                return (FieldState.Placeholder, null);

            if (lines.Count > 1)
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.InvalidStructure,
                    field.Tag,
                    null,
                    "This field must be a single paragraph.",
                    "Join the paragraphs, using Shift+Enter where a line break is intended."
                );
                return (FieldState.Content, null);
            }

            return lines.Count == 0
                ? (FieldState.Empty, null)
                : (FieldState.Content, new TextFieldContent(field.Tag, lines[0]));
        }

        private (FieldState, FieldContent?) ParseRich(FieldDefinition field, SdtElement sdt)
        {
            var blocks = new List<DocumentBlock>();
            var lists = new ListBuilder();
            var context = new InlineContext(field.Tag, null, field.AllowHyperlinks, false);
            var position = 0;

            foreach (var element in Blocks(Content(sdt), field.Tag))
            {
                position++;

                if (element is Table)
                {
                    diagnostics.Error(
                        DocumentDiagnosticCode.UnsupportedContent,
                        field.Tag,
                        "paragraph " + position.ToString(CultureInfo.InvariantCulture),
                        "Tables are not supported in this field.",
                        "Replace the table with paragraphs or a list."
                    );
                    continue;
                }

                var paragraph = (Paragraph)element;
                context.Location = "paragraph " + position.ToString(CultureInfo.InvariantCulture);
                var inlines = ParseParagraph(paragraph, context);
                var number = styles.Numbering(paragraph);

                if (number == null || inlines.Count == 0)
                {
                    if (inlines.Count != 0)
                        blocks.AddRange(lists.Close());

                    if (inlines.Count != 0)
                        blocks.Add(new ParagraphBlock(inlines));

                    continue;
                }

                if (!field.AllowLists)
                {
                    diagnostics.Error(
                        DocumentDiagnosticCode.UnsupportedList,
                        field.Tag,
                        context.Location,
                        "Lists are not supported in this field.",
                        "Remove the list formatting, or move the list into a field that allows lists."
                    );
                    continue;
                }

                var item = numbering.Next(number.Value.NumberingId, number.Value.Level);

                if (item == null || item.Kind == null || number.Value.Level > 1)
                {
                    diagnostics.Error(
                        DocumentDiagnosticCode.UnsupportedList,
                        field.Tag,
                        context.Location,
                        item == null ? "The list formatting refers to a missing list definition."
                            : item.Kind == null
                                ? "The list numbering style \""
                                    + item.Format
                                    + "\" is not supported."
                            : "Lists may be nested at most two levels deep.",
                        "Use the template's bullet or numbered list styles with at most two levels."
                    );
                    continue;
                }

                // Direct indentation looks like nesting in Word but does not change the list level.
                if (paragraph.ParagraphProperties?.Indentation != null)
                    diagnostics.Warning(
                        DocumentDiagnosticCode.IgnoredFormatting,
                        field.Tag,
                        context.Location,
                        "The list item's manual indentation is ignored; only its list level is published.",
                        "To nest the item, press Tab at its start or use the second-level list style; otherwise remove the indentation."
                    );

                if (!lists.Add(number.Value.Level, item, inlines))
                    diagnostics.Error(
                        DocumentDiagnosticCode.UnsupportedList,
                        field.Tag,
                        context.Location,
                        "A second-level list item has no first-level item above it, or changes the list type within one item.",
                        "Use one list type per level, and start each nested list below a first-level item."
                    );
            }

            blocks.AddRange(lists.Close());

            if (context.Placeholder && blocks.Count == 0)
                return (FieldState.Placeholder, null);

            if (context.Placeholder)
                diagnostics.Error(
                    DocumentDiagnosticCode.PlaceholderContent,
                    field.Tag,
                    null,
                    "This field still contains placeholder text.",
                    "Replace or delete the remaining placeholder text."
                );

            return blocks.Count == 0
                ? (FieldState.Empty, null)
                : (FieldState.Content, new RichFieldContent(field.Tag, blocks));
        }

        private (FieldState, FieldContent?) ParseTable(FieldDefinition field, SdtElement sdt)
        {
            var tables = new List<Table>();
            var position = 0;

            foreach (var element in Blocks(Content(sdt), field.Tag))
            {
                position++;

                if (element is Table table)
                    tables.Add(table);
                else if (HasText(element))
                    diagnostics.Error(
                        DocumentDiagnosticCode.InvalidStructure,
                        field.Tag,
                        "paragraph " + position.ToString(CultureInfo.InvariantCulture),
                        "Text was found next to the table.",
                        "Move the text into a table row, or delete it."
                    );
            }

            if (tables.Count > 1)
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.InvalidStructure,
                    field.Tag,
                    null,
                    "This field must contain exactly one table.",
                    "Merge the rows into one table and delete the other tables."
                );
                return (FieldState.Content, null);
            }

            if (tables.Count == 0)
                return (FieldState.Empty, null);

            var rows = new List<List<IReadOnlyList<DocumentInline>>>();
            var empty = new List<int>();
            var placeholder = false;
            var number = 0;
            var valid = true;

            foreach (var row in Flatten<TableRow>(tables[0]))
            {
                number++;
                var location = "row " + number.ToString(CultureInfo.InvariantCulture);

                if (row.TableRowProperties?.GetFirstChild<TableHeader>() != null)
                    continue;

                var cells = Flatten<TableCell>(row).ToArray();
                // Property exceptions (w:tblPrEx), which Word writes on ordinary rows, are formatting only.
                var irregular =
                    row.TableRowProperties?.GetFirstChild<GridBefore>() != null
                    || row.TableRowProperties?.GetFirstChild<GridAfter>() != null
                    || cells.Any(cell =>
                        cell.TableCellProperties?.GridSpan?.Val?.Value > 1
                        || cell.TableCellProperties?.VerticalMerge != null
                        || cell.TableCellProperties?.HorizontalMerge != null
                        || cell.Descendants<Table>().Any()
                    );

                if (irregular || cells.Length != field.Columns)
                {
                    diagnostics.Error(
                        DocumentDiagnosticCode.InvalidTableRow,
                        field.Tag,
                        location,
                        "Each row must have exactly "
                            + field.Columns.ToString(CultureInfo.InvariantCulture)
                            + " cells without merged cells or nested tables.",
                        "Split merged cells and keep one value per cell, or delete the row."
                    );
                    valid = false;
                    continue;
                }

                var values = new List<IReadOnlyList<DocumentInline>>();
                var context = new InlineContext(field.Tag, location, field.AllowHyperlinks, false);

                foreach (var cell in cells)
                {
                    var paragraphs = Paragraphs(cell, context)
                        .Select(pair => pair.Inlines)
                        .Where(inlines => inlines.Count != 0)
                        .ToArray();

                    if (paragraphs.Length > 1)
                    {
                        diagnostics.Error(
                            DocumentDiagnosticCode.InvalidTableRow,
                            field.Tag,
                            location,
                            "A cell contains more than one paragraph.",
                            "Use Shift+Enter for a line break inside a cell."
                        );
                        valid = false;
                    }

                    values.Add(paragraphs.FirstOrDefault() ?? Array.Empty<DocumentInline>());
                }

                placeholder |= context.Placeholder;
                var filled = values.Count(cell => Inlines.ToPlainText(cell).Trim().Length != 0);

                if (filled == 0)
                {
                    empty.Add(rows.Count);
                    rows.Add(values);
                    continue;
                }

                if (filled != field.Columns)
                {
                    diagnostics.Error(
                        DocumentDiagnosticCode.InvalidTableRow,
                        field.Tag,
                        location,
                        "The row is incomplete: "
                            + (
                                Inlines.ToPlainText(values[0]).Trim().Length == 0
                                    ? "the first cell"
                                    : "a value"
                            )
                            + " is missing.",
                        "Complete the row or delete it."
                    );
                    valid = false;
                }

                rows.Add(values);
            }

            // Blank rows at the end are ignored; a blank row between data rows is ambiguous and must be removed.
            while (empty.Count != 0 && empty[empty.Count - 1] == rows.Count - 1)
            {
                rows.RemoveAt(rows.Count - 1);
                empty.RemoveAt(empty.Count - 1);
            }

            if (empty.Count != 0)
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.InvalidTableRow,
                    field.Tag,
                    null,
                    "The table contains an empty row between filled rows.",
                    "Delete the empty row."
                );
                valid = false;
            }

            if (rows.Count == 0)
                return (placeholder ? FieldState.Placeholder : FieldState.Empty, null);

            if (placeholder)
            {
                diagnostics.Error(
                    DocumentDiagnosticCode.PlaceholderContent,
                    field.Tag,
                    null,
                    "The table still contains placeholder text.",
                    "Replace the placeholder text or delete the row."
                );
                valid = false;
            }

            return (
                FieldState.Content,
                valid
                    ? new TableFieldContent(
                        field.Tag,
                        rows.Select(row => row.Select(cell => (IEnumerable<DocumentInline>)cell))
                    )
                    : null
            );
        }

        private (FieldState, FieldContent?) ParseGroup(FieldDefinition field, SdtElement sdt)
        {
            var children = new Dictionary<string, (FieldState State, FieldContent? Content)>(
                StringComparer.Ordinal
            );

            void Visit(OpenXmlElement container)
            {
                foreach (var element in container.ChildElements)
                {
                    if (element is not SdtElement control)
                    {
                        if (
                            element
                            is Paragraph
                                or Table
                                or TableRow
                                or TableCell
                                or CustomXmlElement
                        )
                        {
                            if (element is Paragraph && HasText(element, true))
                                diagnostics.Error(
                                    DocumentDiagnosticCode.UnboundContent,
                                    field.Tag,
                                    null,
                                    "Text was found in this section outside its fields: \""
                                        + DiagnosticList.Excerpt(OwnText(element))
                                        + "\".",
                                    "Move the text into the section's heading or body field, or delete it."
                                );

                            Visit(element);
                        }

                        continue;
                    }

                    var tag = Tag(control);

                    if (tag != null && schema.InstructionTags.Contains(tag))
                        continue;

                    var child = field.Children.FirstOrDefault(item => item.Tag == tag);

                    if (child == null)
                    {
                        if (
                            tag != null
                            && tag.StartsWith(schema.TagPrefix, StringComparison.Ordinal)
                        )
                            diagnostics.Error(
                                DocumentDiagnosticCode.InvalidStructure,
                                tag,
                                null,
                                "This field was found inside "
                                    + field.Tag
                                    + ", where it does not belong.",
                                "Move it back to its own position, or recreate the section from the template."
                            );
                        else
                            Visit(Content(control));

                        continue;
                    }

                    if (children.ContainsKey(child.Tag))
                    {
                        diagnostics.Error(
                            DocumentDiagnosticCode.DuplicateField,
                            child.Tag,
                            null,
                            "This field occurs more than once in its section.",
                            "Delete the accidental copy."
                        );
                        continue;
                    }

                    children.Add(child.Tag, Parse(child, control));
                }
            }

            Visit(Content(sdt));

            var present = children
                .Where(pair => pair.Value.State == FieldState.Content)
                .Select(pair => pair.Key)
                .ToArray();

            if (present.Length == 0)
                return (
                    children.Values.Any(item => item.State == FieldState.Placeholder)
                        ? FieldState.Placeholder
                        : FieldState.Empty,
                    null
                );

            foreach (
                var child in field.Children.Where(item =>
                    item.Required && !present.Contains(item.Tag)
                )
            )
                diagnostics.Error(
                    DocumentDiagnosticCode.IncompleteSection,
                    child.Tag,
                    null,
                    "The section has content, but this part of it is "
                        + (children.ContainsKey(child.Tag) ? "empty." : "missing."),
                    "Complete the section, or delete the whole section including its heading."
                );

            return (
                FieldState.Content,
                new GroupFieldContent(
                    field.Tag,
                    children
                        .Values.Where(item => item.Content != null)
                        .Select(item => item.Content!)
                )
            );
        }

        // Block-level paragraphs and tables of a field, descending through untagged containers.
        private IEnumerable<OpenXmlElement> Blocks(OpenXmlElement container, string field)
        {
            foreach (var element in container.ChildElements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (element)
                {
                    case Paragraph or Table:
                        yield return element;
                        break;
                    case SdtElement sdt:
                        var tag = Tag(sdt);

                        if (tag != null && schema.InstructionTags.Contains(tag))
                            break;

                        if (
                            tag != null
                            && tag.StartsWith(schema.TagPrefix, StringComparison.Ordinal)
                        )
                        {
                            diagnostics.Error(
                                DocumentDiagnosticCode.InvalidStructure,
                                tag,
                                null,
                                "This field was found inside "
                                    + field
                                    + ", where it does not belong.",
                                "Move it back to its own position, or recreate it from the template."
                            );
                            break;
                        }

                        foreach (var item in Blocks(Content(sdt), field))
                            yield return item;

                        break;
                    case CustomXmlElement:
                        foreach (var item in Blocks(element, field))
                            yield return item;

                        break;
                    case AltChunk:
                        diagnostics.Error(
                            DocumentDiagnosticCode.UnsupportedContent,
                            field,
                            null,
                            "Embedded external content is not supported.",
                            "Paste the content as ordinary text instead."
                        );
                        break;
                }
            }
        }

        private IEnumerable<(
            Paragraph Paragraph,
            IReadOnlyList<DocumentInline> Inlines
        )> Paragraphs(OpenXmlElement container, InlineContext context)
        {
            if (container is SdtContentRun inline)
            {
                var parent = inline.Ancestors<Paragraph>().FirstOrDefault() ?? new Paragraph();
                var parser = new InlineParser(this, parent, context);
                parser.Visit(inline);
                parser.Finish();
                var result = Inlines.Normalize(parser.Result);
                yield return (
                    parent,
                    Inlines.ToPlainText(result).Trim().Length == 0
                        ? Array.Empty<DocumentInline>()
                        : result
                );
                yield break;
            }

            foreach (var element in Blocks(container, context.Field))
                if (element is Paragraph paragraph)
                    yield return (paragraph, ParseParagraph(paragraph, context));
                else
                    diagnostics.Error(
                        DocumentDiagnosticCode.UnsupportedContent,
                        context.Field,
                        context.Location,
                        "Nested tables are not supported in this field.",
                        "Replace the table with text."
                    );
        }

        private bool Numbered(Paragraph paragraph) => styles.Numbering(paragraph) != null;

        private IReadOnlyList<DocumentInline> ParseParagraph(
            Paragraph paragraph,
            InlineContext context
        )
        {
            var parser = new InlineParser(this, paragraph, context);
            parser.Visit(paragraph);
            parser.Finish();
            var result = Inlines.Normalize(parser.Result);

            // Whitespace alone is spacing, not content.
            return Inlines.ToPlainText(result).Trim().Length == 0
                ? Array.Empty<DocumentInline>()
                : result;
        }

        private static OpenXmlElement Content(SdtElement sdt) =>
            (OpenXmlElement?)sdt.GetFirstChild<SdtContentBlock>()
            ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentRun>()
            ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentCell>()
            ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentRow>()
            ?? sdt;

        private static IEnumerable<T> Flatten<T>(OpenXmlElement container)
            where T : OpenXmlElement
        {
            foreach (var element in container.ChildElements)
                if (element is T match)
                    yield return match;
                else if (element is SdtElement sdt)
                    foreach (var item in Flatten<T>(Content(sdt)))
                        yield return item;
                else if (element is CustomXmlElement)
                    foreach (var item in Flatten<T>(element))
                        yield return item;
        }

        private bool HasText(OpenXmlElement element, bool ownOnly = false) =>
            (ownOnly ? OwnText(element) : element.InnerText).Trim().Length != 0;

        // Text of a paragraph excluding nested controls, which are classified separately.
        private static string OwnText(OpenXmlElement element)
        {
            var text = new StringBuilder();

            void Visit(OpenXmlElement current)
            {
                foreach (var child in current.ChildElements)
                    if (child is Text)
                        text.Append(child.InnerText);
                    else if (child is not SdtElement)
                        Visit(child);
            }

            Visit(element);
            return text.ToString();
        }

        private enum FieldState
        {
            Absent,
            Empty,
            Placeholder,
            Content,
            Duplicate,
        }

        private sealed class InlineContext
        {
            internal string Field { get; }
            internal string? Location { get; set; }
            internal bool AllowHyperlinks { get; }
            internal bool Plain { get; }
            internal bool Placeholder { get; set; }

            internal InlineContext(string field, string? location, bool allowHyperlinks, bool plain)
            {
                Field = field;
                Location = location;
                AllowHyperlinks = allowHyperlinks;
                Plain = plain;
            }
        }

        // Converts one paragraph's runs into inlines, interpreting hyperlinks, HYPERLINK fields and supported characters.
        private sealed class InlineParser
        {
            private readonly Extraction owner;
            private readonly Paragraph paragraph;
            private readonly InlineContext context;
            private readonly List<DocumentInline> result = new List<DocumentInline>();
            private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
            private List<DocumentInline>? linkContent;
            private int fieldDepth;
            private bool fieldInstruction;
            private bool fieldIgnored;
            private readonly StringBuilder instruction = new StringBuilder();
            private Uri? fieldTarget;

            internal IReadOnlyList<DocumentInline> Result => result;

            internal InlineParser(Extraction owner, Paragraph paragraph, InlineContext context)
            {
                this.owner = owner;
                this.paragraph = paragraph;
                this.context = context;
            }

            internal void Visit(OpenXmlElement container)
            {
                foreach (var element in container.ChildElements)
                    switch (element)
                    {
                        case Run run:
                            ParseRun(run);
                            break;
                        case Hyperlink link:
                            Link(link);
                            break;
                        case SdtRun sdt:
                            var tag = Tag(sdt);

                            if (tag != null && owner.schema.InstructionTags.Contains(tag))
                                break;

                            if (
                                tag != null
                                && tag.StartsWith(owner.schema.TagPrefix, StringComparison.Ordinal)
                            )
                                owner.diagnostics.Error(
                                    DocumentDiagnosticCode.InvalidStructure,
                                    tag,
                                    null,
                                    "This field was found inside "
                                        + context.Field
                                        + ", where it does not belong.",
                                    "Move it back to its own position, or recreate it from the template."
                                );
                            else if (sdt.SdtProperties?.GetFirstChild<ShowingPlaceholder>() != null)
                                context.Placeholder = true;
                            else
                                Visit(Content(sdt));

                            break;
                        case SimpleField simple:
                            ParseSimpleField(simple);
                            break;
                        case DeletedRun or MoveFromRun:
                            break;
                        case InsertedRun or MoveToRun or CustomXmlRun:
                            Visit(element);
                            break;
                        case ParagraphProperties
                        or BookmarkStart
                        or BookmarkEnd
                        or ProofError
                        or CommentRangeStart
                        or CommentRangeEnd
                        or PermStart
                        or PermEnd:
                            break;
                        default:
                            if (element.LocalName == "smartTag")
                                Visit(element);
                            else if (element.LocalName is "oMath" or "oMathPara")
                                Unsupported(
                                    "Equations are not supported.",
                                    "Write the content as text."
                                );
                            else if (
                                element.InnerText.Trim().Length != 0
                                || element.Descendants<Drawing>().Any()
                            )
                                Unsupported(
                                    "Unsupported content was found.",
                                    "Replace it with plain text in the field."
                                );

                            break;
                    }
            }

            internal void Finish()
            {
                if (fieldDepth != 0)
                    Unsupported(
                        "A Word field continues beyond the end of the paragraph.",
                        "Convert the field to text (select it and press Ctrl+Shift+F9), or retype it."
                    );
            }

            private void ParseRun(Run run)
            {
                if (WordStyles.IsPlaceholder(run) && run.InnerText.Length != 0)
                {
                    context.Placeholder = true;
                    return;
                }

                var hidden = owner.styles.IsHidden(run, paragraph);
                var marks = context.Plain ? TextMarks.None : owner.styles.Marks(run, paragraph);

                foreach (var element in run.ChildElements)
                {
                    switch (element)
                    {
                        case FieldChar character:
                            FieldCharacter(character);
                            continue;
                        case FieldCode code:
                            if (fieldInstruction)
                                instruction.Append(code.Text);

                            continue;
                        case RunProperties
                        or LastRenderedPageBreak
                        or CommentReference
                        or AnnotationReferenceMark
                        or DeletedText
                        or DeletedFieldCode:
                            continue;
                    }

                    if (fieldInstruction || fieldIgnored)
                        continue;

                    if (hidden && element is Text or TabChar)
                    {
                        Unsupported(
                            "Hidden text was found.",
                            "Unhide the text (Font > Hidden) or delete it."
                        );
                        continue;
                    }

                    switch (element)
                    {
                        case Text text:
                            if (text.Text.Length != 0)
                                Emit(new TextInline(text.Text, marks));

                            break;
                        case TabChar or PositionalTab:
                            Emit(new TextInline("\t", marks));
                            break;
                        case Break brk:
                            if (brk.Type == null || brk.Type.Value == BreakValues.TextWrapping)
                                Emit(LineBreakInline.Instance);
                            else
                                owner.diagnostics.Warning(
                                    DocumentDiagnosticCode.IgnoredFormatting,
                                    context.Field,
                                    context.Location,
                                    "A manual page or column break was ignored; the publication layout controls pagination.",
                                    "Remove the break to silence this warning."
                                );

                            break;
                        case CarriageReturn:
                            Emit(LineBreakInline.Instance);
                            break;
                        case NoBreakHyphen:
                            Emit(new TextInline("‑", marks));
                            break;
                        case SoftHyphen:
                            Emit(new TextInline("­", marks));
                            break;
                        case SymbolChar:
                            Unsupported(
                                "A symbol inserted from a symbol font was found.",
                                "Insert the character as Unicode text instead (Insert > Symbol, normal text font)."
                            );
                            break;
                        case FootnoteReference or EndnoteReference:
                            Unsupported(
                                "Footnotes and endnotes are not supported.",
                                "Move the note into the text."
                            );
                            break;
                        case Drawing or Picture or EmbeddedObject or AlternateContent:
                            Unsupported(
                                "Pictures, text boxes, shapes and embedded objects are not supported here.",
                                "Remove the object; supply images separately."
                            );
                            break;
                        default:
                            if (element.InnerText.Length != 0)
                                Unsupported(
                                    "Unsupported content was found.",
                                    "Replace it with plain text in the field."
                                );

                            break;
                    }
                }
            }

            private void Link(Hyperlink link)
            {
                var target = Target(link);

                if (target == null)
                {
                    Visit(link);
                    return;
                }

                linkContent = new List<DocumentInline>();
                Visit(link);
                CloseLink(target);
            }

            private Uri? Target(Hyperlink link)
            {
                if (!Allowed())
                    return null;

                if (link.Id?.Value is not string id)
                {
                    Unsupported(
                        "Links to places inside the document are not supported.",
                        "Link to an external web or email address, or remove the link."
                    );
                    return null;
                }

                var relationship = owner.main.HyperlinkRelationships.FirstOrDefault(item =>
                    item.Id == id
                );

                if (relationship == null)
                {
                    owner.diagnostics.Error(
                        DocumentDiagnosticCode.BrokenHyperlink,
                        context.Field,
                        context.Location,
                        "A link has no destination.",
                        "Edit the link in Word and enter its address again."
                    );
                    return null;
                }

                return Destination(relationship.Uri.OriginalString);
            }

            private bool Allowed()
            {
                if (context.AllowHyperlinks)
                    return true;

                Unsupported(
                    "Links are not supported in this field.",
                    "Remove the link and keep its text."
                );
                return false;
            }

            private Uri? Destination(string text)
            {
                if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri))
                {
                    owner.diagnostics.Error(
                        DocumentDiagnosticCode.BrokenHyperlink,
                        context.Field,
                        context.Location,
                        "A link destination is not an absolute address.",
                        "Edit the link and enter a complete address such as https://example.com/."
                    );
                    return null;
                }

                if (!HyperlinkInline.IsSupportedScheme(uri.Scheme))
                {
                    owner.diagnostics.Error(
                        DocumentDiagnosticCode.UnsafeHyperlink,
                        context.Field,
                        context.Location,
                        "A link uses the unsupported scheme \"" + uri.Scheme + "\".",
                        "Use an https, http or mailto link, or remove the link."
                    );
                    return null;
                }

                return uri;
            }

            private void CloseLink(Uri target)
            {
                var content = Inlines.Normalize(linkContent!);
                linkContent = null;

                if (content.Count == 0 || Inlines.ToPlainText(content).Trim().Length == 0)
                    owner.diagnostics.Warning(
                        DocumentDiagnosticCode.BrokenHyperlink,
                        context.Field,
                        context.Location,
                        "A link without visible text was ignored.",
                        "Delete the empty link or give it text."
                    );
                else
                    result.Add(new HyperlinkInline(target, content));
            }

            private void ParseSimpleField(SimpleField field)
            {
                var target = HyperlinkField(field.Instruction?.Value ?? "");

                if (target == null)
                    return;

                linkContent = new List<DocumentInline>();
                Visit(field);
                CloseLink(target);
            }

            private void FieldCharacter(FieldChar character)
            {
                var type = character.FieldCharType?.Value;

                if (type == FieldCharValues.Begin)
                {
                    if (++fieldDepth == 1)
                    {
                        instruction.Clear();
                        fieldInstruction = true;
                    }
                    else
                        fieldIgnored = true;

                    return;
                }

                if (type == FieldCharValues.Separate && fieldDepth == 1)
                {
                    fieldInstruction = false;
                    fieldTarget = HyperlinkField(instruction.ToString());
                    fieldIgnored = fieldTarget == null;

                    if (fieldTarget != null)
                        linkContent = new List<DocumentInline>();

                    return;
                }

                if (type == FieldCharValues.End && fieldDepth > 0 && --fieldDepth == 0)
                {
                    if (fieldInstruction)
                        HyperlinkField(instruction.ToString(), false);

                    if (fieldTarget != null)
                        CloseLink(fieldTarget);

                    fieldInstruction = false;
                    fieldIgnored = false;
                    fieldTarget = null;
                }
            }

            // Only HYPERLINK fields are interpreted; others are diagnosed and their cached result is not published.
            private Uri? HyperlinkField(string code, bool hasResult = true)
            {
                var text = code.Trim();

                if (!text.StartsWith("HYPERLINK", StringComparison.OrdinalIgnoreCase))
                {
                    Unsupported(
                        "The Word field \""
                            + DiagnosticList.Excerpt(text.Split(' ')[0])
                            + "\" is not supported.",
                        "Convert the field to text (select it and press Ctrl+Shift+F9), or retype the text."
                    );
                    return null;
                }

                if (!Allowed())
                    return null;

                var rest = text.Substring("HYPERLINK".Length).Trim();

                if (rest.StartsWith("\\l", StringComparison.Ordinal) || rest.Length == 0)
                {
                    Unsupported(
                        "Links to places inside the document are not supported.",
                        "Link to an external web or email address, or remove the link."
                    );
                    return null;
                }

                var address = rest.StartsWith("\"", StringComparison.Ordinal)
                    ? rest.Substring(1, Math.Max(0, rest.IndexOf('"', 1) - 1))
                    : rest.Split(' ')[0];

                if (!hasResult)
                    return null;

                return Destination(address);
            }

            private void Emit(DocumentInline inline)
            {
                if (linkContent != null)
                    linkContent.Add(inline);
                else
                    result.Add(inline);
            }

            private void Unsupported(string message, string remedy)
            {
                if (reported.Add(message))
                    owner.diagnostics.Error(
                        DocumentDiagnosticCode.UnsupportedContent,
                        context.Field,
                        context.Location,
                        message,
                        remedy
                    );
            }
        }

        // Groups consecutive list paragraphs into lists of at most two levels.
        private sealed class ListBuilder
        {
            private List<ListItem>? items;
            private ListKind kind;
            private int start;
            private int definition;
            private List<DocumentInline>? pendingContent;
            private List<ListItem>? children;
            private ListKind childKind;
            private int childStart;
            private readonly List<DocumentBlock> closed = new List<DocumentBlock>();

            internal bool Add(
                int level,
                WordNumbering.Item item,
                IReadOnlyList<DocumentInline> content
            )
            {
                if (level == 0)
                {
                    if (items != null && (item.Definition != definition || item.Kind != kind))
                        CloseList();

                    FlushItem();

                    if (items == null)
                    {
                        items = new List<ListItem>();
                        kind = item.Kind!.Value;
                        start = item.Number;
                        definition = item.Definition;
                    }

                    pendingContent = content.ToList();
                    return true;
                }

                if (pendingContent == null)
                    return false;

                if (children != null && item.Kind != childKind)
                {
                    // A kind change inside one parent starts a new nested list; v1 keeps only one nested list per item.
                    return false;
                }

                if (children == null)
                {
                    children = new List<ListItem>();
                    childKind = item.Kind!.Value;
                    childStart = item.Number;
                }

                children.Add(new ListItem(content));
                return true;
            }

            internal IEnumerable<DocumentBlock> Close()
            {
                CloseList();
                var result = closed.ToArray();
                closed.Clear();
                return result;
            }

            private void FlushItem()
            {
                if (pendingContent == null)
                    return;

                items!.Add(
                    new ListItem(
                        pendingContent,
                        children == null ? null : new ListBlock(childKind, childStart, children)
                    )
                );
                pendingContent = null;
                children = null;
            }

            private void CloseList()
            {
                FlushItem();

                if (items != null)
                    closed.Add(new ListBlock(kind, start, items));

                items = null;
            }
        }
    }
}
