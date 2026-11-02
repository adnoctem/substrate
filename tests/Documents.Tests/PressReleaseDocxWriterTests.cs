using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AdNoctem.Substrate.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace AdNoctem.Substrate.Documents.Tests;

public sealed class PressReleaseDocxWriterTests
{
    private static PressRelease Simple(
        string language = "en",
        IEnumerable<CompanyDescription>? descriptions = null
    ) =>
        new PressRelease(
            language,
            "1.0",
            "Release headline",
            "Sampletown - May",
            [new ParagraphBlock([new TextInline("Lead.")])],
            new PressReleaseSection(
                "Media Contact:",
                [new ParagraphBlock([new TextInline("Jane Doe")])]
            ),
            companyDescriptions: descriptions
        );

    private static PressRelease Complete() =>
        new PressRelease(
            "en",
            "1.0",
            "Release headline",
            "Sampletown - May",
            [
                new ParagraphBlock([new TextInline("Lead & text.")]),
                new ListBlock(ListKind.Decimal, 3, [new ListItem([new TextInline("Three")])]),
            ],
            new PressReleaseSection(
                "Media Contact:",
                [new ParagraphBlock([new TextInline("Jane Doe")])]
            ),
            new PressReleaseSpecifications(
                "TECHNICAL & DATA",
                [new SpecificationRow([new TextInline("Size:")], [new TextInline("275/18")])]
            ),
            new PressReleaseSection("FACT & BOX", [new ParagraphBlock([new TextInline("Fact.")])]),
            [
                new ParagraphBlock([
                    new TextInline("Video:"),
                    new HyperlinkInline(
                        new Uri("https://video.example/?a=1&b=2"),
                        [new TextInline("watch")]
                    ),
                ]),
            ],
            [
                new CompanyDescription(
                    "example",
                    [new ParagraphBlock([new TextInline("About & us.")])]
                ),
            ],
            [new ParagraphBlock([new TextInline("Editorial.")])]
        );

    private static byte[] Render(PressRelease release, TemplatePack pack)
    {
        using (var stream = new MemoryStream())
        {
            new PressReleaseDocxWriter().Write(release, pack, stream);
            return stream.ToArray();
        }
    }

    private static string Text(byte[] bytes)
    {
        using (var document = WordprocessingDocument.Open(new MemoryStream(bytes, false), false))
            return document.MainDocumentPart!.Document!.Body!.InnerText;
    }

    [Fact]
    public void WritesCompleteReleaseWithRelationshipsNumberingAndPageSetup()
    {
        using var fixture = new TemplatePackFixture();
        var bytes = Render(Complete(), fixture.Load());
        var text = Text(bytes);

        Assert.Contains("Release headline", text);
        Assert.Contains("Sampletown - May", text);
        Assert.Contains("Lead & text.", text);
        Assert.Contains("TECHNICAL & DATA", text);
        Assert.Contains("Size:", text);
        Assert.Contains("275/18", text);
        Assert.Contains("FACT & BOX", text);
        Assert.Contains("Fact.", text);
        Assert.Contains("watch", text);
        Assert.Contains("About & us.", text);
        Assert.Contains("Editorial.", text);
        Assert.Contains("Media Contact:", text);
        Assert.Contains("Jane Doe", text);
        Assert.DoesNotContain("Base ", text);

        using (var document = WordprocessingDocument.Open(new MemoryStream(bytes, false), false))
        {
            var main = document.MainDocumentPart!;
            var body = main.Document!.Body!;

            Assert.Empty(body.Descendants<SdtBlock>());
            Assert.Empty(body.Descendants<SdtRun>());
            Assert.Contains(
                main.HyperlinkRelationships,
                item => item.Uri.AbsoluteUri == "https://video.example/?a=1&b=2"
            );
            Assert.NotEmpty(
                main.NumberingDefinitionsPart!.Numbering!.Elements<NumberingInstance>()
            );
            var size = body.Elements<SectionProperties>().Last().GetFirstChild<PageSize>();
            Assert.Equal(11906u, size!.Width!.Value);
            Assert.Equal(16838u, size.Height!.Value);
        }
    }

