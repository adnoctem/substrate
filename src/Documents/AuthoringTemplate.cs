using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AdNoctem.Substrate.Documents;

/// <summary>Top-level or nested content of an authoring template.</summary>
public abstract class AuthoringElement
{
    private protected AuthoringElement() { }
}

/// <summary>Visible author guidance in a control with an instruction tag. Readers skip it; it is never published.</summary>
public sealed class AuthoringInstruction : AuthoringElement
{
    public string Tag { get; }
    public string Text { get; }

    public AuthoringInstruction(string tag, string text)
    {
        Tag = PressReleaseSection.RequireText(tag, nameof(tag));
        Text = PressReleaseSection.RequireText(text, nameof(text));
    }
}

/// <summary>One content control of an authoring template, with a stable tag and a localized title.</summary>
/// <remarks>Prompt is placeholder text that Word shows while the field is empty and restores when it is cleared. Content is
/// prefilled wording saved into documents created from the template. Locked controls can be edited but not deleted.</remarks>
public sealed class AuthoringField : AuthoringElement
{
    public string Tag { get; }
    public string Title { get; }
    public FieldKind Kind { get; }
    public bool Locked { get; }
    public string? Prompt { get; }
    public string? ParagraphStyle { get; }
    public IReadOnlyList<DocumentBlock>? Content { get; }
    public IReadOnlyList<string> ColumnTitles { get; }
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<DocumentInline>>> Rows { get; }
    public int EmptyRows { get; }
    public IReadOnlyList<AuthoringElement> Children { get; }

    private AuthoringField(
        string tag,
        string title,
        FieldKind kind,
        bool locked,
        string? prompt,
        string? paragraphStyle,
        IEnumerable<DocumentBlock>? content,
        IEnumerable<string>? columnTitles = null,
        IEnumerable<IEnumerable<IEnumerable<DocumentInline>>>? rows = null,
        int emptyRows = 0,
        IEnumerable<AuthoringElement>? children = null
    )
    {
        Tag = PressReleaseSection.RequireText(tag, nameof(tag));
        Title = PressReleaseSection.RequireText(title, nameof(title));
        Kind = kind;
        Locked = locked;
        // Empty strings mean absent, so PowerShell callers can pass $null to string parameters.
        Prompt = string.IsNullOrEmpty(prompt) ? null : prompt;
        ParagraphStyle = string.IsNullOrEmpty(paragraphStyle) ? null : paragraphStyle;
        Content = content == null ? null : Array.AsReadOnly(content.ToArray());
        ColumnTitles = Array.AsReadOnly((columnTitles ?? []).ToArray());
        Rows = Array.AsReadOnly(
            (rows ?? [])
                .Select(row =>
                    (IReadOnlyList<IReadOnlyList<DocumentInline>>)
                        Array.AsReadOnly(
                            row.Select(cell =>
                                    (IReadOnlyList<DocumentInline>)Array.AsReadOnly(cell.ToArray())
                                )
                                .ToArray()
                        )
                )
                .ToArray()
        );
        EmptyRows =
            emptyRows >= 0 ? emptyRows : throw new ArgumentOutOfRangeException(nameof(emptyRows));
        Children = Array.AsReadOnly((children ?? []).ToArray());

        if (Prompt == null && Content == null && kind is FieldKind.PlainText or FieldKind.RichText)
            throw new ArgumentException("A text field needs a prompt or prefilled content.");
    }

    public static AuthoringField Text(
        string tag,
        string title,
        string? prompt,
        string? content = null,
        bool locked = false,
        string? paragraphStyle = null
    ) =>
        new AuthoringField(
            tag,
            title,
            FieldKind.PlainText,
            locked,
            prompt,
            paragraphStyle,
            string.IsNullOrEmpty(content) ? null : [new ParagraphBlock([new TextInline(content!)])]
        );

    public static AuthoringField Rich(
        string tag,
        string title,
        string? prompt,
        IEnumerable<DocumentBlock>? content = null,
        bool locked = false,
        string? paragraphStyle = null
    ) =>
        new AuthoringField(tag, title, FieldKind.RichText, locked, prompt, paragraphStyle, content);

