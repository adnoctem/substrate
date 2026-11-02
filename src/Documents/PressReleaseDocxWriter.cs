using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AdNoctem.Substrate.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AdNoctem.Substrate.Documents;

/// <summary>Writes a validated press release into a tagged Word layout from a template pack.</summary>
/// <remarks>The base document supplies page setup, headers and styles. The writer fills the recognized content controls,
/// removes their wrappers, and rebuilds hyperlinks and numbering from the semantic model. A base document must be a
/// macro-free DOCX that contains exactly the controls the layout requires.</remarks>
public sealed class PressReleaseDocxWriter
{
    public void Write(
        PressRelease release,
        TemplatePack pack,
        Stream destination,
        CancellationToken cancellationToken = default
    )
    {
        if (release == null)
            throw new ArgumentNullException(nameof(release));

        if (pack == null)
            throw new ArgumentNullException(nameof(pack));

        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        cancellationToken.ThrowIfCancellationRequested();
        var language = TemplatePack.SelectLanguage(release, pack);
        byte[] output;

        using (var buffer = new MemoryStream())
        {
            buffer.Write(language.Docx, 0, language.Docx.Length);
            buffer.Position = 0;

            try
            {
                using (var document = WordprocessingDocument.Open(buffer, true))
                {
                    if (
                        document.DocumentType != WordprocessingDocumentType.Document
                        || document.MainDocumentPart?.VbaProjectPart != null
                    )
                        throw Invalid(
                            "The DOCX base must be a macro-free Word document, not a template or macro-enabled file."
                        );

                    var main =
                        document.MainDocumentPart
                        ?? throw Invalid("The DOCX base has no main document part.");
                    var body =
                        main.Document?.Body ?? throw Invalid("The DOCX base has no document body.");

                    Rebuild(body, main, release, pack, language);
                    document.Save();
                }
            }
            catch (OpenXmlPackageException ex)
            {
                throw new DocumentException(
                    DocumentFailureReason.InvalidTemplatePack,
                    "The DOCX base is not a valid Word package.",
                    ex
                );
            }

            output = buffer.ToArray();
        }

        cancellationToken.ThrowIfCancellationRequested();
        destination.Write(output, 0, output.Length);
    }

