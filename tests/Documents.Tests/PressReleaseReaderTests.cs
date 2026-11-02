using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using static AdNoctem.Substrate.Documents.Tests.WordFixture;

namespace AdNoctem.Substrate.Documents.Tests;

public sealed class PressReleaseReaderTests
{
    private static PressReleaseReadResult Read(byte[] document, string? expectedLanguage = null)
    {
        using (var stream = new MemoryStream(document, false))
            return new PressReleaseReader().Read(stream, Schema, expectedLanguage);
    }

    private static PressRelease Valid(byte[] document)
    {
        var result = Read(document);
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Release!;
    }

    private static DocumentDiagnostic Single(
        byte[] document,
        DocumentDiagnosticCode code,
        string? field = null
    )
    {
        var result = Read(document);
        Assert.False(result.Succeeded);
        Assert.Null(result.Release);
        var matches = result
            .Diagnostics.Where(item => item.Code == code && (field == null || item.Field == field))
            .ToArray();
        Assert.True(
            matches.Length != 0,
            "Expected " + code + " but found: " + string.Join("; ", result.Diagnostics)
        );
        return matches[0];
    }

    private static string Plain(IEnumerable<DocumentInline> content) =>
        Inlines.ToPlainText(content);

    [Fact]
    public void CompleteReleaseMapsEverySectionInLayoutOrder()
    {
        var release = Valid(Release());

        Assert.Equal("en", release.Language);
        Assert.Equal("1.0", release.TemplateVersion);
        Assert.Equal("Synthetic Headline", release.Headline);
        Assert.Equal("Sampletown, November 2026", release.Dateline);
        Assert.Equal(2, release.Body.Count);
        var first = Assert.IsType<ParagraphBlock>(release.Body[0]);
        Assert.Collection(
            first.Content,
            item => Assert.Equal("First paragraph with ", Assert.IsType<TextInline>(item).Text),
            item => Assert.Equal(TextMarks.Bold, Assert.IsType<TextInline>(item).Marks),
            item => Assert.Equal(" text.", Assert.IsType<TextInline>(item).Text)
        );
        Assert.Equal("Technical data", release.Specifications!.Heading);
        Assert.Equal(
            new[] { "Size:", "Weight:" },
            release.Specifications.Rows.Select(row => Plain(row.Label))
        );
        Assert.Equal(
            new[] { "275/70 R18", "20 kg" },
            release.Specifications.Rows.Select(row => Plain(row.Value))
        );
        Assert.Equal("Fact box", release.FactBox!.Heading);
        var media = Assert.IsType<ParagraphBlock>(Assert.Single(release.Media!));
        Assert.IsType<LineBreakInline>(media.Content[1]);
        Assert.Equal(
            "https://video.example/watch?v=abc",
            Assert.IsType<HyperlinkInline>(media.Content[2]).Destination.OriginalString
        );
        Assert.Equal("example", Assert.Single(release.CompanyDescriptions).Id);
        Assert.NotNull(release.EditorialNote);
        Assert.Equal("Media contact:", release.Contact.Heading);
        var contact = Assert.IsType<ParagraphBlock>(Assert.Single(release.Contact.Blocks));
        Assert.Equal(
            "mailto:press@example.com",
            contact.Content.OfType<HyperlinkInline>().Single().Destination.OriginalString
        );
    }

