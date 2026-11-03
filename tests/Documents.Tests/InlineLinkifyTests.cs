using System;
using System.Linq;
using Xunit;

namespace AdNoctem.Substrate.Documents.Tests;

public sealed class InlineLinkifyTests
{
    private static HyperlinkInline Link(DocumentInline item) =>
        Assert.IsType<HyperlinkInline>(item);

    private static string Text(DocumentInline item) => Assert.IsType<TextInline>(item).Text;

    [Theory]
    [InlineData("See https://example.com/a.", "https://example.com/a", ".")]
    [InlineData("See https://example.com/a, then", "https://example.com/a", ", then")]
    [InlineData("See https://example.com/a; then", "https://example.com/a", "; then")]
    [InlineData("(see https://example.com/a)", "https://example.com/a", ")")]
    [InlineData("See https://example.com/a?", "https://example.com/a", "?")]
    public void TrailingPunctuationStaysText(string input, string url, string trailing)
    {
        var content = Inlines.Linkify([new TextInline(input)]);

        var link = Link(content[1]);
        Assert.Equal(url, link.Destination.OriginalString);
        Assert.Equal(url, Inlines.ToPlainText(link.Content));
        Assert.Equal(trailing, Inlines.ToPlainText(content.Skip(2)));
        Assert.All(content.Skip(2), item => Assert.IsType<TextInline>(item));
        Assert.Equal(input, Inlines.ToPlainText(content));
    }

    [Fact]
    public void QueriesFragmentsAndBalancedBracketsStayInTheDestination()
    {
        const string Url = "https://www.youtube.com/watch?v=abc_1-2&t=30s#top";
        const string Wiki = "https://en.example.org/wiki/Tyre_(vehicle)";
        var content = Inlines.Linkify([new TextInline(Url + " and " + Wiki + ".")]);

        Assert.Equal(Url, Link(content[0]).Destination.OriginalString);
        Assert.Equal(" and ", Text(content[1]));
        Assert.Equal(Wiki, Link(content[2]).Destination.OriginalString);
        Assert.Equal(".", Text(content[3]));
    }

    [Fact]
    public void MarksArePreservedOnLinkAndSurroundingText()
    {
        var content = Inlines.Linkify([
            new TextInline("Video: http://video.example/x now", TextMarks.Bold),
        ]);

        Assert.Equal(3, content.Count);
        Assert.Equal(TextMarks.Bold, ((TextInline)content[0]).Marks);
        Assert.Equal(TextMarks.Bold, ((TextInline)Link(content[1]).Content.Single()).Marks);
        Assert.Equal(TextMarks.Bold, ((TextInline)content[2]).Marks);
    }

    [Fact]
    public void ExistingLinksLineBreaksAndOtherTextAreUnchanged()
    {
        var existing = new HyperlinkInline(
            new Uri("https://example.com/"),
            [new TextInline("https://example.com/other")]
        );
        var content = Inlines.Linkify([
            existing,
            LineBreakInline.Instance,
            new TextInline("mailto:press@example.com www.example.com ftp://example.com https://"),
        ]);

        Assert.Same(existing, content[0]);
        Assert.Same(LineBreakInline.Instance, content[1]);
        Assert.Equal(
            "mailto:press@example.com www.example.com ftp://example.com https://",
            Text(Assert.Single(content.Skip(2)))
        );
    }
}