    public void Write(
        PressRelease release,
        TemplatePack pack,
        FileSystemPath destination,
        bool overwrite = false,
        CancellationToken cancellationToken = default
    )
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        using (var buffer = new MemoryStream())
        {
            Write(release, pack, buffer, cancellationToken);
            buffer.Position = 0;
            new FileManager().WriteAtomically(destination, buffer, overwrite, cancellationToken);
        }
    }

    private static void Rebuild(
        Body body,
        MainDocumentPart main,
        PressRelease release,
        TemplatePack pack,
        TemplateLanguage language
    )
    {
        var known = KnownTags(pack);
        var placeholders = new Dictionary<string, Placeholder>(StringComparer.Ordinal);

        foreach (var element in body.Descendants<SdtBlock>())
        {
            var tag =
                WordContentReader.Tag(element)
                ?? throw Invalid("Every content control in the DOCX base must have a tag.");

            if (!known.Contains(tag))
                throw Invalid("The DOCX base contains unknown content control \"" + tag + "\".");

            if (placeholders.ContainsKey(tag))
                throw Invalid("The DOCX base contains duplicate content control \"" + tag + "\".");

            placeholders.Add(tag, new Placeholder(element));
        }

        var headline = Require(placeholders, PressReleaseTags.Headline);
        var dateline = Require(placeholders, PressReleaseTags.Dateline);
        var bodyField = Require(placeholders, PressReleaseTags.Body);
        var contact = Require(placeholders, PressReleaseTags.Contact);
        var contactHeading = Require(placeholders, PressReleaseTags.ContactHeading);
        var contactDetails = Require(placeholders, PressReleaseTags.ContactDetails);

        Mapped(release.Specifications != null, PressReleaseSectionKind.Specifications, language);
        Mapped(release.FactBox != null, PressReleaseSectionKind.FactBox, language);
        Mapped(release.Media != null, PressReleaseSectionKind.Media, language);
        Mapped(release.EditorialNote != null, PressReleaseSectionKind.Editorial, language);
        Mapped(
            release.CompanyDescriptions.Count != 0,
            PressReleaseSectionKind.CompanyDescriptions,
            language
        );

        foreach (var description in release.CompanyDescriptions)
            if (!pack.Schema.CompanyDescriptionIds.Contains(description.Id, StringComparer.Ordinal))
                throw new DocumentException(
                    DocumentFailureReason.InvalidRelease,
                    "The template pack does not declare company description \""
                        + description.Id
                        + "\"."
                );

        var emitter = new WordContentEmitter(main);
        var bodyStyle = bodyField.ParagraphStyle;
        var bodyFormat = Format(bodyField);
        var separatorFormat = Format(bodyField);

        Replace(
            bodyField,
            Body(release, language, emitter, bodyStyle, bodyFormat, dateline, separatorFormat)
        );
        Replace(headline, Plain(emitter, release.Headline, headline, bold: false));
        dateline.Element.Remove();

        Replace(
            contactHeading,
            Plain(emitter, release.Contact.Heading, contactHeading, bold: true)
        );
        Replace(
            contactDetails,
            Rich(emitter, release.Contact.Blocks, contactDetails, Format(contactDetails))
        );

        if (release.Specifications is PressReleaseSpecifications specifications)
        {
            var group = Require(placeholders, PressReleaseTags.Specifications);
            var heading = Require(placeholders, PressReleaseTags.SpecificationsHeading);
            var table = Require(placeholders, PressReleaseTags.SpecificationsTable);

            Replace(heading, Plain(emitter, specifications.Heading, heading, bold: false));
            Replace(table, Rows(emitter, specifications, language, table, Format(table)));
            Unwrap(group);
        }
        else
            Remove(placeholders, PressReleaseTags.Specifications);

        if (release.FactBox is PressReleaseSection factBox)
        {
            var group = Require(placeholders, PressReleaseTags.FactBox);
            var heading = Require(placeholders, PressReleaseTags.FactBoxHeading);
            var content = Require(placeholders, PressReleaseTags.FactBoxBody);

            Replace(heading, Plain(emitter, factBox.Heading, heading, bold: false));
            Replace(content, Rich(emitter, factBox.Blocks, content, Format(content)));
            Unwrap(group);
        }
        else
            Remove(placeholders, PressReleaseTags.FactBox);

        if (release.Media is IReadOnlyList<DocumentBlock> media)
        {
            var field = Require(placeholders, PressReleaseTags.Media);
            Replace(field, Rich(emitter, media, field, Format(field)));
        }
        else
            Remove(placeholders, PressReleaseTags.Media);

        foreach (var id in pack.Schema.CompanyDescriptionIds)
        {
            var description = release.CompanyDescriptions.FirstOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal)
            );

            if (description == null)
            {
                Remove(placeholders, PressReleaseTags.CompanyDescription(id));
                continue;
            }

            var field = Require(placeholders, PressReleaseTags.CompanyDescription(id));
            Replace(field, Rich(emitter, description.Blocks, field, Format(field)));
        }

        if (release.EditorialNote is IReadOnlyList<DocumentBlock> editorial)
        {
            var field = Require(placeholders, PressReleaseTags.Editorial);
            Replace(field, Rich(emitter, editorial, field, Format(field)));
        }
        else
            Remove(placeholders, PressReleaseTags.Editorial);

        Unwrap(contact);

        foreach (var element in body.Descendants<SdtBlock>())
            throw Invalid(
                "The DOCX base contains an unfilled content control tagged \""
                    + WordContentReader.Tag(element)
                    + "\"."
            );

        if (
            body.Descendants<SdtRun>().Any()
            || body.Descendants<SdtCell>().Any()
            || body.Descendants<SdtRow>().Any()
        )
            throw Invalid("The DOCX base contains a content control that cannot be filled.");
    }

    private static HashSet<string> KnownTags(TemplatePack pack)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal)
        {
            PressReleaseTags.Headline,
            PressReleaseTags.Dateline,
            PressReleaseTags.Body,
            PressReleaseTags.Specifications,
            PressReleaseTags.SpecificationsHeading,
            PressReleaseTags.SpecificationsTable,
            PressReleaseTags.FactBox,
            PressReleaseTags.FactBoxHeading,
            PressReleaseTags.FactBoxBody,
            PressReleaseTags.Media,
            PressReleaseTags.Editorial,
            PressReleaseTags.Contact,
            PressReleaseTags.ContactHeading,
            PressReleaseTags.ContactDetails,
        };

        foreach (var id in pack.Schema.CompanyDescriptionIds)
            tags.Add(PressReleaseTags.CompanyDescription(id));

        return tags;
    }

    private static IEnumerable<OpenXmlElement> Body(
        PressRelease release,
        TemplateLanguage language,
        WordContentEmitter emitter,
        ParagraphProperties? style,
        RunFormat format,
        Placeholder dateline,
        RunFormat separatorFormat
    )
    {
        var output = new List<OpenXmlElement>();
        var first = true;
        var datelineFormat = DatelineFormat(dateline);

        foreach (var block in release.Body)
        {
            if (first)
            {
                first = false;

                if (block is ParagraphBlock paragraph)
                {
                    var target = new Paragraph();

                    if (style != null)
                        target.AppendChild(style.CloneNode(true));

                    target.AppendChild(
                        WordContentEmitter.Run(
                            new TextInline(release.Dateline),
                            datelineFormat,
                            false
                        )
                    );
                    target.AppendChild(
                        WordContentEmitter.Run(
                            new TextInline(language.DatelineSeparator),
                            separatorFormat,
                            false
                        )
                    );
                    target.Append(emitter.Inlines(paragraph.Content, format));
                    output.Add(target);
                    continue;
                }

                var prefix = new Paragraph();

                if (style != null)
                    prefix.AppendChild(style.CloneNode(true));

                prefix.AppendChild(
                    WordContentEmitter.Run(new TextInline(release.Dateline), datelineFormat, false)
                );
                prefix.AppendChild(
                    WordContentEmitter.Run(
                        new TextInline(language.DatelineSeparator),
                        separatorFormat,
                        false
                    )
                );
                output.Add(prefix);
            }

            output.AddRange(emitter.Blocks([block], () => Clone(style), format));
        }

        return output;
    }

    private static IEnumerable<OpenXmlElement> Rows(
        WordContentEmitter emitter,
        PressReleaseSpecifications specifications,
        TemplateLanguage language,
        Placeholder placeholder,
        RunFormat format
    )
    {
        var style = placeholder.ParagraphStyle;
        var bold = BoldFormat(placeholder);

        foreach (var row in specifications.Rows)
        {
            var paragraph = new Paragraph();

            if (style != null)
                paragraph.AppendChild(style.CloneNode(true));

            if (language.SpecificationBullet is string bullet)
            {
                var properties = new RunProperties();

                if (language.SpecificationBulletColor is string color)
                    properties.AppendChild(new Color { Val = color });

                var prefix = new Run();

                if (properties.HasChildren)
                    prefix.AppendChild(properties);

                prefix.Append(new Text(bullet) { Space = SpaceProcessingModeValues.Preserve });
                prefix.AppendChild(new TabChar());
                paragraph.AppendChild(prefix);
            }

            paragraph.Append(emitter.Inlines(row.Label, bold));
            paragraph.AppendChild(
                WordContentEmitter.Run(
                    new TextInline(language.SpecificationSeparator),
                    format,
                    false
                )
            );
            paragraph.Append(emitter.Inlines(row.Value, format));
            yield return paragraph;
        }
    }

    private static IEnumerable<OpenXmlElement> Plain(
        WordContentEmitter emitter,
        string text,
        Placeholder placeholder,
        bool bold
    ) =>
        [
            emitter.Paragraph(
                [new TextInline(text)],
                Clone(placeholder.ParagraphStyle),
                bold ? BoldFormat(placeholder) : Format(placeholder)
            ),
        ];

    private static IEnumerable<OpenXmlElement> Rich(
        WordContentEmitter emitter,
        IEnumerable<DocumentBlock> blocks,
        Placeholder placeholder,
        RunFormat format
    ) => emitter.Blocks(blocks, () => Clone(placeholder.ParagraphStyle), format);

    private static RunFormat Format(Placeholder placeholder)
    {
        var normal = Clone(placeholder.Run);

        if (normal != null)
        {
            normal.Bold = null;
            normal.Italic = null;
        }

        var strong = Clone(placeholder.Strong) ?? Clone(placeholder.Run) ?? new RunProperties();
        strong.Bold = new Bold();
        strong.Italic = null;
        var link = Clone(placeholder.Link) ?? normal;
        return new RunFormat(normal, strong, link);
    }

    private static RunFormat BoldFormat(Placeholder placeholder)
    {
        var bold = Clone(placeholder.Strong) ?? Clone(placeholder.Run) ?? new RunProperties();
        bold.Bold = new Bold();
        bold.Italic = null;
        return new RunFormat(bold, bold, bold);
    }

    private static RunFormat DatelineFormat(Placeholder placeholder)
    {
        var bold = Clone(placeholder.Run) ?? new RunProperties();
        bold.Bold = new Bold();
        bold.Italic = null;
        return new RunFormat(bold, bold, bold);
    }

    private static ParagraphProperties? Clone(ParagraphProperties? properties) =>
        properties == null ? null : (ParagraphProperties)properties.CloneNode(true);

    private static RunProperties? Clone(RunProperties? properties) =>
        properties == null ? null : (RunProperties)properties.CloneNode(true);

    private static void Replace(Placeholder placeholder, IEnumerable<OpenXmlElement> content)
    {
        if (placeholder.Element.Parent == null)
            return;

        var items = content.ToList();

        for (var index = items.Count - 1; index >= 0; index--)
            placeholder.Element.InsertAfterSelf(items[index]);

        placeholder.Element.Remove();
    }

    private static void Unwrap(Placeholder placeholder)
    {
        if (placeholder.Element.Parent == null)
            return;

        var content = placeholder.Element.GetFirstChild<SdtContentBlock>();

        if (content != null)
            foreach (
                var child in content.ChildElements.Reverse().Select(item => item.CloneNode(true))
            )
                placeholder.Element.InsertAfterSelf(child);

        placeholder.Element.Remove();
    }

    private static void Remove(IReadOnlyDictionary<string, Placeholder> placeholders, string tag)
    {
        if (placeholders.TryGetValue(tag, out var placeholder))
            placeholder.Element.Remove();
    }

    private static Placeholder Require(
        IReadOnlyDictionary<string, Placeholder> placeholders,
        string tag
    ) =>
        placeholders.TryGetValue(tag, out var placeholder)
            ? placeholder
            : throw Invalid("The DOCX base is missing content control \"" + tag + "\".");

    private static void Mapped(
        bool present,
        PressReleaseSectionKind kind,
        TemplateLanguage language
    )
    {
        if (present && !language.Sections.Contains(kind))
            throw new DocumentException(
                DocumentFailureReason.UnsupportedSection,
                "The template pack language \""
                    + language.Code
                    + "\" does not map the "
                    + kind
                    + " section."
            );
    }

    private static DocumentException Invalid(string message) =>
        new DocumentException(DocumentFailureReason.InvalidTemplatePack, message);

    private sealed class Placeholder
    {
        internal SdtBlock Element { get; }
        internal ParagraphProperties? ParagraphStyle { get; }
        internal RunProperties? Run { get; }
        internal RunProperties? Strong { get; }
        internal RunProperties? Link { get; }

        internal Placeholder(SdtBlock element)
        {
            Element = element;
            var paragraph = element.Descendants<Paragraph>().FirstOrDefault();
            ParagraphStyle = Clone(paragraph?.ParagraphProperties);
            var runs = paragraph?.Descendants<RunProperties>().ToArray() ?? [];
            Run = Clone(runs.FirstOrDefault());
            Strong = Clone(runs.FirstOrDefault(IsBold));
            Link = Clone(
                paragraph
                    ?.Descendants<Hyperlink>()
                    .SelectMany(item => item.Descendants<RunProperties>())
                    .FirstOrDefault()
            );
        }

        private static bool IsBold(RunProperties properties) =>
            properties.Bold != null && (properties.Bold.Val == null || properties.Bold.Val.Value);
    }
}
