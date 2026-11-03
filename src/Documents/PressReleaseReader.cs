using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Documents;

/// <summary>Release is null whenever an error diagnostic exists; warnings never prevent a result.</summary>
public sealed class PressReleaseReadResult
{
    public PressRelease? Release { get; }
    public IReadOnlyList<DocumentDiagnostic> Diagnostics { get; }
    public bool Succeeded => Release != null;

    internal PressReleaseReadResult(
        PressRelease? release,
        IReadOnlyList<DocumentDiagnostic> diagnostics
    )
    {
        Release = release;
        Diagnostics = diagnostics;
    }
}

/// <summary>Reads and validates a press release authored from a template of the press-release/1 schema.</summary>
/// <remarks>Language and template version come from the document's custom properties, never from its filename. An
/// expected language is an assertion: a mismatch fails instead of overriding the document.</remarks>
public sealed class PressReleaseReader
{
    private readonly WordContentReader reader;

    public PressReleaseReader(WordContentReader? reader = null) =>
        this.reader = reader ?? new WordContentReader();

    public PressReleaseReadResult Read(
        FileSystemPath path,
        PressReleaseSchema schema,
        string? expectedLanguage = null,
        CancellationToken cancellationToken = default
    )
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        using (
            var stream = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read)
        )
            return Read(stream, schema, expectedLanguage, cancellationToken);
    }

    public PressReleaseReadResult Read(
        Stream source,
        PressReleaseSchema schema,
        string? expectedLanguage = null,
        CancellationToken cancellationToken = default
    )
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        var extraction = reader.Read(source, schema.Content, cancellationToken);
        var diagnostics = new DiagnosticList();
        diagnostics.AddRange(extraction.Diagnostics);

        if (
            extraction.Diagnostics.Any(item =>
                item.Code
                    is DocumentDiagnosticCode.InvalidPackage
                        or DocumentDiagnosticCode.MacroEnabledDocument
                        or DocumentDiagnosticCode.UnsupportedDocumentType
            )
        )
            return new PressReleaseReadResult(null, diagnostics.Items);

        // An empty expectation means none, so PowerShell callers can pass $null.
        var metadata = Metadata(
            extraction.Properties,
            schema,
            string.IsNullOrEmpty(expectedLanguage) ? null : expectedLanguage,
            diagnostics
        );

        if (diagnostics.HasErrors || metadata == null)
            return new PressReleaseReadResult(null, diagnostics.Items);

        var fields = extraction.Fields;
        var release = new PressRelease(
            metadata.Value.Language,
            metadata.Value.TemplateVersion,
            Text(fields, PressReleaseTags.Headline)!,
            Text(fields, PressReleaseTags.Dateline)!,
            Blocks(fields, PressReleaseTags.Body)!,
            Section(
                fields,
                PressReleaseTags.Contact,
                PressReleaseTags.ContactHeading,
                PressReleaseTags.ContactDetails
            )!,
            Specifications(fields),
            Section(
                fields,
                PressReleaseTags.FactBox,
                PressReleaseTags.FactBoxHeading,
                PressReleaseTags.FactBoxBody
            ),
            Blocks(fields, PressReleaseTags.Media),
            schema
                .CompanyDescriptionIds.Select(id =>
                    (id, blocks: Blocks(fields, PressReleaseTags.CompanyDescription(id)))
                )
                .Where(item => item.blocks != null)
                .Select(item => new CompanyDescription(item.id, item.blocks!)),
            Blocks(fields, PressReleaseTags.Editorial)
        );

        return new PressReleaseReadResult(release, diagnostics.Items);
    }

    private static (string Language, string TemplateVersion)? Metadata(
        IReadOnlyDictionary<string, string> properties,
        PressReleaseSchema schema,
        string? expectedLanguage,
        DiagnosticList diagnostics
    )
    {
        string? Property(string name)
        {
            if (properties.TryGetValue(name, out var value) && value.Trim().Length != 0)
                return value.Trim();

            diagnostics.Error(
                DocumentDiagnosticCode.MissingMetadata,
                null,
                null,
                "The document property " + name + " is missing.",
                "Create the release from the current press-release template instead of a blank or copied document."
            );
            return null;
        }

        var id = Property(PressReleaseTags.SchemaProperty);
        var language = Property(PressReleaseTags.LanguageProperty);
        var version = Property(PressReleaseTags.TemplateVersionProperty);

        if (id != null && id != PressReleaseTags.SchemaId)
            diagnostics.Error(
                DocumentDiagnosticCode.UnsupportedSchema,
                null,
                null,
                "The document uses schema \""
                    + id
                    + "\"; this generator supports "
                    + PressReleaseTags.SchemaId
                    + ".",
                "Use a generator and template pack that support this schema, or migrate the document deliberately."
            );

        if (language != null && !schema.Languages.Contains(language))
            diagnostics.Error(
                DocumentDiagnosticCode.UnsupportedSchema,
                null,
                null,
                "The document language \"" + language + "\" is not supported by the template pack.",
                "Use the authoring template for one of: "
                    + string.Join(", ", schema.Languages)
                    + "."
            );

        if (language != null && expectedLanguage != null && language != expectedLanguage)
            diagnostics.Error(
                DocumentDiagnosticCode.ConflictingMetadata,
                null,
                null,
                "The document declares language \""
                    + language
                    + "\", but \""
                    + expectedLanguage
                    + "\" was expected.",
                "Check that the right source file was selected; the document's own language is never overridden."
            );

        if (version != null && !schema.TemplateVersions.Contains(version))
            diagnostics.Error(
                DocumentDiagnosticCode.UnsupportedSchema,
                null,
                null,
                "Authoring template version \""
                    + version
                    + "\" is not supported by the template pack.",
                "Use a template pack compatible with this template version."
            );

        return language == null || version == null ? null : (language, version);
    }

    private static string? Text(IReadOnlyDictionary<string, FieldContent> fields, string tag) =>
        Find(fields, tag) is TextFieldContent text ? text.Text : null;

    private static IReadOnlyList<DocumentBlock>? Blocks(
        IReadOnlyDictionary<string, FieldContent> fields,
        string tag
    ) => Find(fields, tag) is RichFieldContent rich ? rich.Blocks.Select(Linkify).ToArray() : null;

    // Authors paste plain URLs; linking them here gives every writer the same clickable destinations.
    private static DocumentBlock Linkify(DocumentBlock block) =>
        block switch
        {
            ParagraphBlock paragraph => new ParagraphBlock(Inlines.Linkify(paragraph.Content)),
            ListBlock list => Linkify(list),
            _ => block,
        };

    private static ListBlock Linkify(ListBlock list) =>
        new ListBlock(
            list.Kind,
            list.Start,
            list.Items.Select(item => new ListItem(
                Inlines.Linkify(item.Content),
                item.Sublist == null ? null : Linkify(item.Sublist)
            ))
        );

    private static PressReleaseSection? Section(
        IReadOnlyDictionary<string, FieldContent> fields,
        string tag,
        string heading,
        string body
    ) =>
        fields.TryGetValue(tag, out var content) && content is GroupFieldContent group
            ? new PressReleaseSection(Text(group.Children, heading)!, Blocks(group.Children, body)!)
            : null;

    private static PressReleaseSpecifications? Specifications(
        IReadOnlyDictionary<string, FieldContent> fields
    ) =>
        fields.TryGetValue(PressReleaseTags.Specifications, out var content)
        && content is GroupFieldContent group
            ? new PressReleaseSpecifications(
                Text(group.Children, PressReleaseTags.SpecificationsHeading)!,
                (
                    (TableFieldContent)group.Children[PressReleaseTags.SpecificationsTable]
                ).Rows.Select(row => new SpecificationRow(
                    Inlines.Linkify(row[0]),
                    Inlines.Linkify(row[1])
                ))
            )
            : null;

    private static FieldContent? Find(
        IReadOnlyDictionary<string, FieldContent> fields,
        string tag
    ) => fields.TryGetValue(tag, out var content) ? content : null;
}