    [Fact]
    public void TextIsPreservedLiterallyAcrossFragmentedRuns()
    {
        const string Sample =
            "Größe: 37″ – „BEAST“ bei 20 °C\u00A0km/h <b>&amp;</b> {{pr:body}} 'quoted'";
        var release = Valid(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Paragraph(
                                Sample
                                    .Select(character => (OpenXmlElement)Run(character.ToString()))
                                    .ToArray()
                            ),
                            Paragraph(
                                Run("Line one"),
                                new Run(new Break()),
                                Run("line two"),
                                new Run(new TabChar(), new NoBreakHyphen())
                            )
                        ),
                }
            )
        );

        var paragraph = Assert.IsType<ParagraphBlock>(release.Body[0]);
        Assert.Equal(Sample, Assert.IsType<TextInline>(Assert.Single(paragraph.Content)).Text);
        var second = Assert.IsType<ParagraphBlock>(release.Body[1]);
        Assert.Equal("Line one\nline two\t\u2011", Plain(second.Content));
        Assert.IsType<LineBreakInline>(second.Content[1]);
    }

    [Fact]
    public void RequiredFieldsFailWhenMissingDuplicatedEmptyOrShowingPlaceholders()
    {
        Single(
            Release(omit: [PressReleaseTags.Headline]),
            DocumentDiagnosticCode.MissingRequiredField,
            PressReleaseTags.Headline
        );
        Single(
            Release(extra: fixture =>
                fixture.Add(fixture.Block(PressReleaseTags.Dateline, Paragraph(Run("Elsewhere"))))
            ),
            DocumentDiagnosticCode.DuplicateField,
            PressReleaseTags.Dateline
        );
        Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Headline] = fixture =>
                        fixture.Block(PressReleaseTags.Headline, Paragraph(Run("   "))),
                }
            ),
            DocumentDiagnosticCode.EmptyField,
            PressReleaseTags.Headline
        );
        var placeholder = Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.PlaceholderBlock(
                            PressReleaseTags.Body,
                            "Write the release text here."
                        ),
                }
            ),
            DocumentDiagnosticCode.PlaceholderContent,
            PressReleaseTags.Body
        );
        Assert.Contains("placeholder", placeholder.Message);
    }

    [Fact]
    public void MetadataComesFromDocumentPropertiesAndMustBeCompatible()
    {
        var missing = Metadata();
        missing.Remove(PressReleaseTags.LanguageProperty);
        Single(Release(properties: missing), DocumentDiagnosticCode.MissingMetadata);

        var future = Metadata();
        future[PressReleaseTags.SchemaProperty] = "press-release/2";
        Single(Release(properties: future), DocumentDiagnosticCode.UnsupportedSchema);

        var version = Metadata();
        version[PressReleaseTags.TemplateVersionProperty] = "9.9";
        Single(Release(properties: version), DocumentDiagnosticCode.UnsupportedSchema);

        var language = Metadata("fr");
        Single(Release(properties: language), DocumentDiagnosticCode.UnsupportedSchema);

        var german = Release(properties: Metadata("de"));
        Assert.Equal("de", Valid(german).Language);
        var conflict = Read(german, "en");
        Assert.Contains(
            conflict.Diagnostics,
            item => item.Code == DocumentDiagnosticCode.ConflictingMetadata
        );
        Assert.Null(conflict.Release);
    }

    [Fact]
    public void OptionalSectionsAreOmittedWhenDeletedOrLeftAsPlaceholders()
    {
        var release = Valid(
            Release(
                omit:
                [
                    PressReleaseTags.Specifications,
                    PressReleaseTags.Media,
                    PressReleaseTags.CompanyDescription("example"),
                    PressReleaseTags.Editorial,
                ],
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.FactBox] = fixture =>
                        fixture.Block(
                            PressReleaseTags.FactBox,
                            fixture.PlaceholderBlock(PressReleaseTags.FactBoxHeading, "Heading"),
                            fixture.PlaceholderBlock(PressReleaseTags.FactBoxBody, "Text")
                        ),
                }
            )
        );

        Assert.Null(release.Specifications);
        Assert.Null(release.FactBox);
        Assert.Null(release.Media);
        Assert.Null(release.EditorialNote);
        Assert.Empty(release.CompanyDescriptions);
    }

    [Fact]
    public void PartialSectionsAndUnboundTextAreDiagnosed()
    {
        var headingOnly = Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.FactBox] = fixture =>
                        fixture.Block(
                            PressReleaseTags.FactBox,
                            fixture.Block(
                                PressReleaseTags.FactBoxHeading,
                                Paragraph(Run("Fact box"))
                            ),
                            fixture.Block(PressReleaseTags.FactBoxBody, Paragraph())
                        ),
                }
            ),
            DocumentDiagnosticCode.IncompleteSection,
            PressReleaseTags.FactBoxBody
        );
        Assert.Contains("delete the whole section", headingOnly.Remedy);

        // Word's "Remove Content Control" keeps the text but drops the tag.
        var unwrapped = Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Editorial] = _ => Paragraph(Run("Free for editorial use.")),
                }
            ),
            DocumentDiagnosticCode.UnboundContent
        );
        Assert.Contains("Free for editorial use.", unwrapped.Message);

        Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.FactBox] = fixture =>
                        fixture.Block(
                            PressReleaseTags.FactBox,
                            fixture.Block(
                                PressReleaseTags.FactBoxHeading,
                                Paragraph(Run("Fact box"))
                            ),
                            Paragraph(Run("Loose text")),
                            fixture.Block(PressReleaseTags.FactBoxBody, Paragraph(Run("Fact.")))
                        ),
                }
            ),
            DocumentDiagnosticCode.UnboundContent,
            PressReleaseTags.FactBox
        );
    }

    [Fact]
    public void InstructionsUnknownTagsAndMisplacedFieldsAreClassified()
    {
        var release = Valid(
            Release(extra: fixture =>
            {
                fixture.Add(
                    fixture.Block(
                        PressReleaseTags.Instruction,
                        Paragraph(Run("Delete optional sections as a whole."))
                    )
                );
                fixture.Add(fixture.Block("other.tag", Paragraph()));
            })
        );
        Assert.DoesNotContain(
            "Delete optional",
            Plain(release.Body.OfType<ParagraphBlock>().SelectMany(p => p.Content))
        );

        Single(
            Release(extra: fixture =>
                fixture.Add(fixture.Block("pr.subtitle", Paragraph(Run("x"))))
            ),
            DocumentDiagnosticCode.UnknownField,
            "pr.subtitle"
        );
        Single(
            Release(extra: fixture =>
                fixture.Add(fixture.Block(PressReleaseTags.FactBoxHeading, Paragraph(Run("x"))))
            ),
            DocumentDiagnosticCode.InvalidStructure,
            PressReleaseTags.FactBoxHeading
        );
        Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Paragraph(Run("Body")),
                            fixture.Block(PressReleaseTags.Headline, Paragraph(Run("Nested")))
                        ),
                }
            ),
            DocumentDiagnosticCode.InvalidStructure,
            PressReleaseTags.Headline
        );
        Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Headline] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Headline,
                            Paragraph(Run("One")),
                            Paragraph(Run("Two"))
                        ),
                }
            ),
            DocumentDiagnosticCode.InvalidStructure,
            PressReleaseTags.Headline
        );
    }

    [Fact]
    public void InlinePlainTextControlsAreAccepted()
    {
        var release = Valid(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Dateline] = fixture =>
                        Paragraph(
                            fixture.Inline(
                                PressReleaseTags.Dateline,
                                Run("Inline "),
                                Run("dateline", italic: true)
                            )
                        ),
                }
            )
        );
        Assert.Equal("Inline dateline", release.Dateline);
    }

    [Theory]
    [InlineData("ins")]
    [InlineData("del")]
    [InlineData("moveFrom")]
    [InlineData("rPrChange")]
    [InlineData("table")]
    public void TrackedChangesBlockExtraction(string kind)
    {
        OpenXmlElement Revised(WordFixture fixture)
        {
            var paragraph = Paragraph(Run("Body text"));

            switch (kind)
            {
                case "ins":
                    paragraph.AppendChild(
                        new InsertedRun(Run(" added")) { Id = "1", Author = "A" }
                    );
                    break;
                case "del":
                    paragraph.AppendChild(
                        new DeletedRun(new Run(new DeletedText(" removed")))
                        {
                            Id = "1",
                            Author = "A",
                        }
                    );
                    break;
                case "moveFrom":
                    paragraph.AppendChild(
                        new MoveFromRun(Run(" moved")) { Id = "1", Author = "A" }
                    );
                    break;
                case "rPrChange":
                    var run = Run(" restyled", bold: true);
                    run.RunProperties!.AppendChild(
                        new RunPropertiesChange(new PreviousRunProperties())
                        {
                            Id = "1",
                            Author = "A",
                        }
                    );
                    paragraph.AppendChild(run);
                    break;
                default:
                    var table = Table(new[] { "Size:", "1" });
                    table
                        .GetFirstChild<TableRow>()!
                        .PrependChild(
                            new TableRowProperties(new Inserted { Id = "1", Author = "A" })
                        );
                    return fixture.Block(
                        PressReleaseTags.Specifications,
                        fixture.Block(
                            PressReleaseTags.SpecificationsHeading,
                            Paragraph(Run("Data"))
                        ),
                        fixture.Block(PressReleaseTags.SpecificationsTable, table)
                    );
            }

            return fixture.Block(PressReleaseTags.Body, paragraph);
        }

        var tag = kind == "table" ? PressReleaseTags.Specifications : PressReleaseTags.Body;
        var diagnostic = Single(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [tag] = Revised,
                }
            ),
            DocumentDiagnosticCode.UnresolvedRevision,
            tag
        );
        Assert.Contains("accept or reject", diagnostic.Remedy);
    }

    [Fact]
    public void CommentsAreReportedWithoutReviewerDataAndExcludedFromContent()
    {
        var result = Read(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Body] = fixture =>
                    {
                        var paragraph = Paragraph(Run("Commented body."));
                        fixture.Comment(paragraph, "Please verify the number.");
                        return fixture.Block(PressReleaseTags.Body, paragraph);
                    },
                }
            )
        );

        Assert.True(result.Succeeded);
        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal(DocumentDiagnosticCode.CommentsPresent, warning.Code);
        Assert.DoesNotContain("Reviewer", warning.ToString());
        Assert.DoesNotContain("verify", warning.ToString());
        Assert.Equal("Commented body.", Plain(((ParagraphBlock)result.Release!.Body[0]).Content));
    }

    [Theory]
    [InlineData("drawing")]
    [InlineData("footnote")]
    [InlineData("field")]
    [InlineData("hidden")]
    [InlineData("symbol")]
    [InlineData("table")]
    public void UnsupportedContentFailsInsteadOfDisappearing(string kind)
    {
        Paragraph Content()
        {
            var paragraph = Paragraph(Run("Text "));

            switch (kind)
            {
                case "drawing":
                    paragraph.AppendChild(new Run(new Drawing()));
                    break;
                case "footnote":
                    paragraph.AppendChild(new Run(new FootnoteReference { Id = 1 }));
                    break;
                case "field":
                    paragraph.Append(
                        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                        new Run(new FieldCode(" PAGE ")),
                        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
                        Run("3"),
                        new Run(new FieldChar { FieldCharType = FieldCharValues.End })
                    );
                    break;
                case "hidden":
                    paragraph.AppendChild(
                        new Run(new RunProperties(new Vanish()), new Text("secret"))
                    );
                    break;
                case "symbol":
                    paragraph.AppendChild(
                        new Run(new SymbolChar { Font = "Wingdings", Char = "F04A" })
                    );
                    break;
            }

            return paragraph;
        }

        var document = Release(
            replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
            {
                [PressReleaseTags.Body] = fixture =>
                    kind == "table"
                        ? fixture.Block(
                            PressReleaseTags.Body,
                            Paragraph(Run("Text")),
                            Table(new[] { "a", "b" })
                        )
                        : fixture.Block(PressReleaseTags.Body, Content()),
            }
        );
        var diagnostic = Single(
            document,
            DocumentDiagnosticCode.UnsupportedContent,
            PressReleaseTags.Body
        );
        Assert.False(string.IsNullOrEmpty(diagnostic.Remedy));
    }

    [Fact]
    public void HyperlinksKeepDestinationsAndRejectBrokenUnsafeOrInternalLinks()
    {
        var release = Valid(
            Release(
                replace: new Dictionary<string, Func<WordFixture, OpenXmlElement>>
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Paragraph(
                                Run("See "),
                                fixture.Link(
                                    "https://example.com/de/video?id=1&lang=de",
                                    Run("the "),
                                    Run("video", bold: true)
                                ),
                                Run(" or "),
                                new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                                new Run(new FieldCode(" HYPERLINK \"https://example.com/field\" ")),
                                new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
                                Run("field link"),
                                new Run(new FieldChar { FieldCharType = FieldCharValues.End })
                            )
                        ),
                }
            )
        );

        var links = ((ParagraphBlock)release.Body[0]).Content.OfType<HyperlinkInline>().ToArray();
        Assert.Equal(
            "https://example.com/de/video?id=1&lang=de",
            links[0].Destination.OriginalString
        );
        Assert.Equal("the video", Plain(links[0].Content));
        Assert.Equal(TextMarks.Bold, ((TextInline)links[0].Content[1]).Marks);
        Assert.Equal("https://example.com/field", links[1].Destination.OriginalString);
        Assert.Equal("field link", Plain(links[1].Content));

        OpenXmlElement Body(WordFixture fixture, OpenXmlElement link) =>
            fixture.Block(PressReleaseTags.Body, Paragraph(link));

        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        Body(fixture, fixture.BrokenLink(Run("x"))),
                }
            ),
            DocumentDiagnosticCode.BrokenHyperlink
        );
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        Body(fixture, fixture.Link("file:///C:/secret.docx", Run("x"))),
                }
            ),
            DocumentDiagnosticCode.UnsafeHyperlink
        );
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        Body(fixture, new Hyperlink(Run("x")) { Anchor = "_Toc1" }),
                }
            ),
            DocumentDiagnosticCode.UnsupportedContent
        );
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Headline] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Headline,
                            Paragraph(fixture.Link("https://example.com", Run("x")))
                        ),
                }
            ),
            DocumentDiagnosticCode.UnsupportedContent,
            PressReleaseTags.Headline
        );
    }

    [Fact]
    public void ListsKeepKindStartRestartContinuationAndNesting()
    {
        var release = Valid(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Item(Bullets, 0, Run("Bullet one")),
                            Item(Bullets, 1, Run("Nested")),
                            Item(Bullets, 0, Run("Bullet two")),
                            Paragraph(Run("Between")),
                            Item(Numbers, 0, Run("Five")),
                            Item(Numbers, 0, Run("Six")),
                            Paragraph(Run("Interrupted")),
                            Item(Numbers, 0, Run("Seven continues")),
                            Paragraph(Run("Restart follows")),
                            Item(RestartedNumbers, 0, Run("One again")),
                            Styled("ListBullet", Run("Bullet from style"))
                        ),
                }
            )
        );

        var blocks = release.Body;
        var bullets = Assert.IsType<ListBlock>(blocks[0]);
        Assert.Equal(ListKind.Bullet, bullets.Kind);
        Assert.Equal(2, bullets.Items.Count);
        Assert.Equal("Nested", Plain(Assert.Single(bullets.Items[0].Sublist!.Items).Content));
        var numbers = Assert.IsType<ListBlock>(blocks[2]);
        Assert.Equal(ListKind.Decimal, numbers.Kind);
        Assert.Equal(5, numbers.Start);
        Assert.Equal(2, numbers.Items.Count);
        Assert.Equal(7, Assert.IsType<ListBlock>(blocks[4]).Start);
        Assert.Equal(1, Assert.IsType<ListBlock>(blocks[6]).Start);
        Assert.Equal(ListKind.Bullet, Assert.IsType<ListBlock>(blocks[7]).Kind);
    }

    [Fact]
    public void ManualListIndentationWarnsBecauseOnlyLevelsArePublished()
    {
        var indented = Item(Bullets, 0, Run("Looks nested"));
        indented.ParagraphProperties!.Indentation = new Indentation { Left = "1080" };
        var result = Read(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Item(Bullets, 0, Run("Top")),
                            indented
                        ),
                }
            )
        );

        Assert.True(result.Succeeded);
        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal(DocumentDiagnosticCode.IgnoredFormatting, warning.Code);
        Assert.Equal("paragraph 2", warning.Location);
        Assert.Equal(2, ((ListBlock)result.Release!.Body[0]).Items.Count);
    }

    [Fact]
    public void UnsupportedListsAreDiagnosed()
    {
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(PressReleaseTags.Body, Item(Roman, 0, Run("i"))),
                }
            ),
            DocumentDiagnosticCode.UnsupportedList
        );
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Item(Bullets, 0, Run("a")),
                            Item(Bullets, 1, Run("b")),
                            Item(Bullets, 2, Run("c"))
                        ),
                }
            ),
            DocumentDiagnosticCode.UnsupportedList
        );
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(PressReleaseTags.Body, Item(Bullets, 1, Run("orphan"))),
                }
            ),
            DocumentDiagnosticCode.UnsupportedList
        );
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Media] = fixture =>
                        fixture.Block(PressReleaseTags.Media, Item(Bullets, 0, Run("x"))),
                }
            ),
            DocumentDiagnosticCode.UnsupportedList,
            PressReleaseTags.Media
        );
    }

    [Fact]
    public void EmphasisResolvesStylesTogglesAndExplicitOverrides()
    {
        var release = Valid(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Body] = fixture =>
                        fixture.Block(
                            PressReleaseTags.Body,
                            Paragraph(
                                Run("strong", style: "Strong"),
                                Run("emphasis", style: "Emphasis"),
                                Run("both", bold: true, italic: true)
                            ),
                            Styled(
                                "BoldParagraph",
                                Run("styled"),
                                Run("toggled", style: "Strong"),
                                Run("cleared", bold: false)
                            ),
                            Styled("DerivedPlain", Run("derived"))
                        ),
                }
            )
        );

        TextMarks[] Marks(int index) =>
            ((ParagraphBlock)release.Body[index])
                .Content.Cast<TextInline>()
                .Select(item => item.Marks)
                .ToArray();

        Assert.Equal(
            new[] { TextMarks.Bold, TextMarks.Italic, TextMarks.Bold | TextMarks.Italic },
            Marks(0)
        );
        Assert.Equal(new[] { TextMarks.Bold, TextMarks.None }, Marks(1));
        Assert.Equal("styledtoggledcleared", Plain(((ParagraphBlock)release.Body[1]).Content));
        Assert.Equal(new[] { TextMarks.None }, Marks(2));
    }

    [Fact]
    public void SpecificationTablesValidateShape()
    {
        OpenXmlElement Specs(WordFixture fixture, Table table) =>
            fixture.Block(
                PressReleaseTags.Specifications,
                fixture.Block(PressReleaseTags.SpecificationsHeading, Paragraph(Run("Data"))),
                fixture.Block(PressReleaseTags.SpecificationsTable, table)
            );

        var header = Table(
            new[] { "Label", "Value" },
            new[] { "A:", "1" },
            new[] { "B:", "2" },
            new[] { "", "" },
            new[] { "", "" }
        );
        header.GetFirstChild<TableRow>()!.PrependChild(new TableRowProperties(new TableHeader()));

        // Word writes cell-margin exceptions on every row; they are formatting, not shape.
        foreach (var row in header.Elements<TableRow>())
            row.PrependChild(
                new TablePropertyExceptions(
                    new TableCellMarginDefault(
                        new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

        var release = Valid(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Specifications] = fixture => Specs(fixture, header),
                }
            )
        );
        Assert.Equal(
            new[] { "A:", "B:" },
            release.Specifications!.Rows.Select(row => Plain(row.Label))
        );

        var incomplete = Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Specifications] = fixture =>
                        Specs(fixture, Table(new[] { "A:", "1" }, new[] { "B:", "" })),
                }
            ),
            DocumentDiagnosticCode.InvalidTableRow,
            PressReleaseTags.SpecificationsTable
        );
        Assert.Equal("row 2", incomplete.Location);

        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Specifications] = fixture =>
                        Specs(
                            fixture,
                            Table(new[] { "A:", "1" }, new[] { "", "" }, new[] { "B:", "2" })
                        ),
                }
            ),
            DocumentDiagnosticCode.InvalidTableRow
        );

        var merged = Table(new[] { "A:", "1" });
        merged.Descendants<TableCellProperties>().First().AppendChild(new GridSpan { Val = 2 });
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Specifications] = fixture => Specs(fixture, merged),
                }
            ),
            DocumentDiagnosticCode.InvalidTableRow
        );

        var nested = Table(new[] { "A:", "1" });
        nested.Descendants<TableCell>().Last().AppendChild(Table(new[] { "x" }));
        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Specifications] = fixture => Specs(fixture, nested),
                }
            ),
            DocumentDiagnosticCode.InvalidTableRow
        );

        Single(
            Release(
                replace: new()
                {
                    [PressReleaseTags.Specifications] = fixture =>
                        Specs(fixture, Table(new[] { "A:", "1", "extra" })),
                }
            ),
            DocumentDiagnosticCode.InvalidTableRow
        );
    }

    [Fact]
    public void PackagesThatAreNotPlainDocumentsAreRejected()
    {
        Single(Encoding("not a zip"), DocumentDiagnosticCode.InvalidPackage);
        Single(
            Create(_ => { }, type: WordprocessingDocumentType.Template),
            DocumentDiagnosticCode.UnsupportedDocumentType
        );
        Single(
            Create(_ => { }, type: WordprocessingDocumentType.MacroEnabledDocument),
            DocumentDiagnosticCode.MacroEnabledDocument
        );

        using (
            var stream = new MemoryStream(
                Create(_ => { }, type: WordprocessingDocumentType.Template),
                false
            )
        )
        {
            var extraction = new WordContentReader(allowTemplates: true).Read(
                stream,
                Schema.Content
            );
            Assert.DoesNotContain(
                extraction.Diagnostics,
                item => item.Code == DocumentDiagnosticCode.UnsupportedDocumentType
            );
        }
    }

    [Fact]
    public void SourcesAreNeverModifiedAndReadsAreIndependent()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "substrate documents " + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(directory);

        try
        {
            var valid = Path.Combine(directory, "Release ä.docx");
            var invalid = Path.Combine(directory, "Invalid.docx");
            File.WriteAllBytes(valid, Release());
            File.WriteAllBytes(invalid, Release(omit: [PressReleaseTags.Body]));
            var before = new[] { Hash(valid), Hash(invalid) };
            var reader = new PressReleaseReader();

            var first = reader.Read(IO.FileSystemPath.Parse(valid), Schema);
            var second = reader.Read(IO.FileSystemPath.Parse(invalid), Schema);
            var third = reader.Read(
                IO.FileSystemPath.Parse(valid),
                new PressReleaseSchema(["en"], ["1.0"], ["partner"])
            );

            Assert.True(first.Succeeded);
            Assert.False(second.Succeeded);
            Assert.Single(first.Release!.CompanyDescriptions);
            Assert.False(third.Succeeded);
            Assert.Contains(
                third.Diagnostics,
                item => item.Code == DocumentDiagnosticCode.UnknownField
            );
            Assert.Equal(before, new[] { Hash(valid), Hash(invalid) });
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static byte[] Encoding(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return Convert.ToBase64String(sha.ComputeHash(stream));
    }
}