    /// <remarks>Column titles form a repeated header row, which readers skip. Empty rows give authors room to type.</remarks>
    public static AuthoringField Table(
        string tag,
        string title,
        IEnumerable<string> columnTitles,
        IEnumerable<IEnumerable<IEnumerable<DocumentInline>>>? rows = null,
        int emptyRows = 3,
        bool locked = false
    ) =>
        new AuthoringField(
            tag,
            title,
            FieldKind.Table,
            locked,
            null,
            null,
            null,
            columnTitles,
            rows,
            emptyRows
        );

    public static AuthoringField Group(
        string tag,
        string title,
        bool locked,
        params AuthoringElement[] children
    ) =>
        new AuthoringField(
            tag,
            title,
            FieldKind.Group,
            locked,
            null,
            null,
            null,
            children: children
        );
}

/// <summary>Declares an authoring template: content in document order, document properties and optional styling source.</summary>
/// <remarks>Styles, theme, fonts and page size are copied from the style source; its content, headers and metadata are not.
/// The proofing language replaces language settings in the copied styles.</remarks>
public sealed class AuthoringTemplate
{
    public IReadOnlyList<AuthoringElement> Content { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }
    public string? ProofingLanguage { get; }
    public byte[]? StyleSource { get; }
    public string? InstructionStyle { get; }

    public AuthoringTemplate(
        IEnumerable<AuthoringElement> content,
        IDictionary<string, string> properties,
        string? proofingLanguage = null,
        byte[]? styleSource = null,
        string? instructionStyle = null
    )
    {
        Content = Array.AsReadOnly(
            (content ?? throw new ArgumentNullException(nameof(content))).ToArray()
        );
        Properties = new Dictionary<string, string>(
            properties ?? throw new ArgumentNullException(nameof(properties)),
            StringComparer.Ordinal
        );
        ProofingLanguage = string.IsNullOrEmpty(proofingLanguage) ? null : proofingLanguage;
        StyleSource = styleSource;
        InstructionStyle = string.IsNullOrEmpty(instructionStyle) ? null : instructionStyle;
    }
}

/// <summary>Writes macro-free Word authoring templates (.dotx) or documents from a declaration.</summary>
/// <remarks>Output is reproducible for the same declaration: control identifiers and placeholder names derive from tags.
/// No core or application metadata, macros, external links or attached-template paths are written.</remarks>
public sealed class AuthoringTemplateBuilder
{
    public void Write(
        AuthoringTemplate template,
        Stream destination,
        bool asTemplate = true,
        CancellationToken cancellationToken = default
    )
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        using (var buffer = new MemoryStream())
        {
            using (
                var document = WordprocessingDocument.Create(
                    buffer,
                    asTemplate
                        ? WordprocessingDocumentType.Template
                        : WordprocessingDocumentType.Document
                )
            )
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var page = CopyStyles(template, main);
                EnsurePlaceholderStyle(main);
                EnsureListStyles(main);
                main.AddNewPart<DocumentSettingsPart>().Settings = new Settings(
                    new DefaultTabStop { Val = 720 },
                    new CharacterSpacingControl { Val = CharacterSpacingValues.DoNotCompress }
                );
                var writer = new Writer(main, template, cancellationToken);

                foreach (var element in template.Content)
                    main.Document.Body!.AppendChild(writer.Element(element));

                main.Document.Body!.AppendChild(page);
                writer.FinishGlossary();
                AddProperties(document, template.Properties);
            }

