using System;
using System.Linq;
using AdNoctem.Substrate.Registry;
using Xunit;

namespace AdNoctem.Substrate.Office.Tests;

public sealed class OfficeExclusionTests
{
    [Fact]
    public void AbsentExcludedAppsMeansAnEmptyObservedSet()
    {
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            OfficeInventoryTestData.Configured(null)
        );
        var product = Assert.Single(inventory.Products);
        Assert.NotNull(product.ExcludedApplications);
        Assert.Empty(product.ExcludedApplications!);
        Assert.Contains(
            product.Evidence,
            value => value.StartsWith("ExcludeApp none:", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void PresentExcludedAppsIsParsed()
    {
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            OfficeInventoryTestData.Configured(RegistryValue.String("OneDrive,OneNote"))
        );
        var product = Assert.Single(inventory.Products);
        Assert.Equal(new[] { "onedrive", "onenote" }, product.ExcludedApplications!);
        Assert.Contains(
            product.Evidence,
            value => value.StartsWith("ExcludeApp from ", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void NonStringExcludedAppsRemainsUnknown()
    {
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            OfficeInventoryTestData.Configured(RegistryValue.DWord(1))
        );
        var product = Assert.Single(inventory.Products);
        Assert.Null(product.ExcludedApplications);
        Assert.Contains(
            product.Evidence,
            value => value.StartsWith("ExcludeApp unknown:", StringComparison.Ordinal)
        );
        Assert.Contains(
            "ExcludeApp",
            new OfficeDeploymentValidator().Evaluate(Target(), inventory).Unknowns
        );
    }

    [Fact]
    public void AbsentExcludedAppsIsCompliantWhenNoApplicationsAreExcluded()
    {
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            OfficeInventoryTestData.Configured(null)
        );
        var assessment = new OfficeDeploymentValidator().Evaluate(Target(), inventory);
        Assert.True(assessment.Compliant, string.Join(", ", assessment.Unknowns));
        Assert.Empty(assessment.Unknowns);
        var plan = new OfficeDeploymentPlanner().Create(
            new OfficeDeploymentRequest(OfficeDeploymentAction.Install, Target()),
            inventory
        );
        Assert.Equal(OfficeDeploymentState.Compliant, plan.State);
        Assert.DoesNotContain("RecoveryRequired", plan.Blockers);
        Assert.True(plan.Eligible, string.Join(", ", plan.Blockers));
    }

    [Fact]
    public void StandardOfficeExclusionIdentifiersAreAcceptedAndWritten()
    {
        var target = new OfficeConfiguration(
            OfficeProduct.Standard2019Volume,
            languages: new[] { "de-de" },
            version: new Version(OfficeInventoryTestData.Build),
            excludedApplications: new[]
            {
                OfficeApplication.Groove,
                OfficeApplication.OneDrive,
                OfficeApplication.OneNote,
                OfficeApplication.Publisher,
            }
        );
        Assert.Equal(
            new[]
            {
                OfficeApplication.Groove,
                OfficeApplication.OneDrive,
                OfficeApplication.OneNote,
                OfficeApplication.Publisher,
            },
            target.ExcludedApplications
        );
        var xml = new OdtConfigurationBuilder().Install(target, @"C:\Synthetic\Media");
        var ids = xml.SelectNodes("/Configuration/Add/Product/ExcludeApp")!
            .Cast<System.Xml.XmlNode>()
            .Select(node => node.Attributes!["ID"]!.Value)
            .ToArray();
        Assert.Equal(new[] { "Groove", "OneDrive", "OneNote", "Publisher" }, ids);
        var inventory = new OfficeInventoryManager().Analyze(
            OfficeInventoryTestData.Machine,
            OfficeInventoryTestData.Configured(
                RegistryValue.String("Groove,OneDrive,OneNote,Publisher")
            )
        );
        Assert.Equal(
            new[] { "groove", "onedrive", "onenote", "publisher" },
            Assert.Single(inventory.Products).ExcludedApplications!
        );
        Assert.True(new OfficeDeploymentValidator().Evaluate(target, inventory).Compliant);
    }

    private static OfficeConfiguration Target() =>
        new OfficeConfiguration(
            OfficeProduct.Standard2019Volume,
            languages: new[] { "de-de" },
            version: new Version(OfficeInventoryTestData.Build)
        );
}