    [Fact]
    public void RemovesAbsentOptionalSections()
    {
        using var fixture = new TemplatePackFixture();
        var text = Text(Render(Simple(), fixture.Load()));

        Assert.Contains("Release headline", text);
        Assert.Contains("Media Contact:", text);
        Assert.DoesNotContain("TECHNICAL", text);
        Assert.DoesNotContain("FACT", text);
        Assert.DoesNotContain("Video:", text);
        Assert.DoesNotContain("About", text);
        Assert.DoesNotContain("Editorial.", text);
        Assert.DoesNotContain("Base ", text);
    }

    [Fact]
    public void RejectsMissingRequiredControl()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteBytes(
            "base.docx",
            RemoveControl(DocxTemplateFixture.Create(), PressReleaseTags.Body)
        );
        fixture.WriteManifest();
        var pack = fixture.Load();

        var exception = Assert.Throws<DocumentException>(() => Render(Simple(), pack));
        Assert.Equal(DocumentFailureReason.InvalidTemplatePack, exception.Reason);
    }

    [Fact]
    public void RejectsUnknownContentControl()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteBytes("base.docx", AddControl(DocxTemplateFixture.Create(), "pr.unknown"));
        fixture.WriteManifest();
        var pack = fixture.Load();

        var exception = Assert.Throws<DocumentException>(() => Render(Simple(), pack));
        Assert.Equal(DocumentFailureReason.InvalidTemplatePack, exception.Reason);
    }

    [Fact]
    public void RejectsCompanyDescriptionNotDeclaredByPack()
    {
        using var fixture = new TemplatePackFixture();
        var release = Simple(
            descriptions:
            [
                new CompanyDescription(
                    "partner",
                    [new ParagraphBlock([new TextInline("Partner.")])]
                ),
            ]
        );

        var exception = Assert.Throws<DocumentException>(() => Render(release, fixture.Load()));
        Assert.Equal(DocumentFailureReason.InvalidRelease, exception.Reason);
    }

    [Fact]
    public void WritesToFileWithExplicitOverwrite()
    {
        using var fixture = new TemplatePackFixture();
        var pack = fixture.Load();
        var destination = FileSystemPath.Parse(Path.Combine(fixture.Root, "release.docx"));
        var writer = new PressReleaseDocxWriter();

        writer.Write(Simple(), pack, destination);
        Assert.Contains("Release headline", Text(File.ReadAllBytes(destination.Value)));

        Assert.Throws<IOException>(() => writer.Write(Simple(), pack, destination));

        writer.Write(Simple(), pack, destination, overwrite: true);
        Assert.Contains("Media Contact:", Text(File.ReadAllBytes(destination.Value)));
    }

    [Fact]
    public void HonorsCancellationBeforeWriting()
    {
        using var fixture = new TemplatePackFixture();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new PressReleaseDocxWriter().Write(
                Simple(),
                fixture.Load(),
                new MemoryStream(),
                cancelled.Token
            )
        );
    }

    private static byte[] RemoveControl(byte[] bytes, string tag)
    {
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = 0;

            using (var document = WordprocessingDocument.Open(stream, true))
            {
                var control = document
                    .MainDocumentPart!.Document!.Body!.Descendants<SdtBlock>()
                    .Single(item => WordContentReader.Tag(item) == tag);
                control.Remove();
                document.Save();
            }

            return stream.ToArray();
        }
    }

    private static byte[] AddControl(byte[] bytes, string tag)
    {
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = 0;

            using (var document = WordprocessingDocument.Open(stream, true))
            {
                var body = document.MainDocumentPart!.Document!.Body!;
                body.AppendChild(
                    new SdtBlock(
                        new SdtProperties(new Tag { Val = tag }),
                        new SdtContentBlock(new Paragraph(new Run(new Text("Unexpected"))))
                    )
                );
                document.Save();
            }

            return stream.ToArray();
        }
    }
}
