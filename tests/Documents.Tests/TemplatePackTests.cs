using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AdNoctem.Substrate.Documents.Tests;

public sealed class TemplatePackTests
{
    private static DocumentException Failure(Action action, DocumentFailureReason reason)
    {
        var exception = Assert.Throws<DocumentException>(action);
        Assert.Equal(reason, exception.Reason);
        return exception;
    }

    [Fact]
    public void LoadsManifestAssetsAndCompiledTemplates()
    {
        using var fixture = new TemplatePackFixture();
        var pack = fixture.Load();

        Assert.Equal("1.0", pack.Version);
        Assert.Equal("1.0", pack.OutputLayoutVersion);
        Assert.Equal(new[] { "de", "en" }, pack.Languages.Keys.OrderBy(item => item));
        Assert.Equal(new[] { "1.0" }, pack.Schema.TemplateVersions);
        Assert.Equal(new[] { "example" }, pack.Schema.CompanyDescriptionIds);
        Assert.Equal("PRESS RELEASE", pack.Languages["en"].Masthead);
        Assert.Equal("PRESSEMITTEILUNG", pack.Languages["de"].Masthead);
        Assert.Contains(PressReleaseSectionKind.Contact, pack.Languages["en"].Sections);
        Assert.NotEmpty(pack.Languages["en"].Docx);
    }

    [Fact]
    public void RejectsMissingAssets()
    {
        using var fixture = new TemplatePackFixture();
        File.Delete(Path.Combine(fixture.Root, "release.en.html"));

        Failure(() => fixture.Load(), DocumentFailureReason.MissingTemplateAsset);
    }

    [Fact]
    public void RejectsHashMismatch()
    {
        using var fixture = new TemplatePackFixture();
        fixture.Write("release.en.html", TemplatePackFixture.DefaultHtml + " ");

        Failure(() => fixture.Load(), DocumentFailureReason.TemplateAssetHashMismatch);
    }

    [Fact]
    public void RejectsDocxHashMismatch()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteBytes("base.docx", [1, 2, 3]);

        Failure(() => fixture.Load(), DocumentFailureReason.TemplateAssetHashMismatch);
    }

    [Fact]
    public void RejectsPathTraversal()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteManifest(enHtml: "../outside.html");

        Failure(() => fixture.Load(), DocumentFailureReason.UnsafeTemplatePath);
    }

    [Fact]
    public void RejectsIncompatibleSchemaId()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteManifest(schema: "press-release/2");

        Failure(() => fixture.Load(), DocumentFailureReason.UnsupportedTemplateVersion);
    }

    [Fact]
    public void RejectsEmptySchemaVersions()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteManifest(schemaVersions: "[]");

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplatePack);
    }

    [Fact]
    public void RejectsLanguageWithoutContactSection()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteManifest(
            companyDescriptions: "null",
            enSections: "[\"specifications\",\"factBox\",\"media\",\"editorial\"]",
            deSections: "[\"specifications\",\"factBox\",\"media\",\"editorial\"]"
        );

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplatePack);
    }

    [Fact]
    public void RejectsDuplicateSectionMapping()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteManifest(
            companyDescriptions: "null",
            enSections: "[\"specifications\",\"specifications\",\"contact\"]",
            deSections: "[\"contact\"]"
        );

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplatePack);
    }

    [Fact]
    public void RejectsCompanyDescriptionsWithoutMapping()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteManifest(
            enSections: "[\"specifications\",\"factBox\",\"media\",\"editorial\",\"contact\"]",
            deSections: "[\"specifications\",\"factBox\",\"media\",\"editorial\",\"contact\"]"
        );

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplatePack);
    }

    [Fact]
    public void RejectsUnknownTemplateSlot()
    {
        using var fixture = new TemplatePackFixture();
        fixture.Write(
            "release.en.html",
            TemplatePackFixture.DefaultHtml.Replace("{{headline}}", "{{unknown}}")
        );
        fixture.WriteManifest();

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplate);
    }

    [Fact]
    public void RejectsMissingRequiredSlot()
    {
        using var fixture = new TemplatePackFixture();
        fixture.Write(
            "release.en.html",
            TemplatePackFixture.DefaultHtml.Replace("{{title}}", "Release")
        );
        fixture.WriteManifest();

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplate);
    }

    [Fact]
    public void RejectsDeclaredSectionWithoutTemplateRegion()
    {
        using var fixture = new TemplatePackFixture();
        fixture.Write(
            "release.en.html",
            TemplatePackFixture.DefaultHtml.Replace(
                "{{#factbox}}<h2>{{factbox.heading}}</h2>{{factbox.body}}{{/factbox}}",
                ""
            )
        );
        fixture.WriteManifest();

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplate);
    }

    [Fact]
    public void RejectsTemplateSectionNotDeclaredByPack()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteManifest(
            companyDescriptions: "null",
            enSections: "[\"contact\"]",
            deSections: "[\"contact\"]"
        );

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplate);
    }

    [Fact]
    public void RejectsNonUtf8Template()
    {
        using var fixture = new TemplatePackFixture();
        fixture.WriteBytes("release.en.html", [0xff, 0xfe, 0xfd]);
        fixture.WriteManifest();

        Failure(() => fixture.Load(), DocumentFailureReason.InvalidTemplate);
    }
}
