using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdNoctem.Substrate.Documents;

/// <summary>Content-control tags and document properties of the press-release authoring schema, version 1.</summary>
public static class PressReleaseTags
{
    public const string SchemaId = "press-release/1";
    public const string SchemaProperty = "PressRelease.Schema";
    public const string LanguageProperty = "PressRelease.Language";
    public const string TemplateVersionProperty = "PressRelease.TemplateVersion";

    public const string Prefix = "pr.";
    public const string Instruction = "pr.note";
    public const string Headline = "pr.headline";
    public const string Dateline = "pr.dateline";
    public const string Body = "pr.body";
    public const string Specifications = "pr.specifications";
    public const string SpecificationsHeading = "pr.specifications.heading";
    public const string SpecificationsTable = "pr.specifications.table";
    public const string FactBox = "pr.factbox";
    public const string FactBoxHeading = "pr.factbox.heading";
    public const string FactBoxBody = "pr.factbox.body";
    public const string Media = "pr.media";
    public const string Editorial = "pr.editorial";
    public const string Contact = "pr.contact";
    public const string ContactHeading = "pr.contact.heading";
    public const string ContactDetails = "pr.contact.details";

    /// <summary>The tag of a company description block, such as pr.about.example for the identifier "example".</summary>
    public static string CompanyDescription(string id) =>
        "pr.about." + PressReleaseSchema.ValidateIdentifier(id, nameof(id));
}

/// <summary>The accepted variant of the press-release schema: languages, template versions and company description blocks.</summary>
/// <remarks>These values come from the selected template pack. The library defines no languages, organizations or wording.</remarks>
public sealed class PressReleaseSchema
{
    private static readonly Regex Identifier = new Regex(
        "^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant
    );

    public IReadOnlyList<string> Languages { get; }
    public IReadOnlyList<string> TemplateVersions { get; }
    public IReadOnlyList<string> CompanyDescriptionIds { get; }
    public ContentSchema Content { get; }

    /// <param name="languages">Accepted language codes, such as "de" or "en".</param>
    /// <param name="templateVersions">Accepted authoring-template versions; each document must declare one of them.</param>
    /// <param name="companyDescriptionIds">Optional company description blocks in publication order.</param>
    public PressReleaseSchema(
        IEnumerable<string> languages,
        IEnumerable<string> templateVersions,
        IEnumerable<string>? companyDescriptionIds = null
    )
    {
        Languages = Freeze(languages, nameof(languages), false);
        TemplateVersions = Freeze(templateVersions, nameof(templateVersions), true);
        CompanyDescriptionIds = Freeze(
            companyDescriptionIds ?? [],
            nameof(companyDescriptionIds),
            false,
            true
        );

        if (Languages.Count == 0 || TemplateVersions.Count == 0)
            throw new ArgumentException(
                "At least one language and one template version are required."
            );

        var fields = new List<FieldDefinition>
        {
            FieldDefinition.PlainText(PressReleaseTags.Headline, true),
            FieldDefinition.PlainText(PressReleaseTags.Dateline, true),
            FieldDefinition.RichText(PressReleaseTags.Body, true, true, true),
            FieldDefinition.Group(
                PressReleaseTags.Specifications,
                false,
                FieldDefinition.PlainText(PressReleaseTags.SpecificationsHeading, true),
                FieldDefinition.Table(PressReleaseTags.SpecificationsTable, 2, true)
            ),
            FieldDefinition.Group(
                PressReleaseTags.FactBox,
                false,
                FieldDefinition.PlainText(PressReleaseTags.FactBoxHeading, true),
                FieldDefinition.RichText(PressReleaseTags.FactBoxBody, true, true, true)
            ),
            FieldDefinition.RichText(PressReleaseTags.Media, false),
        };
        fields.AddRange(
            CompanyDescriptionIds.Select(id =>
                FieldDefinition.RichText(PressReleaseTags.CompanyDescription(id), false)
            )
        );
        fields.Add(FieldDefinition.RichText(PressReleaseTags.Editorial, false));
        fields.Add(
            FieldDefinition.Group(
                PressReleaseTags.Contact,
                true,
                FieldDefinition.PlainText(PressReleaseTags.ContactHeading, true),
                FieldDefinition.RichText(PressReleaseTags.ContactDetails, true)
            )
        );
        Content = new ContentSchema(
            PressReleaseTags.Prefix,
            fields,
            [PressReleaseTags.Instruction]
        );
    }

    internal static string ValidateIdentifier(string id, string parameter)
    {
        if (id == null || !Identifier.IsMatch(id))
            throw new ArgumentException(
                "Identifiers use lowercase letters, digits and single hyphens.",
                parameter
            );

        return id;
    }

