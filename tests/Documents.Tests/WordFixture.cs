using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AdNoctem.Substrate.Documents.Tests;

// Synthetic WordprocessingML fixtures. Each element is explicit so tests show the construct under examination.
internal sealed class WordFixture
{
    internal static readonly PressReleaseSchema Schema = new PressReleaseSchema(
        ["de", "en"],
        ["1.0"],
        ["example", "partner"]
    );

    internal const int Bullets = 1;
    internal const int Numbers = 2;
    internal const int RestartedNumbers = 3;
    internal const int Roman = 4;
    internal const int StyledBullets = 5;

    private readonly MainDocumentPart main;
    private readonly Body body;
    private int ids = 100;

    private WordFixture(MainDocumentPart main)
    {
        this.main = main;
        body = main.Document!.Body!;
    }

    internal static Dictionary<string, string> Metadata(string language = "en") =>
        new Dictionary<string, string>
        {
            [PressReleaseTags.SchemaProperty] = PressReleaseTags.SchemaId,
            [PressReleaseTags.LanguageProperty] = language,
            [PressReleaseTags.TemplateVersionProperty] = "1.0",
        };

    internal static byte[] Create(
        Action<WordFixture> build,
        IDictionary<string, string>? properties = null,
        WordprocessingDocumentType type = WordprocessingDocumentType.Document
    )
    {
        using (var stream = new MemoryStream())
        {
            using (var document = WordprocessingDocument.Create(stream, type))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Document(new Body());
                AddStyles(main);
                AddNumbering(main);
                var props = properties ?? Metadata();

                if (props.Count != 0)
                {
                    var part = document.AddCustomFilePropertiesPart();
                    part.Properties = new Properties();
                    var id = 2;

                    foreach (var pair in props)
                        part.Properties.AppendChild(
                            new CustomDocumentProperty(new VTLPWSTR(pair.Value))
                            {
                                FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
                                PropertyId = id++,
                                Name = pair.Key,
                            }
                        );
                }

                build(new WordFixture(main));
                main.Document.Body!.AppendChild(new SectionProperties());
            }

            return stream.ToArray();
        }
    }

    /// <summary>A complete valid release; omitted tags are left out, replaced tags use the supplied element.</summary>
    internal static byte[] Release(
        Action<WordFixture>? extra = null,
        IDictionary<string, string>? properties = null,
        string[]? omit = null,
        Dictionary<string, Func<WordFixture, OpenXmlElement>>? replace = null
    ) =>
        Create(
            fixture =>
            {
                var skip = new HashSet<string>(omit ?? []);
                void Add(string tag, Func<OpenXmlElement> create)
                {
                    if (skip.Contains(tag))
                        return;

                    fixture.Add(
                        replace != null && replace.TryGetValue(tag, out var custom)
                            ? custom(fixture)
                            : create()
                    );
                }

                fixture.Add(
                    Paragraph(Run("Logo row is decoration")).Let(p => p.RemoveAllChildren())
                );
                Add(
                    PressReleaseTags.Headline,
                    () =>
                        fixture.Block(
                            PressReleaseTags.Headline,
                            Paragraph(Run("Synthetic Headline"))
                        )
                );
                Add(
                    PressReleaseTags.Dateline,
                    () =>
                        fixture.Block(
                            PressReleaseTags.Dateline,
                            Paragraph(Run("Sampletown, November 2026"))
                        )
                );
                Add(
                    PressReleaseTags.Body,
                    () =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Paragraph(
                                Run("First paragraph with "),
                                Run("bold", bold: true),
                                Run(" text.")
                            ),
                            Paragraph(Run("Second paragraph."))
                        )
                );
                Add(
                    PressReleaseTags.Specifications,
                    () =>
                        fixture.Block(
                            PressReleaseTags.Specifications,
                            fixture.Block(
                                PressReleaseTags.SpecificationsHeading,
                                Paragraph(Run("Technical data"))
                            ),
                            fixture.Block(
                                PressReleaseTags.SpecificationsTable,
                                Table(new[] { "Size:", "275/70 R18" }, new[] { "Weight:", "20 kg" })
                            )
                        )
                );
                Add(
                    PressReleaseTags.FactBox,
                    () =>
                        fixture.Block(
                            PressReleaseTags.FactBox,
                            fixture.Block(
                                PressReleaseTags.FactBoxHeading,
                                Paragraph(Run("Fact box"))
                            ),
                            fixture.Block(PressReleaseTags.FactBoxBody, Paragraph(Run("Fact.")))
                        )
                );
                Add(
                    PressReleaseTags.Media,
                    () =>
                        fixture.Block(
                            PressReleaseTags.Media,
                            Paragraph(
                                Run("Video:", bold: true),
                                new Run(new Break()),
                                fixture.Link(
                                    "https://video.example/watch?v=abc",
                                    Run("https://video.example/watch?v=abc")
                                )
                            )
                        )
                );
                Add(
                    PressReleaseTags.CompanyDescription("example"),
                    () =>
                        fixture.Block(
                            PressReleaseTags.CompanyDescription("example"),
                            Paragraph(Run("About Example:", bold: true), Run(" Example text."))
                        )
                );
                Add(
                    PressReleaseTags.Editorial,
                    () =>
                        fixture.Block(
                            PressReleaseTags.Editorial,
                            Paragraph(Run("Free for editorial use."))
                        )
                );
                Add(
                    PressReleaseTags.Contact,
                    () =>
                        fixture.Block(
                            PressReleaseTags.Contact,
                            fixture.Block(
                                PressReleaseTags.ContactHeading,
                                Paragraph(Run("Media contact:"))
                            ),
                            fixture.Block(
                                PressReleaseTags.ContactDetails,
                                Paragraph(
                                    Run("Alex Example", bold: true),
                                    Run(", Press Office"),
                                    new Run(new Break()),
                                    fixture.Link(
                                        "mailto:press@example.com",
                                        Run("press@example.com")
                                    )
                                )
                            )
                        )
                );
                extra?.Invoke(fixture);
            },
            properties
        );

    internal void Add(params OpenXmlElement[] elements)
    {
        foreach (var element in elements)
            body.AppendChild(element);
    }

    internal SdtBlock Block(string? tag, params OpenXmlElement[] content)
    {
        var properties = new SdtProperties(new SdtId { Val = ids++ });

        if (tag != null)
            properties.AppendChild(new Tag { Val = tag });

        return new SdtBlock(properties, new SdtContentBlock(content));
    }

    internal SdtBlock PlaceholderBlock(string tag, string text) =>
        new SdtBlock(
            new SdtProperties(
                new SdtId { Val = ids++ },
                new Tag { Val = tag },
                new ShowingPlaceholder()
            ),
            new SdtContentBlock(
                Paragraph(
                    new Run(
                        new RunProperties(new RunStyle { Val = "PlaceholderText" }),
                        new Text(text)
                    )
                )
            )
        );

    internal SdtRun Inline(string? tag, params OpenXmlElement[] content)
    {
        var properties = new SdtProperties(new SdtId { Val = ids++ });

        if (tag != null)
            properties.AppendChild(new Tag { Val = tag });

        return new SdtRun(properties, new SdtContentRun(content));
    }

    internal Hyperlink Link(string destination, params OpenXmlElement[] content)
    {
        var relationship = main.AddHyperlinkRelationship(
            new Uri(destination, UriKind.RelativeOrAbsolute),
            true
        );
        return new Hyperlink(content) { Id = relationship.Id };
    }

    internal Hyperlink BrokenLink(params OpenXmlElement[] content) =>
        new Hyperlink(content) { Id = "rIdMissing" };

    internal void Comment(Paragraph anchor, string text)
    {
        var part = main.WordprocessingCommentsPart ?? main.AddNewPart<WordprocessingCommentsPart>();
        part.Comments ??= new Comments();
        part.Comments.AppendChild(
            new Comment(Paragraph(Run(text)))
            {
                Id = "1",
                Author = "Reviewer Name",
                Initials = "RN",
            }
        );
        anchor.PrependChild(new CommentRangeStart { Id = "1" });
        anchor.AppendChild(new CommentRangeEnd { Id = "1" });
        anchor.AppendChild(new Run(new CommentReference { Id = "1" }));
    }

    internal static Paragraph Paragraph(params OpenXmlElement[] content) => new Paragraph(content);

    internal static Paragraph Styled(string style, params OpenXmlElement[] content) =>
        new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = style })).Let(p =>
            p.Append(content)
        );

    internal static Paragraph Item(int numbering, int level, params OpenXmlElement[] content) =>
        new Paragraph(
            new ParagraphProperties(
                new NumberingProperties(
                    new NumberingLevelReference { Val = level },
                    new NumberingId { Val = numbering }
                )
            )
        ).Let(p => p.Append(content));

    internal static Run Run(
        string text,
        bool? bold = null,
        bool? italic = null,
        string? style = null
    )
    {
        var properties = new RunProperties();

        if (style != null)
            properties.AppendChild(new RunStyle { Val = style });

        if (bold != null)
            properties.AppendChild(new Bold { Val = OnOffValue.FromBoolean(bold.Value) });

        if (italic != null)
            properties.AppendChild(new Italic { Val = OnOffValue.FromBoolean(italic.Value) });

        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        if (properties.HasChildren)
            run.PrependChild(properties);

        return run;
    }

    internal static Table Table(params string[][] rows) =>
        new Table(
            new TableProperties(new TableWidth { Type = TableWidthUnitValues.Auto, Width = "0" }),
            new TableGrid(rows[0].Select(_ => new GridColumn { Width = "2000" }))
        ).Let(table =>
            table.Append(
                rows.Select(row => new TableRow(
                    row.Select(cell => Cell(Paragraph(cell.Length == 0 ? new Run() : Run(cell))))
                ))
            )
        );

    internal static TableCell Cell(params OpenXmlElement[] content) =>
        new TableCell(
            new TableCellProperties(
                new TableCellWidth { Width = "2000", Type = TableWidthUnitValues.Dxa }
            )
        ).Let(cell => cell.Append(content));

    private static void AddStyles(MainDocumentPart main)
    {
        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(new FontSize { Val = "22" }))
            ),
            new Style(new StyleName { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Normal",
                Default = true,
            },
            new Style(
                new StyleName { Val = "Bold Paragraph" },
                new BasedOn { Val = "Normal" },
                new StyleRunProperties(new Bold())
            )
            {
                Type = StyleValues.Paragraph,
                StyleId = "BoldParagraph",
            },
            new Style(
                new StyleName { Val = "Derived Plain" },
                new BasedOn { Val = "BoldParagraph" },
                new StyleRunProperties(new Bold { Val = false })
            )
            {
                Type = StyleValues.Paragraph,
                StyleId = "DerivedPlain",
            },
            new Style(new StyleName { Val = "Strong" }, new StyleRunProperties(new Bold()))
            {
                Type = StyleValues.Character,
                StyleId = "Strong",
            },
            new Style(new StyleName { Val = "Emphasis" }, new StyleRunProperties(new Italic()))
            {
                Type = StyleValues.Character,
                StyleId = "Emphasis",
            },
            new Style(
                new StyleName { Val = "Placeholder Text" },
                new StyleRunProperties(new Color { Val = "808080" })
            )
            {
                Type = StyleValues.Character,
                StyleId = "PlaceholderText",
            },
            new Style(
                new StyleName { Val = "List Bullet" },
                new BasedOn { Val = "Normal" },
                new StyleParagraphProperties(
                    new NumberingProperties(new NumberingId { Val = StyledBullets })
                )
            )
            {
                Type = StyleValues.Paragraph,
                StyleId = "ListBullet",
            }
        );
    }

    private static void AddNumbering(MainDocumentPart main)
    {
        Level Level(int index, NumberFormatValues format, int start = 1) =>
            new Level(
                new StartNumberingValue { Val = start },
                new NumberingFormat { Val = format },
                new LevelText { Val = "%1." }
            )
            {
                LevelIndex = index,
            };

        var part = main.AddNewPart<NumberingDefinitionsPart>();
        part.Numbering = new Numbering(
            new AbstractNum(
                Level(0, NumberFormatValues.Bullet),
                Level(1, NumberFormatValues.Bullet),
                Level(2, NumberFormatValues.Bullet)
            )
            {
                AbstractNumberId = 1,
            },
            new AbstractNum(
                Level(0, NumberFormatValues.Decimal, 5),
                Level(1, NumberFormatValues.Decimal)
            )
            {
                AbstractNumberId = 2,
            },
            new AbstractNum(Level(0, NumberFormatValues.LowerRoman)) { AbstractNumberId = 3 },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = Bullets },
            new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = Numbers },
            new NumberingInstance(
                new AbstractNumId { Val = 2 },
                new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 }
            )
            {
                NumberID = RestartedNumbers,
            },
            new NumberingInstance(new AbstractNumId { Val = 3 }) { NumberID = Roman },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = StyledBullets }
        );
    }
}

internal static class FixtureExtensions
{
    internal static T Let<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
