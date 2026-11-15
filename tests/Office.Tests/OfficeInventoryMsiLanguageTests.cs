using Xunit;

namespace AdNoctem.Substrate.Office.Tests;

public sealed class OfficeInventoryMsiLanguageTests
{
    [Fact]
    public void LanguageNeutralMsiProductCodeDoesNotThrowAndKeepsUnknownLanguage()
    {
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            new[]
            {
                OfficeInventoryTestData.Msi(
                    "{90140000-2005-0000-0000-0000000FF1CE}",
                    "Office File Validation Add-In"
                ),
            }
        );
        Assert.Null(Assert.Single(inventory.Msi).LanguageId);
    }

    [Fact]
    public void GermanMsiProductCodeKeepsItsLanguage()
    {
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            new[]
            {
                OfficeInventoryTestData.Msi(
                    "{90120000-0011-0407-0000-0000000FF1CE}",
                    "Microsoft Office Enterprise 2007"
                ),
            }
        );
        Assert.Equal("de-de", Assert.Single(inventory.Msi).LanguageId);
    }

    [Fact]
    public void UnknownPositiveMsiLanguageCodeDoesNotAbortClassification()
    {
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            new[]
            {
                OfficeInventoryTestData.Msi(
                    "{90140000-2005-1234-0000-0000000FF1CE}",
                    "Office Synthetic Component"
                ),
            }
        );
        Assert.Null(Assert.Single(inventory.Msi).LanguageId);
    }

    [Fact]
    public void InventoryWithLanguageNeutralMsiCodeStillReportsConfiguredProducts()
    {
        var records = OfficeInventoryTestData.Configured(null);
        records.Add(
            OfficeInventoryTestData.Msi(
                "{90140000-2005-0000-0000-0000000FF1CE}",
                "Office File Validation Add-In"
            )
        );
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            records
        );
        Assert.Equal(OfficeInventoryTestData.Product, Assert.Single(inventory.Products).ProductId);
        Assert.Null(Assert.Single(inventory.Msi).LanguageId);
        Assert.Empty(inventory.Unknowns);
    }

    [Fact]
    public void OperatingSystemLanguageParserAcceptsTheGermanLcid() =>
        Assert.Equal("de-de", OfficeRegistryReader.ParseOperatingSystemLanguage("0407"));

    [Theory]
    [InlineData("0000")]
    [InlineData("ffff")]
    [InlineData("not-hexadecimal")]
    public void OperatingSystemLanguageParserReportsInvalidLanguageEvidence(string text)
    {
        var error = Assert.Throws<OfficeException>(() =>
            OfficeRegistryReader.ParseOperatingSystemLanguage(text)
        );
        Assert.Equal(OfficeFailureReason.LocaleDiscoveryFailed, error.Reason);
    }
}
