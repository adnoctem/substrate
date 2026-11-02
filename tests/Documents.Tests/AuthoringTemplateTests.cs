using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace AdNoctem.Substrate.Documents.Tests;

public sealed class AuthoringTemplateTests
{
    internal static AuthoringTemplate Synthetic(bool filled, string language = "en")
    {
        return new AuthoringTemplate(
            [
                new AuthoringInstruction(
                    PressReleaseTags.Instruction,
                    "Fill in the fields and delete unused optional sections."
                ),
                AuthoringField.Text(
                    PressReleaseTags.Headline,
                    "Headline",
                    "Enter the headline.",
                    filled ? "Filled headline" : null,
                    true
                ),
                AuthoringField.Text(
                    PressReleaseTags.Dateline,
                    "Dateline",
                    "Place, month year",
                    filled ? "Sampletown, May 2026" : null,
                    true
                ),
                AuthoringField.Rich(
                    PressReleaseTags.Body,
                    "Body",
                    "Write the release text.",
                    filled
                        ?
                        [
                            new ParagraphBlock([
                                new TextInline("Lead with "),
                                new TextInline("emphasis", TextMarks.Bold | TextMarks.Italic),
                            ]),
                            new ListBlock(
                                ListKind.Decimal,
                                3,
                                [
                                    new ListItem([new TextInline("Three")]),
                                    new ListItem([new TextInline("Four")]),
                                ]
                            ),
                        ]
                        : null,
                    true
                ),
                AuthoringField.Group(
                    PressReleaseTags.Specifications,
                    "Technical data (optional)",
                    false,
                    AuthoringField.Text(
                        PressReleaseTags.SpecificationsHeading,
                        "Heading",
                        null,
                        "TECHNICAL DATA"
                    ),
                    AuthoringField.Table(
                        PressReleaseTags.SpecificationsTable,
                        "Rows",
                        ["Label", "Value"],
                        filled
                            ? new[]
                            {
                                new IEnumerable<DocumentInline>[]
                                {
                                    [new TextInline("Size:")],
                                    [new TextInline("XL")],
                                },
                            }
                            : null
                    )
                ),
                AuthoringField.Rich(
                    PressReleaseTags.CompanyDescription("example"),
                    "About Example (optional)",
                    null,
                    [
                        new ParagraphBlock([
                            new TextInline("About Example:", TextMarks.Bold),
                            new TextInline(" Default wording."),
                        ]),
                    ]
                ),
                AuthoringField.Group(
                    PressReleaseTags.Contact,
                    "Contact",
                    true,
                    AuthoringField.Text(
                        PressReleaseTags.ContactHeading,
                        "Heading",
                        null,
                        "Contact:",
                        true
                    ),
                    AuthoringField.Rich(
                        PressReleaseTags.ContactDetails,
                        "Details",
                        null,
                        [
                            new ParagraphBlock([
                                new TextInline("Press Office"),
                                LineBreakInline.Instance,
                                new HyperlinkInline(
                                    new System.Uri("mailto:press@example.com"),
                                    [new TextInline("press@example.com")]
                                ),
                            ]),
                        ],
                        true
                    )
                ),
            ],
            WordFixture.Metadata(language),
            "en-GB"
        );
    }

    private static byte[] Build(AuthoringTemplate template, bool asTemplate)
    {
        using (var stream = new MemoryStream())
        {
            new AuthoringTemplateBuilder().Write(template, stream, asTemplate);
            return stream.ToArray();
        }
    }

    [Fact]
    public void TemplatesArePlaceholderOnlyAndCarryMetadataWithoutPublishingInstructions()
    {
        var bytes = Build(Synthetic(false), true);

        using (var document = WordprocessingDocument.Open(new MemoryStream(bytes, false), false))
        {
            Assert.Equal(WordprocessingDocumentType.Template, document.DocumentType);
            Assert.Null(document.MainDocumentPart!.VbaProjectPart);
            Assert.Null(document.CoreFilePropertiesPart);
            Assert.Equal(
                3,
                document
                    .MainDocumentPart.GlossaryDocumentPart!.GlossaryDocument!.Descendants<DocPart>()
                    .Count()
            );
            var headline = document
                .MainDocumentPart.Document!.Descendants<SdtBlock>()
                .First(sdt => WordContentReader.Tag(sdt) == PressReleaseTags.Headline);
            Assert.NotNull(headline.SdtProperties!.GetFirstChild<Lock>());
            Assert.NotNull(headline.SdtProperties.GetFirstChild<ShowingPlaceholder>());
        }

        using (var stream = new MemoryStream(bytes, false))
        {
            var extraction = new WordContentReader(allowTemplates: true).Read(
                stream,
                WordFixture.Schema.Content
            );
            var errors = extraction
                .Diagnostics.Where(item => item.Severity == DocumentDiagnosticSeverity.Error)
                .ToArray();

            // An untouched optional section with a prefilled heading must be completed or deleted as a whole.
            Assert.Equal(
                new (string?, DocumentDiagnosticCode)[]
                {
                    (
                        PressReleaseTags.SpecificationsTable,
                        DocumentDiagnosticCode.IncompleteSection
                    ),
                    (PressReleaseTags.Headline, DocumentDiagnosticCode.PlaceholderContent),
                    (PressReleaseTags.Dateline, DocumentDiagnosticCode.PlaceholderContent),
                    (PressReleaseTags.Body, DocumentDiagnosticCode.PlaceholderContent),
                },
                errors.Select(item => (item.Field, item.Code))
            );
        }
    }

