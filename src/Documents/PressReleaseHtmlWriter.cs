using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Documents;

/// <summary>Writes a validated press release as a standalone HTML document from a template pack.</summary>
/// <remarks>The writer selects the release language, rejects sections the pack does not map, and fills only the
/// template's validated slots. Authored text and link destinations are encoded for their output context; an authored
/// value is never treated as template markup.</remarks>
public sealed class PressReleaseHtmlWriter
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
        var values = Values(release, pack, language);
        var present = Present(release, language);
        var html = language.Html.Render(values, present);
        var bytes = new UTF8Encoding(false).GetBytes(html);
        cancellationToken.ThrowIfCancellationRequested();
        destination.Write(bytes, 0, bytes.Length);
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

    private static IReadOnlyDictionary<string, string> Values(
        PressRelease release,
        TemplatePack pack,
        TemplateLanguage language
    )
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["language"] = Encode(language.Code),
            ["title"] = Encode(release.Headline),
            ["masthead"] = Encode(language.Masthead),
            ["headline"] = Encode(release.Headline),
            ["body"] = Body(release, language.DatelineSeparator),
            ["contact.heading"] = Encode(release.Contact.Heading),
            ["contact.details"] = Blocks(release.Contact.Blocks),
        };

        if (release.Specifications != null)
        {
            values["specifications.heading"] = Encode(release.Specifications.Heading);
            values["specifications.rows"] = Specifications(
                release.Specifications,
                language.SpecificationSeparator
            );
        }

        if (release.FactBox != null)
        {
            values["factbox.heading"] = Encode(release.FactBox.Heading);
            values["factbox.body"] = Blocks(release.FactBox.Blocks);
        }

        if (release.Media != null)
            values["media"] = Blocks(release.Media);

        if (release.CompanyDescriptions.Count != 0)
            values["about"] = About(release, pack);

        if (release.EditorialNote != null)
            values["editorial"] = Blocks(release.EditorialNote);

        return values;
    }

    private static IReadOnlyCollection<PressReleaseSectionKind> Present(
        PressRelease release,
        TemplateLanguage language
    )
    {
        var present = new List<PressReleaseSectionKind>();

        Section(
            release.Specifications != null,
            PressReleaseSectionKind.Specifications,
            language,
            present
        );
        Section(release.FactBox != null, PressReleaseSectionKind.FactBox, language, present);
        Section(release.Media != null, PressReleaseSectionKind.Media, language, present);

        if (release.CompanyDescriptions.Count != 0)
        {
            Section(true, PressReleaseSectionKind.CompanyDescriptions, language, present);
        }

        Section(
            release.EditorialNote != null,
            PressReleaseSectionKind.Editorial,
            language,
            present
        );

        return present;
    }

    private static void Section(
        bool present,
        PressReleaseSectionKind kind,
        TemplateLanguage language,
        List<PressReleaseSectionKind> sections
    )
    {
        if (!present)
            return;

        if (!language.Sections.Contains(kind))
            throw new DocumentException(
                DocumentFailureReason.UnsupportedSection,
                "The template pack language \""
                    + language.Code
                    + "\" does not map the "
                    + kind
                    + " section."
            );

        sections.Add(kind);
    }

    private static string Body(PressRelease release, string datelineSeparator)
    {
        var output = new StringBuilder();
        var prefix =
            "<span class=\"dateline\"><strong>"
            + Encode(release.Dateline)
            + "</strong></span>"
            + Encode(datelineSeparator);
        var first = true;

        foreach (var block in release.Body)
        {
            if (first)
            {
                first = false;

                if (block is ParagraphBlock paragraph)
                {
                    Paragraph(output, paragraph, prefix);
                    continue;
                }

                output.Append("<p>").Append(prefix).Append("</p>");
            }

            Block(output, block);
        }

        return output.ToString();
    }

    private static string Blocks(IEnumerable<DocumentBlock> blocks)
    {
        var output = new StringBuilder();

        foreach (var block in blocks)
            Block(output, block);

        return output.ToString();
    }

    private static void Block(StringBuilder output, DocumentBlock block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                Paragraph(output, paragraph, null);
                break;
            case ListBlock list:
                List(output, list);
                break;
            default:
                throw new DocumentException(
                    DocumentFailureReason.InvalidRelease,
                    "Unsupported block type " + block.GetType().Name + "."
                );
        }
    }

    private static void Paragraph(StringBuilder output, ParagraphBlock paragraph, string? prefix)
    {
        output.Append("<p>");

        if (prefix != null)
            output.Append(prefix);

        Inlines(output, paragraph.Content);
        output.Append("</p>");
    }

    private static void List(StringBuilder output, ListBlock list)
    {
        var element = list.Kind == ListKind.Bullet ? "ul" : "ol";
        output.Append('<').Append(element);

        if (list.Kind == ListKind.Decimal && list.Start != 1)
            output
                .Append(" start=\"")
                .Append(list.Start.ToString(CultureInfo.InvariantCulture))
                .Append('"');

        output.Append('>');

        foreach (var item in list.Items)
        {
            output.Append("<li>");
            Inlines(output, item.Content);

            if (item.Sublist != null)
                List(output, item.Sublist);

            output.Append("</li>");
        }

        output.Append("</").Append(element).Append('>');
    }

    private static void Inlines(StringBuilder output, IEnumerable<DocumentInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    var bold = (text.Marks & TextMarks.Bold) != 0;
                    var italic = (text.Marks & TextMarks.Italic) != 0;

                    if (bold)
                        output.Append("<strong>");

                    if (italic)
                        output.Append("<em>");

                    output.Append(Encode(text.Text));

                    if (italic)
                        output.Append("</em>");

                    if (bold)
                        output.Append("</strong>");

                    break;
                case LineBreakInline:
                    output.Append("<br>");
                    break;
                case HyperlinkInline link:
                    output
                        .Append("<a href=\"")
                        .Append(Encode(link.Destination.AbsoluteUri))
                        .Append("\">");
                    Inlines(output, link.Content);
                    output.Append("</a>");
                    break;
                default:
                    throw new DocumentException(
                        DocumentFailureReason.InvalidRelease,
                        "Unsupported inline type " + inline.GetType().Name + "."
                    );
            }
        }
    }

    private static string Specifications(
        PressReleaseSpecifications specifications,
        string separator
    )
    {
        var output = new StringBuilder();
        var gap = Encode(separator);

        foreach (var row in specifications.Rows)
        {
            output.Append("<li><strong>");
            Inlines(output, row.Label);
            output.Append("</strong>").Append(gap);
            Inlines(output, row.Value);
            output.Append("</li>");
        }

        return output.ToString();
    }

    private static string About(PressRelease release, TemplatePack pack)
    {
        foreach (var description in release.CompanyDescriptions)
            if (!pack.Schema.CompanyDescriptionIds.Contains(description.Id, StringComparer.Ordinal))
                throw new DocumentException(
                    DocumentFailureReason.InvalidRelease,
                    "The template pack does not declare company description \""
                        + description.Id
                        + "\"."
                );

        var output = new StringBuilder();

        foreach (var id in pack.Schema.CompanyDescriptionIds)
        {
            var description = release.CompanyDescriptions.FirstOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal)
            );

            if (description != null)
                output.Append(Blocks(description.Blocks));
        }

        return output.ToString();
    }

    private static string Encode(string value)
    {
        var output = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            switch (character)
            {
                case '&':
                    output.Append("&amp;");
                    break;
                case '<':
                    output.Append("&lt;");
                    break;
                case '>':
                    output.Append("&gt;");
                    break;
                case '"':
                    output.Append("&quot;");
                    break;
                case '\'':
                    output.Append("&#39;");
                    break;
                default:
                    if (
                        (
                            character < ' '
                            && character != '\t'
                            && character != '\n'
                            && character != '\r'
                        )
                        || character == '\u007f'
                    )
                        output
                            .Append("&#x")
                            .Append(((int)character).ToString("X", CultureInfo.InvariantCulture))
                            .Append(';');
                    else
                        output.Append(character);
                    break;
            }
        }

        return output.ToString();
    }
}