    private static IReadOnlyList<string> Freeze(
        IEnumerable<string> values,
        string parameter,
        bool freeText,
        bool identifiers = false
    )
    {
        var items = (values ?? throw new ArgumentNullException(parameter)).ToArray();

        foreach (var item in items)
            if (identifiers)
                ValidateIdentifier(item, parameter);
            else if (
                string.IsNullOrWhiteSpace(item)
                || item.Trim() != item
                || (!freeText && !Identifier.IsMatch(item))
            )
                throw new ArgumentException("Invalid value: " + item, parameter);

        if (items.Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new ArgumentException("Values must be unique.", parameter);

        return Array.AsReadOnly(items);
    }
}

/// <summary>A heading and blocks, such as the fact box.</summary>
public sealed class PressReleaseSection
{
    public string Heading { get; }
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    public PressReleaseSection(string heading, IEnumerable<DocumentBlock> blocks)
    {
        Heading = RequireText(heading, nameof(heading));
        Blocks = PressRelease.RequireBlocks(blocks, nameof(blocks))!;
    }

    internal static string RequireText(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A nonempty value is required.", parameter)
            : value;
}

/// <summary>One technical specification. The label is published exactly as authored, including any colon.</summary>
public sealed class SpecificationRow
{
    public IReadOnlyList<DocumentInline> Label { get; }
    public IReadOnlyList<DocumentInline> Value { get; }

    public SpecificationRow(IEnumerable<DocumentInline> label, IEnumerable<DocumentInline> value)
    {
        Label = Inlines.Freeze(label, nameof(label));
        Value = Inlines.Freeze(value, nameof(value));

        if (Label.Count == 0 || Value.Count == 0)
            throw new ArgumentException("Label and value are required.");
    }
}

public sealed class PressReleaseSpecifications
{
    public string Heading { get; }
    public IReadOnlyList<SpecificationRow> Rows { get; }

    public PressReleaseSpecifications(string heading, IEnumerable<SpecificationRow> rows)
    {
        Heading = PressReleaseSection.RequireText(heading, nameof(heading));
        Rows = Array.AsReadOnly((rows ?? throw new ArgumentNullException(nameof(rows))).ToArray());

        if (Rows.Count == 0 || Rows.Any(row => row == null))
            throw new ArgumentException("At least one row is required.", nameof(rows));
    }
}

/// <summary>A company description block declared by the template pack, with the wording saved in the source.</summary>
public sealed class CompanyDescription
{
    public string Id { get; }
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    public CompanyDescription(string id, IEnumerable<DocumentBlock> blocks)
    {
        Id = PressReleaseSchema.ValidateIdentifier(id, nameof(id));
        Blocks = PressRelease.RequireBlocks(blocks, nameof(blocks))!;
    }
}

/// <summary>A validated press release, independent of any source document or output format.</summary>
/// <remarks>Sections are presented in the fixed order defined by the output layout, regardless of source order. Null
/// optional sections are absent and must be omitted, never filled from defaults.</remarks>
public sealed class PressRelease
{
    public string Language { get; }
    public string TemplateVersion { get; }
    public string Headline { get; }
    public string Dateline { get; }
    public IReadOnlyList<DocumentBlock> Body { get; }
    public PressReleaseSpecifications? Specifications { get; }
    public PressReleaseSection? FactBox { get; }
    public IReadOnlyList<DocumentBlock>? Media { get; }
    public IReadOnlyList<CompanyDescription> CompanyDescriptions { get; }
    public IReadOnlyList<DocumentBlock>? EditorialNote { get; }
    public PressReleaseSection Contact { get; }

    public PressRelease(
        string language,
        string templateVersion,
        string headline,
        string dateline,
        IEnumerable<DocumentBlock> body,
        PressReleaseSection contact,
        PressReleaseSpecifications? specifications = null,
        PressReleaseSection? factBox = null,
        IEnumerable<DocumentBlock>? media = null,
        IEnumerable<CompanyDescription>? companyDescriptions = null,
        IEnumerable<DocumentBlock>? editorialNote = null
    )
    {
        Language = PressReleaseSection.RequireText(language, nameof(language));
        TemplateVersion = PressReleaseSection.RequireText(templateVersion, nameof(templateVersion));
        Headline = PressReleaseSection.RequireText(headline, nameof(headline));
        Dateline = PressReleaseSection.RequireText(dateline, nameof(dateline));
        Body = RequireBlocks(body, nameof(body))!;
        Contact = contact ?? throw new ArgumentNullException(nameof(contact));
        Specifications = specifications;
        FactBox = factBox;
        Media = media == null ? null : RequireBlocks(media, nameof(media));
        CompanyDescriptions = Array.AsReadOnly((companyDescriptions ?? []).ToArray());
        EditorialNote =
            editorialNote == null ? null : RequireBlocks(editorialNote, nameof(editorialNote));

        if (CompanyDescriptions.Any(item => item == null))
            throw new ArgumentException("Null description.", nameof(companyDescriptions));

        if (
            CompanyDescriptions.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count()
            != CompanyDescriptions.Count
        )
            throw new ArgumentException(
                "Company descriptions must be unique.",
                nameof(companyDescriptions)
            );
    }

    internal static IReadOnlyList<DocumentBlock>? RequireBlocks(
        IEnumerable<DocumentBlock> blocks,
        string parameter
    )
    {
        var items = (blocks ?? throw new ArgumentNullException(parameter)).ToArray();

        if (items.Length == 0 || items.Any(item => item == null))
            throw new ArgumentException("At least one block is required.", parameter);

        return Array.AsReadOnly(items);
    }
}
