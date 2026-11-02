using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AdNoctem.Substrate.Documents;

// Formatting applied to emitted runs. Marks add bold or italic on top of the base, so presentation stays with the target
// document while authored emphasis survives. Null properties mean the paragraph's style alone applies.
internal sealed class RunFormat
{
    internal RunProperties? Normal { get; }
    internal RunProperties? Strong { get; }
    internal RunProperties? Link { get; }

    internal RunFormat(
        RunProperties? normal = null,
        RunProperties? strong = null,
        RunProperties? link = null
    )
    {
        Normal = normal;
        Strong = strong;
        Link = link;
    }

    internal RunProperties? For(TextMarks marks, bool link)
    {
        var bold = (marks & TextMarks.Bold) != 0;
        var source =
            link && Link != null ? Link
            : bold && Strong != null ? Strong
            : Normal;
        var properties = (RunProperties?)source?.CloneNode(true) ?? new RunProperties();

        // Typed setters keep the schema order of run properties.
        if (bold)
            properties.Bold = new Bold();

        if ((marks & TextMarks.Italic) != 0)
            properties.Italic = new Italic();

        return properties.HasChildren ? properties : null;
    }
}

// Writes the semantic model as WordprocessingML into one main document part. Links receive new relationships and every
// list receives a new numbering instance, so identifiers never come from a source document.
internal sealed class WordContentEmitter
{
    private readonly MainDocumentPart main;
    private int? bulletDefinition;
    private int? decimalDefinition;

    private int relationships;

    internal WordContentEmitter(MainDocumentPart main) => this.main = main;

    // Sequential identifiers keep rebuilt packages comparable; existing identifiers in the target part are skipped.
    private string NextRelationshipId()
    {
        string id;

        do id =
            "rIdLink"
            + (++relationships).ToString(System.Globalization.CultureInfo.InvariantCulture);
        while (
            main.Parts.Any(part => part.RelationshipId == id)
            || main.HyperlinkRelationships.Any(item => item.Id == id)
            || main.ExternalRelationships.Any(item => item.Id == id)
        );

        return id;
    }

    internal IEnumerable<OpenXmlElement> Blocks(
        IEnumerable<DocumentBlock> blocks,
        Func<ParagraphProperties?> paragraph,
        RunFormat format,
        Func<int, ParagraphProperties?>? listParagraph = null
    )
    {
        foreach (var block in blocks)
            switch (block)
            {
                case ParagraphBlock text:
                    yield return Paragraph(text.Content, paragraph(), format);
                    break;
                case ListBlock list:
                    foreach (var item in List(list, 0, listParagraph ?? (_ => paragraph()), format))
                        yield return item;

                    break;
            }
    }

    internal Paragraph Paragraph(
        IEnumerable<DocumentInline> content,
        ParagraphProperties? properties,
        RunFormat format
    )
    {
        var paragraph = new Paragraph();

        if (properties != null)
            paragraph.AppendChild(properties);

        paragraph.Append(Inlines(content, format));
        return paragraph;
    }

    internal IEnumerable<OpenXmlElement> Inlines(
        IEnumerable<DocumentInline> content,
        RunFormat format
    )
    {
        foreach (var inline in content)
            switch (inline)
            {
                case HyperlinkInline link:
                    var relationship = main.AddHyperlinkRelationship(
                        new Uri(link.Destination.OriginalString, UriKind.Absolute),
                        true,
                        NextRelationshipId()
                    );
                    yield return new Hyperlink(link.Content.Select(item => Run(item, format, true)))
                    {
                        Id = relationship.Id,
                        History = true,
                    };
                    break;
                default:
                    yield return Run(inline, format, false);
                    break;
            }
    }

    internal static Run Run(DocumentInline inline, RunFormat format, bool link)
    {
        var run = new Run();

        if (inline is TextInline text)
        {
            if (format.For(text.Marks, link) is RunProperties properties)
                run.AppendChild(properties);

            run.Append(TextElements(text.Text));
        }
        else
        {
            if (format.For(TextMarks.None, link) is RunProperties properties)
                run.AppendChild(properties);

            run.AppendChild(new Break());
        }

        return run;
    }

    // Tabs and no-break hyphens have dedicated elements; all other characters, including NBSP, remain literal text.
    internal static IEnumerable<OpenXmlElement> TextElements(string text)
    {
        var start = 0;

        for (var index = 0; index <= text.Length; index++)
        {
            if (index < text.Length && text[index] != '\t' && text[index] != '‑')
                continue;

            if (index > start)
                yield return new Text(text.Substring(start, index - start))
                {
                    Space = SpaceProcessingModeValues.Preserve,
                };

            if (index < text.Length)
                yield return text[index] == '\t' ? new TabChar() : new NoBreakHyphen();

            start = index + 1;
        }
    }

    private IEnumerable<Paragraph> List(
        ListBlock list,
        int level,
        Func<int, ParagraphProperties?> paragraph,
        RunFormat format
    )
    {
        var instance = Instance(list);

        foreach (var item in list.Items)
        {
            var properties = paragraph(level) ?? new ParagraphProperties();
            properties.NumberingProperties = new NumberingProperties(
                new NumberingLevelReference { Val = level },
                new NumberingId { Val = instance }
            );

            yield return Paragraph(item.Content, properties, format);

            if (item.Sublist != null)
                foreach (var nested in List(item.Sublist, level + 1, paragraph, format))
                    yield return nested;
        }
    }

    private int Instance(ListBlock list)
    {
        var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
        part.Numbering ??= new Numbering();
        var numbering = part.Numbering;
        var bullet = list.Kind == ListKind.Bullet;
        var definition = bullet ? bulletDefinition : decimalDefinition;

        if (definition == null)
        {
            definition =
                numbering
                    .Elements<AbstractNum>()
                    .Select(item => item.AbstractNumberId?.Value ?? 0)
                    .DefaultIfEmpty(-1)
                    .Max() + 1;
            var levels = Enumerable
                .Range(0, 2)
                .Select(index => new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat
                    {
                        Val = bullet ? NumberFormatValues.Bullet : NumberFormatValues.Decimal,
                    },
                    new LevelText
                    {
                        Val = bullet ? (index == 0 ? "•" : "–") : "%" + (index + 1) + ".",
                    },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(
                        new Indentation { Left = (360 * (index + 1)).ToString(), Hanging = "360" }
                    )
                )
                {
                    LevelIndex = index,
                });
            var abstractNum = new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }
            )
            {
                AbstractNumberId = definition,
            };
            abstractNum.Append(levels);
            var last = numbering.Elements<AbstractNum>().LastOrDefault();

            if (last != null)
                last.InsertAfterSelf(abstractNum);
            else
                numbering.PrependChild(abstractNum);

            if (bullet)
                bulletDefinition = definition;
            else
                decimalDefinition = definition;
        }

        var id =
            numbering
                .Elements<NumberingInstance>()
                .Select(item => item.NumberID?.Value ?? 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
        var instance = new NumberingInstance(new AbstractNumId { Val = definition })
        {
            NumberID = id,
        };

        // Each list restarts at its own first number, independent of earlier lists sharing the definition.
        instance.AppendChild(
            new LevelOverride(new StartOverrideNumberingValue { Val = list.Start })
            {
                LevelIndex = 0,
            }
        );
        numbering.AppendChild(instance);
        return id;
    }
}