            buffer.Position = 0;
            buffer.CopyTo(destination);
        }
    }

    private static SectionProperties CopyStyles(AuthoringTemplate template, MainDocumentPart main)
    {
        var section = new SectionProperties(
            new PageSize { Width = 11906, Height = 16838 },
            new PageMargin
            {
                Top = 1417,
                Right = 1417,
                Bottom = 1417,
                Left = 1417,
                Header = 708,
                Footer = 708,
                Gutter = 0,
            }
        );

        if (template.StyleSource == null)
        {
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(
                new Style(new StyleName { Val = "Normal" })
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Normal",
                    Default = true,
                }
            );
            return section;
        }

        using (
            var source = WordprocessingDocument.Open(
                new MemoryStream(template.StyleSource, false),
                false
            )
        )
        {
            var origin =
                source.MainDocumentPart
                ?? throw new ArgumentException("The style source has no document part.");

            void Copy<T>(T? part, Func<T> create)
                where T : OpenXmlPart
            {
                if (part == null)
                    return;

                using (var stream = part.GetStream(FileMode.Open, FileAccess.Read))
                    create().FeedData(stream);
            }

            Copy(origin.StyleDefinitionsPart, () => main.AddNewPart<StyleDefinitionsPart>());
            Copy(origin.ThemePart, () => main.AddNewPart<ThemePart>());
            Copy(origin.FontTablePart, () => main.AddNewPart<FontTablePart>());
            Copy(
                origin.NumberingDefinitionsPart,
                () => main.AddNewPart<NumberingDefinitionsPart>()
            );

            var sourceSection = origin
                .Document?.Body?.Elements<SectionProperties>()
                .LastOrDefault();

            if (sourceSection?.GetFirstChild<PageSize>() is PageSize size)
                section.ReplaceChild(
                    (PageSize)size.CloneNode(true),
                    section.GetFirstChild<PageSize>()!
                );

            if (sourceSection?.GetFirstChild<PageMargin>() is PageMargin margin)
                section.ReplaceChild(
                    (PageMargin)margin.CloneNode(true),
                    section.GetFirstChild<PageMargin>()!
                );
        }

        if (template.ProofingLanguage != null && main.StyleDefinitionsPart?.Styles is Styles styles)
            foreach (var language in styles.Descendants<Languages>())
                language.Val = template.ProofingLanguage;

        return section;
    }

    private static void EnsurePlaceholderStyle(MainDocumentPart main)
    {
        var styles = (
            main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>()
        ).Styles ??= new Styles();

        if (
            styles
                .Elements<Style>()
                .Any(style => style.StyleId?.Value == WordStyles.PlaceholderStyle)
        )
            return;

        styles.AppendChild(
            new Style(
                new StyleName { Val = "Placeholder Text" },
                new UIPriority { Val = 99 },
                new SemiHidden(),
                new StyleRunProperties(new Color { Val = "808080" })
            )
            {
                Type = StyleValues.Character,
                StyleId = WordStyles.PlaceholderStyle,
            }
        );
    }

    // Word's built-in list styles, linked to two-level definitions so Tab and Shift+Tab change the level. Existing
    // styles from the style source are kept unchanged.
    private static void EnsureListStyles(MainDocumentPart main)
    {
        var styles = main.StyleDefinitionsPart!.Styles!;
        var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
        var numbering = part.Numbering ??= new Numbering();
        bool Exists(string id) => styles.Elements<Style>().Any(style => style.StyleId?.Value == id);

        foreach (
            var (first, second, name, format) in new[]
            {
                ("ListBullet", "ListBullet2", "List Bullet", NumberFormatValues.Bullet),
                ("ListNumber", "ListNumber2", "List Number", NumberFormatValues.Decimal),
            }
        )
        {
            if (Exists(first) || Exists(second))
                continue;

            var definition =
                numbering
                    .Elements<AbstractNum>()
                    .Select(item => item.AbstractNumberId?.Value ?? 0)
                    .DefaultIfEmpty(-1)
                    .Max() + 1;
            var instance =
                numbering
                    .Elements<NumberingInstance>()
                    .Select(item => item.NumberID?.Value ?? 0)
                    .DefaultIfEmpty(0)
                    .Max() + 1;
            var bullet = format == NumberFormatValues.Bullet;
            var abstractNum = new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.Multilevel }
            )
            {
                AbstractNumberId = definition,
            };

            for (var level = 0; level < 2; level++)
                abstractNum.AppendChild(
                    new Level(
                        new StartNumberingValue { Val = 1 },
                        new NumberingFormat { Val = format },
                        new ParagraphStyleIdInLevel { Val = level == 0 ? first : second },
                        new LevelText
                        {
                            Val = bullet
                                ? (level == 0 ? "\u2022" : "\u2013")
                                : "%" + (level + 1) + ".",
                        },
                        new LevelJustification { Val = LevelJustificationValues.Left },
                        new PreviousParagraphProperties(
                            new Indentation
                            {
                                Left = (360 * (level + 1)).ToString(),
                                Hanging = "360",
                            }
                        )
                    )
                    {
                        LevelIndex = level,
                    }
                );

            var last = numbering.Elements<AbstractNum>().LastOrDefault();

            if (last != null)
                last.InsertAfterSelf(abstractNum);
            else
                numbering.PrependChild(abstractNum);

            numbering.AppendChild(
                new NumberingInstance(new AbstractNumId { Val = definition })
                {
                    NumberID = instance,
                }
            );

            for (var level = 0; level < 2; level++)
            {
                var numberingProperties = new NumberingProperties(
                    new NumberingId { Val = instance }
                );

                if (level == 1)
                    numberingProperties.PrependChild(new NumberingLevelReference { Val = 1 });

                styles.AppendChild(
                    new Style(
                        new StyleName
                        {
                            Val =
                                level == 0
                                    ? name.ToLowerInvariant()
                                    : name.ToLowerInvariant() + " 2",
                        },
                        new BasedOn { Val = "Normal" },
                        new UIPriority { Val = 99 },
                        new PrimaryStyle(),
                        new StyleParagraphProperties(numberingProperties)
                    )
                    {
                        Type = StyleValues.Paragraph,
                        StyleId = level == 0 ? first : second,
                    }
                );
            }
        }
    }

    private static void AddProperties(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string> properties
    )
    {
        if (properties.Count == 0)
            return;

        var part = document.AddCustomFilePropertiesPart();
        part.Properties = new DocumentFormat.OpenXml.CustomProperties.Properties();
        var id = 2;

        foreach (var pair in properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            part.Properties.AppendChild(
                new CustomDocumentProperty(new VTLPWSTR(pair.Value))
                {
                    FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
                    PropertyId = id++,
                    Name = pair.Key,
                }
            );
    }

    private sealed class Writer
    {
        private readonly MainDocumentPart main;
        private readonly AuthoringTemplate template;
        private readonly CancellationToken cancellationToken;
        private readonly WordContentEmitter emitter;
        private readonly List<DocPart> glossary = new List<DocPart>();
        private readonly RunFormat plain = new RunFormat();
        private int id = 1000;

        internal Writer(
            MainDocumentPart main,
            AuthoringTemplate template,
            CancellationToken cancellationToken
        )
        {
            this.main = main;
            this.template = template;
            this.cancellationToken = cancellationToken;
            emitter = new WordContentEmitter(main);
        }

        internal OpenXmlElement Element(AuthoringElement element)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (element is AuthoringInstruction instruction)
                return Control(instruction.Tag, null, false, null, [Instruction(instruction.Text)]);

            var field = (AuthoringField)element;

            switch (field.Kind)
            {
                case FieldKind.Group:
                    return Control(
                        field.Tag,
                        field.Title,
                        field.Locked,
                        null,
                        field.Children.Select(Element).ToArray()
                    );
                case FieldKind.Table:
                    // Word 2019 drops enclosing controls when a row is inserted below a table that ends its control, so a
                    // paragraph always follows the table inside the control.
                    return Control(
                        field.Tag,
                        field.Title,
                        field.Locked,
                        null,
                        [Table(field), new Paragraph()]
                    );
            }

            var style = field.ParagraphStyle;
            Func<ParagraphProperties?> paragraph = () =>
                style == null
                    ? null
                    : new ParagraphProperties(new ParagraphStyleId { Val = style });
            string? placeholder = null;

            if (field.Prompt != null)
            {
                placeholder = Name(field.Tag);
                glossary.Add(DocPart(placeholder, Prompt(field.Prompt, paragraph())));
            }

            var content =
                field.Content != null
                    ? emitter.Blocks(field.Content, paragraph, plain).ToArray()
                    : new OpenXmlElement[] { Prompt(field.Prompt!, paragraph()) };

            return Control(
                field.Tag,
                field.Title,
                field.Locked,
                placeholder,
                content,
                field.Content == null
            );
        }

        internal void FinishGlossary()
        {
            if (glossary.Count == 0)
                return;

            main.AddNewPart<GlossaryDocumentPart>().GlossaryDocument = new GlossaryDocument(
                new DocParts(glossary)
            );
        }

        private SdtBlock Control(
            string tag,
            string? title,
            bool locked,
            string? placeholder,
            OpenXmlElement[] content,
            bool showingPlaceholder = false
        )
        {
            var properties = new SdtProperties();

            if (title != null)
                properties.AppendChild(new SdtAlias { Val = title });

            properties.AppendChild(new Tag { Val = tag });
            properties.AppendChild(new SdtId { Val = id++ });

            if (locked)
                properties.AppendChild(new Lock { Val = LockingValues.SdtLocked });

            if (placeholder != null)
                properties.AppendChild(
                    new SdtPlaceholder(new DocPartReference { Val = placeholder })
                );

            if (showingPlaceholder)
                properties.AppendChild(new ShowingPlaceholder());

            return new SdtBlock(properties, new SdtContentBlock(content));
        }

        private Paragraph Instruction(string text)
        {
            var properties =
                template.InstructionStyle == null
                    ? null
                    : new ParagraphProperties(
                        new ParagraphStyleId { Val = template.InstructionStyle }
                    );
            var format =
                template.InstructionStyle == null
                    ? new RunFormat(new RunProperties(new Italic(), new Color { Val = "7F7F7F" }))
                    : plain;
            return emitter.Paragraph([new TextInline(text)], properties, format);
        }

        private static Paragraph Prompt(string text, ParagraphProperties? properties)
        {
            var paragraph = new Paragraph();

            if (properties != null)
                paragraph.AppendChild(properties);

            paragraph.AppendChild(
                WordContentEmitter.Run(
                    new TextInline(text),
                    new RunFormat(
                        new RunProperties(new RunStyle { Val = WordStyles.PlaceholderStyle })
                    ),
                    false
                )
            );
            return paragraph;
        }

        private Table Table(AuthoringField field)
        {
            var columns = Math.Max(
                field.ColumnTitles.Count,
                field.Rows.Select(row => row.Count).DefaultIfEmpty(2).Max()
            );
            var width = 9000 / columns;
            var border = new Func<BorderType, BorderType>(item =>
            {
                item.Val = BorderValues.Single;
                item.Size = 4;
                item.Color = "A6A6A6";
                return item;
            });
            var table = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                    new TableBorders(
                        border(new TopBorder()),
                        border(new LeftBorder()),
                        border(new BottomBorder()),
                        border(new RightBorder()),
                        border(new InsideHorizontalBorder()),
                        border(new InsideVerticalBorder())
                    ),
                    new TableLook { Val = "0000" }
                ),
                new TableGrid(
                    Enumerable
                        .Range(0, columns)
                        .Select(_ => new GridColumn { Width = width.ToString() })
                )
            );

            TableCell Cell(IEnumerable<OpenXmlElement> inlines) =>
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth
                        {
                            Type = TableWidthUnitValues.Dxa,
                            Width = width.ToString(),
                        }
                    ),
                    new Paragraph(inlines)
                );

            if (field.ColumnTitles.Count != 0)
                table.AppendChild(
                    new TableRow(
                        new OpenXmlElement[] { new TableRowProperties(new TableHeader()) }.Concat(
                            field.ColumnTitles.Select(title =>
                                Cell([
                                    WordContentEmitter.Run(
                                        new TextInline(title, TextMarks.Bold),
                                        plain,
                                        false
                                    ),
                                ])
                            )
                        )
                    )
                );

            foreach (var row in field.Rows)
                table.AppendChild(
                    new TableRow(row.Select(cell => Cell(emitter.Inlines(cell, plain))))
                );

            for (var index = 0; index < field.EmptyRows; index++)
                table.AppendChild(new TableRow(Enumerable.Range(0, columns).Select(_ => Cell([]))));

            return table;
        }

        // Placeholder names and identifiers derive from tags so rebuilding a template yields the same references.
        private static string Name(string tag) => "Prompt_" + tag.Replace('.', '_');

        private static DocPart DocPart(string name, Paragraph prompt)
        {
            using (var md5 = MD5.Create())
            {
                var guid = new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(name)));
                return new DocPart(
                    new DocPartProperties(
                        new DocPartName { Val = name },
                        new Category(
                            new Name { Val = "General" },
                            new Gallery { Val = DocPartGalleryValues.Placeholder }
                        ),
                        new DocPartTypes(new DocPartType { Val = DocPartValues.SdtPlaceholder }),
                        new Behaviors(new Behavior { Val = DocPartBehaviorValues.Content }),
                        new DocPartId { Val = "{" + guid.ToString().ToUpperInvariant() + "}" }
                    ),
                    new DocPartBody(prompt)
                );
            }
        }
    }
}
