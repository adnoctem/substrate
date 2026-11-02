using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using AdNoctem.Substrate.IO;
using Xunit;

namespace AdNoctem.Substrate.Documents.Tests;

public sealed class PressReleaseHtmlWriterTests
{
    private const string WithoutMedia =
        "[\"specifications\",\"factBox\",\"companyDescriptions\",\"editorial\",\"contact\"]";

    private static string Render(PressRelease release, TemplatePack pack)
    {
        using (var stream = new MemoryStream())
        {
            new PressReleaseHtmlWriter().Write(release, pack, stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private static PressRelease Simple(
        string language = "en",
        IEnumerable<CompanyDescription>? descriptions = null
    ) =>
        new PressRelease(
            language,
            "1.0",
            "Headline & <tag>",
            "Sampletown - May",
            [new ParagraphBlock([new TextInline("Lead & text.")])],
            new PressReleaseSection(
                "Media Contact:",
                [new ParagraphBlock([new TextInline("Jane Doe")])]
            ),
            companyDescriptions: descriptions
        );

    private static PressRelease Complete()
    {
        var body = new List<DocumentBlock>
        {
            new ParagraphBlock([
                new TextInline("Lead "),
                new TextInline("bold", TextMarks.Bold),
                new TextInline(" and "),
                new TextInline("both", TextMarks.Bold | TextMarks.Italic),
                new TextInline(" & <raw>"),
                LineBreakInline.Instance,
                new HyperlinkInline(
                    new Uri("https://example.test/?a=1&b=2"),
                    [new TextInline("site & more")]
                ),
            ]),
            new ListBlock(
                ListKind.Bullet,
                1,
                [
                    new ListItem(
                        [new TextInline("One")],
                        new ListBlock(
                            ListKind.Decimal,
                            3,
                            [new ListItem([new TextInline("Three")])]
                        )
                    ),
                ]
            ),
        };

        return new PressRelease(
            "en",
            "1.0",
            "Headline & <tag>",
            "Sampletown - May",
            body,
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
                    LineBreakInline.Instance,
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
    }

    [Fact]
    public void WritesCompleteReleaseWithEncodedContentAndOrderedSections()
    {
        using var fixture = new TemplatePackFixture();
        var html = Render(Complete(), fixture.Load());

        Assert.Contains("<html lang=\"en\">", html);
        Assert.Contains("<title>Headline &amp; &lt;tag&gt;</title>", html);
        Assert.Contains("<h1>Headline &amp; &lt;tag&gt;</h1>", html);
        Assert.Contains(
            "<span class=\"dateline\"><strong>Sampletown - May</strong></span> - Lead ",
            html
        );
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<strong><em>both</em></strong>", html);
        Assert.Contains(" &amp; &lt;raw&gt;<br>", html);
        Assert.Contains("<a href=\"https://example.test/?a=1&amp;b=2\">site &amp; more</a>", html);
        Assert.Contains("<ul><li>One<ol start=\"3\"><li>Three</li></ol></li></ul>", html);
        Assert.Contains(
            "<h2>TECHNICAL &amp; DATA</h2><ul><li><strong>Size:</strong> 275/18</li></ul>",
            html
        );
        Assert.Contains("<h2>FACT &amp; BOX</h2><p>Fact.</p>", html);
        Assert.Contains("<a href=\"https://video.example/?a=1&amp;b=2\">watch</a>", html);
        Assert.Contains("<div class=\"about-box\"><p>About &amp; us.</p></div>", html);
        Assert.Contains("<div class=\"editorial\"><p>Editorial.</p></div>", html);
        Assert.Contains(
            "<footer><div>Media Contact:</div><div><p>Jane Doe</p></div></footer>",
            html
        );
        Assert.DoesNotContain("{{", html);
        Assert.True(html.IndexOf("<h1>") < html.IndexOf("<span class=\"dateline\""));
        Assert.True(html.IndexOf("TECHNICAL") < html.IndexOf("FACT"));
        Assert.True(html.IndexOf("FACT") < html.IndexOf("about-box"));
        Assert.True(html.IndexOf("about-box") < html.IndexOf("<footer>"));
    }

    [Fact]
    public void OmitsAbsentOptionalSections()
    {
        using var fixture = new TemplatePackFixture();
        var html = Render(Simple(), fixture.Load());

        Assert.DoesNotContain("TECHNICAL", html);
        Assert.DoesNotContain("about-box", html);
        Assert.DoesNotContain("media-link", html);
        Assert.DoesNotContain("editorial", html);
        Assert.DoesNotContain("{{", html);
        Assert.Contains("<footer>", html);
    }

    [Fact]
    public void SelectsLocalizedLabelsAndLanguage()
    {
        using var fixture = new TemplatePackFixture();
        var html = Render(Simple("de"), fixture.Load());

        Assert.Contains("<html lang=\"de\">", html);
        Assert.Contains("PRESSEMITTEILUNG", html);
        Assert.DoesNotContain("PRESS RELEASE", html);
    }

    [Fact]
    public void RejectsUnsupportedLanguage()
    {
        using var fixture = new TemplatePackFixture();
        var exception = Assert.Throws<DocumentException>(() =>
            Render(Simple("fr"), fixture.Load())
        );

        Assert.Equal(DocumentFailureReason.UnsupportedLanguage, exception.Reason);
    }

    [Fact]
    public void RejectsSectionThatThePackDoesNotMap()
    {
        using var fixture = new TemplatePackFixture();
        var html = TemplatePackFixture.DefaultHtml.Replace(
            "{{#media}}<div class=\"media-link\">{{media}}</div>{{/media}}",
            ""
        );
        fixture.Write("release.en.html", html);
        fixture.Write("release.de.html", html);
        fixture.WriteManifest(
            companyDescriptions: "[\"example\"]",
            enSections: WithoutMedia,
            deSections: WithoutMedia
        );
        var pack = fixture.Load();

        var exception = Assert.Throws<DocumentException>(() => Render(Complete(), pack));
        Assert.Equal(DocumentFailureReason.UnsupportedSection, exception.Reason);
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
        var destination = FileSystemPath.Parse(Path.Combine(fixture.Root, "release.html"));
        var writer = new PressReleaseHtmlWriter();

        writer.Write(Simple(), pack, destination);
        Assert.Contains("Headline", File.ReadAllText(destination.Value));

        Assert.Throws<IOException>(() => writer.Write(Simple(), pack, destination));

        writer.Write(Simple(), pack, destination, overwrite: true);
        Assert.Contains("PRESS RELEASE", File.ReadAllText(destination.Value));
    }

    [Fact]
    public void HonorsCancellationBeforeWriting()
    {
        using var fixture = new TemplatePackFixture();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new PressReleaseHtmlWriter().Write(
                Simple(),
                fixture.Load(),
                new MemoryStream(),
                cancelled.Token
            )
        );
    }
}
