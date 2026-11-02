using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AdNoctem.Substrate.Documents.Tests;

// Documents saved by desktop Word 2019 from the synthetic template; see tests/fixtures/documents/README.md.
public sealed class WordSavedFixtureTests
{
    private static readonly PressReleaseSchema Schema = new PressReleaseSchema(
        ["en"],
        ["1.0"],
        ["example"]
    );

    private static PressReleaseReadResult Read(string name) =>
        new PressReleaseReader().Read(
            IO.FileSystemPath.Parse(
                Path.Combine(AppContext.BaseDirectory, "fixtures", name + ".docx")
            ),
            Schema
        );

    private static string Plain(DocumentBlock block) =>
        Inlines.ToPlainText(((ParagraphBlock)block).Content);

    [Fact]
    public void CompletedDocumentKeepsWordAuthoredContent()
    {
        var result = Read("complete");
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        var release = result.Release!;

        Assert.Equal("Word headline – „Größe“ 37″", release.Headline);
        Assert.Equal("Sampletown, November 2026", release.Dateline);
        var lead = (ParagraphBlock)release.Body[0];
        Assert.Contains(
            lead.Content,
            item => item is TextInline { Text: "bold", Marks: TextMarks.Bold }
        );
        Assert.Contains(
            lead.Content,
            item => item is TextInline { Text: "italic", Marks: TextMarks.Italic }
        );
        var bullets = (ListBlock)release.Body[1];
        Assert.Equal(ListKind.Bullet, bullets.Kind);
        Assert.Equal(
            new[] { "Bullet one", "Bullet two" },
            bullets.Items.Select(item => Inlines.ToPlainText(item.Content))
        );
        var numbers = (ListBlock)release.Body[2];
        Assert.Equal(ListKind.Decimal, numbers.Kind);
        Assert.Equal(
            "Level two",
            Inlines.ToPlainText(numbers.Items[1].Sublist!.Items.Single().Content)
        );
        var link = ((ParagraphBlock)release.Body[3]).Content.OfType<HyperlinkInline>().Single();
        Assert.Equal("https://example.com/de/video?id=1&lang=de", link.Destination.OriginalString);
        Assert.Equal("example site", Inlines.ToPlainText(link.Content));
        Assert.Equal(
            new[] { "Size:|275/70 R18", "Load:|2,000 kg", "Speed:|180 km/h" },
            release.Specifications!.Rows.Select(row =>
                Inlines.ToPlainText(row.Label) + "|" + Inlines.ToPlainText(row.Value)
            )
        );
        Assert.Equal("WHY IT MATTERS", release.FactBox!.Heading);
        Assert.Equal(
            "About Example: Default wording.",
            Plain(release.CompanyDescriptions.Single().Blocks[0])
        );
        Assert.Equal("Free for editorial use.", Plain(release.EditorialNote![0]));
        Assert.Equal("Contact:", release.Contact.Heading);
    }

    [Fact]
    public void WholeBlockDeletionOmitsSections()
    {
        var result = Read("deleted-optional");
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Null(result.Release!.Specifications);
        Assert.Null(result.Release.FactBox);
        Assert.Null(result.Release.EditorialNote);
        Assert.Empty(result.Release.CompanyDescriptions);
    }

    [Theory]
    [InlineData("untouched", DocumentDiagnosticCode.PlaceholderContent, PressReleaseTags.Headline)]
    [InlineData(
        "untouched",
        DocumentDiagnosticCode.IncompleteSection,
        PressReleaseTags.SpecificationsTable
    )]
    [InlineData("wrapper-removed", DocumentDiagnosticCode.UnboundContent, null)]
    [InlineData(
        "heading-only",
        DocumentDiagnosticCode.IncompleteSection,
        PressReleaseTags.FactBoxBody
    )]
    public void AuthoringMistakesAreDiagnosed(
        string name,
        DocumentDiagnosticCode code,
        string? field
    )
    {
        var result = Read(name);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == code && item.Field == field);
    }
}