    [Fact]
    public void PrefilledContentRoundTripsThroughTheReader()
    {
        using (var stream = new MemoryStream(Build(Synthetic(true, "de"), false), false))
        {
            var result = new PressReleaseReader().Read(stream, WordFixture.Schema);
            Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
            var release = result.Release!;

            Assert.Equal("de", release.Language);
            Assert.Equal("Filled headline", release.Headline);
            var lead = (ParagraphBlock)release.Body[0];
            Assert.Equal(TextMarks.Bold | TextMarks.Italic, ((TextInline)lead.Content[1]).Marks);
            var list = (ListBlock)release.Body[1];
            Assert.Equal(ListKind.Decimal, list.Kind);
            Assert.Equal(3, list.Start);
            Assert.Equal("TECHNICAL DATA", release.Specifications!.Heading);
            Assert.Equal(
                "Size:",
                Inlines.ToPlainText(Assert.Single(release.Specifications.Rows).Label)
            );
            Assert.Equal(
                "About Example: Default wording.",
                Inlines.ToPlainText(
                    ((ParagraphBlock)release.CompanyDescriptions[0].Blocks[0]).Content
                )
            );
            var contact = (ParagraphBlock)release.Contact.Blocks[0];
            Assert.Equal(
                "mailto:press@example.com",
                contact.Content.OfType<HyperlinkInline>().Single().Destination.OriginalString
            );
        }
    }

    [Fact]
    public void TemplatesProvideLinkedTwoLevelListStylesAndKeepRowsInsertable()
    {
        using (
            var document = WordprocessingDocument.Open(
                new MemoryStream(Build(Synthetic(false), true), false),
                false
            )
        )
        {
            var main = document.MainDocumentPart!;
            var styles = main.StyleDefinitionsPart!.Styles!.Elements<Style>()
                .ToDictionary(style => style.StyleId!.Value!);
            Assert.Equal(
                1,
                styles["ListBullet2"]
                    .StyleParagraphProperties!
                    .NumberingProperties!
                    .NumberingLevelReference!
                    .Val!
                    .Value
            );
            var instance = styles["ListNumber"]
                .StyleParagraphProperties!
                .NumberingProperties!
                .NumberingId!
                .Val!
                .Value;
            var definition = main.NumberingDefinitionsPart!.Numbering!.Elements<NumberingInstance>()
                .Single(item => item.NumberID!.Value == instance);
            var levels = main
                .NumberingDefinitionsPart.Numbering.Elements<AbstractNum>()
                .Single(item =>
                    item.AbstractNumberId!.Value == definition.AbstractNumId!.Val!.Value
                )
                .Elements<Level>()
                .ToArray();
            Assert.Equal(
                new[] { "ListNumber", "ListNumber2" },
                levels.Select(level => level.ParagraphStyleIdInLevel!.Val!.Value)
            );

            // Word 2019 drops enclosing controls when a row is inserted below a table that ends its control.
            var table = main.Document!.Descendants<SdtBlock>()
                .Single(sdt => WordContentReader.Tag(sdt) == PressReleaseTags.SpecificationsTable);
            Assert.IsType<Paragraph>(table.SdtContentBlock!.LastChild);
        }
    }

    [Fact]
    public void StyleSourcesContributeStylesNumberingAndPageSetupOnly()
    {
        var source = WordFixture.Release();
        var template = Synthetic(false);
        var styled = new AuthoringTemplate(
            template.Content,
            WordFixture.Metadata(),
            "de-DE",
            source
        );

        using (
            var document = WordprocessingDocument.Open(
                new MemoryStream(Build(styled, true), false),
                false
            )
        )
        {
            var main = document.MainDocumentPart!;
            Assert.Contains(
                main.StyleDefinitionsPart!.Styles!.Elements<Style>(),
                style => style.StyleId!.Value == "BoldParagraph"
            );
            Assert.Contains(
                main.NumberingDefinitionsPart!.Numbering!.Elements<NumberingInstance>(),
                item => item.NumberID!.Value == WordFixture.Roman
            );
            Assert.DoesNotContain("Synthetic Headline", main.Document!.InnerText);
            Assert.Empty(main.HeaderParts);
        }
    }

    [Fact]
    public void BuildsAreReproducible()
    {
        static string Parts(byte[] bytes)
        {
            using (
                var document = WordprocessingDocument.Open(new MemoryStream(bytes, false), false)
            )
                return document.MainDocumentPart!.Document!.OuterXml
                    + document.MainDocumentPart.GlossaryDocumentPart!.GlossaryDocument!.OuterXml;
        }

        Assert.Equal(Parts(Build(Synthetic(false), true)), Parts(Build(Synthetic(false), true)));
    }
}
