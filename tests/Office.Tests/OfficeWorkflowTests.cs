using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Win32;
using AdNoctem.Substrate.Registry;
using Xunit;

namespace AdNoctem.Substrate.Office.Tests;

public sealed class OfficeWorkflowTests
{
    private const string Root = @"SOFTWARE\Microsoft\Office\ClickToRun\";
    private const string Active = "11111111-2222-3333-4444-555555555555";
    private const string Product = "Standard2019Volume";
    private const string Build = "16.0.10417.20095";
    private static OfficeRegistryRecord Record(string path, string[]? children = null, params string[] pairs)
    {
        var values = new Dictionary<string, RegistryValue>();
        for (var i = 0; i < pairs.Length; i += 2)
            values.Add(pairs[i], RegistryValue.String(pairs[i + 1]));
        return new OfficeRegistryRecord(RegistryView.Registry64, RegistryPath.Parse("HKLM\\" + path), values, children);
    }
    private static List<OfficeRegistryRecord> Registrations()
    {
        var productPath = Root + "ProductReleaseIDs\\" + Active + "\\" + Product + ".16";
        return new List<OfficeRegistryRecord>
        {
            Record(Root + "Configuration", null, "ProductReleaseIds", Product, "Platform", "x64", "CDNBaseUrl", "https://officecdn.microsoft.com/pr/f2e724c1-748f-4b47-8fb8-8e0d210e9208", "ClientCulture", "de-de", "VersionToReport", "16.0.99999.1", Product + ".ExcludedApps", "Groove,Teams"),
            Record(Root + "ProductReleaseIDs", new[] { Active }, "ActiveConfiguration", Active),
            Record(productPath, new[] { "de-de", "en-us", "x-none" }),
            Record(productPath + "\\de-de", null, "Version", Build), Record(productPath + "\\en-us", null, "Version", Build), Record(productPath + "\\x-none", null, "Version", Build)
        };
    }
    [Fact]
    public void InventoryLocaleAndComplianceRespectEvidenceAuthority()
    {
        var records = Registrations();
        var manager = new OfficeInventoryManager();
        var inventory = manager.Analyze(Active, records);
        var product = Assert.Single(inventory.Products);
        Assert.Equal(new Version(Build), product.Version);
        Assert.Equal(OfficeVersionSource.ActiveProductResources, product.InstalledVersion.Source);
        var locale = new OfficeLocaleResolver().FromInstalledOffice(inventory);
        Assert.Equal(new[] { "de-de", "en-us" }, locale.Languages);
        var target = new OfficeConfiguration(OfficeProduct.Standard2019Volume, languages: locale.Languages, version: new Version(Build), excludedApplications: new[] { OfficeApplication.Teams, OfficeApplication.Groove });
        var validator = new OfficeDeploymentValidator();
        Assert.True(validator.Evaluate(target, inventory).Compliant);
        Assert.Contains("Version", validator.Evaluate(new OfficeConfiguration(target.Product, languages: locale.Languages), inventory).Unknowns);
        records.Add(Record(Root + "Inventory\\Office\\16.0", null, "OfficeProductReleaseIds", Product, "OfficePackageVersion", "malformed"));
        var malformed = manager.Analyze(Active, records);
        Assert.Null(Assert.Single(malformed.Products).Version);
        Assert.Contains("Version", validator.Evaluate(target, malformed).Unknowns);
        records[records.Count - 1] = Record(Root + "Inventory\\Office\\16.0", null, "OfficeProductReleaseIds", "ProPlus2019Volume", "OfficePackageVersion", Build);
        var conflicting = manager.Analyze(Active, records);
        Assert.Contains("ConflictingInstalledProductIdentity", conflicting.Unknowns);
        Assert.Throws<OfficeException>(() => new OfficeLocaleResolver().FromInstalledOffice(conflicting));
        records[records.Count - 1] = Record(Root + "Inventory\\Office\\16.0", null, "OfficeProductReleaseIds", Product, "OfficePackageVersion", "16.0.12345.1");
        var documented = Assert.Single(manager.Analyze(Active, records).Products);
        Assert.Equal(OfficeVersionSource.ClickToRunInventory, documented.InstalledVersion.Source);
        Assert.Equal(new Version("16.0.12345.1"), documented.Version);
    }
    [Fact]
    public void PlanningAndXmlKeepRemovalConsentAndPreservationBoundaries()
    {
        var inventory = new OfficeInventoryManager().Analyze(Active, Registrations());
        var target = new OfficeConfiguration(OfficeProduct.Standard2019Volume, languages: new[] { "de-de", "en-us" }, version: new Version(Build),
            excludedApplications: new[] { OfficeApplication.Teams, OfficeApplication.Groove });
        var planner = new OfficeDeploymentPlanner();
        var compliant = planner.Create(new OfficeDeploymentRequest(OfficeDeploymentAction.Install, target), inventory);
        Assert.True(compliant.Eligible);
        Assert.Equal(OfficeDeploymentState.Compliant, compliant.State);
        Assert.Throws<OfficeException>(() => new OfficeDeploymentRequest(OfficeDeploymentAction.Update, target, removeMsi: true));
        var newTarget = new OfficeConfiguration(target.Product, languages: target.Languages, version: new Version("16.0.20000.1"), excludedApplications: target.ExcludedApplications);
        var update = planner.Create(new OfficeDeploymentRequest(OfficeDeploymentAction.Update, newTarget, @"C:\Media\Office"), inventory);
        Assert.Contains("UnverifiedMedia", update.Blockers);
        Assert.True(planner.Create(new OfficeDeploymentRequest(OfficeDeploymentAction.Update, newTarget, @"C:\Media\Office"), inventory, new OfficePlanningMedia(true, version: newTarget.Version)).Eligible);
        var removesPrimary = planner.Create(new OfficeDeploymentRequest(OfficeDeploymentAction.RemoveLanguage, target, languages: new[] { "de-de" }), inventory);
        Assert.Contains("PrimaryLanguageRequiresMigration", removesPrimary.Blockers);
        var builder = new OdtConfigurationBuilder();
        var install = builder.Install(target, @"C:\Media\Office & tools");
        Assert.Equal("FALSE", install.SelectSingleNode("/Configuration/Add/@AllowCdnFallback")!.Value);
        Assert.Equal("FALSE", install.SelectSingleNode("/Configuration/Property[@Name='FORCEAPPSHUTDOWN']/@Value")!.Value);
        Assert.Null(install.SelectSingleNode("/Configuration/RemoveMSI"));
        Assert.NotNull(builder.Migrate(target, @"C:\Media\Office", true).SelectSingleNode("/Configuration/RemoveMSI"));
        Assert.Throws<OfficeException>(() => builder.Install(new OfficeConfiguration(target.Product), @"C:\Media\Office"));
        Assert.Equal("FALSE", builder.RemoveProducts(new[] { "Standard2019Volume" }).SelectSingleNode("/Configuration/Remove/@All")!.Value);
        var preference = new OfficeApplicationPreference(RegistryPath.Parse(@"HKCU\software\microsoft\office\16.0\word\options"), "test", "<&>",
            OfficePreferenceValueType.String, OfficePreferenceApplication.Word, "synthetic");
        var xml = builder.SetApplicationPreferences(new[] { preference });
        Assert.Equal("<&>", xml.SelectSingleNode("/Configuration/AppSettings/User/@Value")!.Value);
        Assert.Null(xml.SelectSingleNode("/Configuration/Display"));
        Assert.Equal("False", builder.SetUpdateConfiguration(new OfficeUpdateConfiguration(enabled: false)).SelectSingleNode("/Configuration/Updates/@Enabled")!.Value);
    }
    [Fact]
    public void LegacyMsiAndUncertainAppPathsBlockUnsupportedPreservation()
    {
        var records = Registrations();
        var uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
        records.Add(Record(uninstall + "{90120000-0011-0407-0000-0000000FF1CE}", null, "Publisher", "Microsoft Corporation", "DisplayName", "Microsoft Office Enterprise 2007", "WindowsInstaller", "1"));
        records.Add(Record(uninstall + "{90160000-008F-0000-1000-0000000FF1CE}", null, "Publisher", "Microsoft Corporation", "DisplayName", "Office 16 Click-to-Run Extensibility Component"));
        var inventory = new OfficeInventoryManager().Analyze(Active, records);
        Assert.Equal("de-de", Assert.Single(inventory.Msi).LanguageId);
        Assert.Equal(OfficeRelatedRole.ClickToRunInfrastructure, Assert.Single(inventory.RelatedComponents).Role);
        Assert.Throws<OfficeException>(() => new OfficeLocaleResolver().FromInstalledOffice(inventory));
        Assert.Contains("OtherProducts", new OfficeDeploymentValidator().Evaluate(new OfficeConfiguration(OfficeProduct.Standard2019Volume), inventory).Discrepancies);
        var migration = new OfficeDeploymentPlanner().Create(new OfficeDeploymentRequest(OfficeDeploymentAction.Migrate, new OfficeConfiguration(OfficeProduct.Standard2019Volume)), inventory);
        Assert.Contains("MsiConsentRequired", migration.Blockers);
        Assert.Contains("UnapprovedProducts", migration.Blockers);
        var app = Record(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE", null, "", @"\\server\share\Office16\WINWORD.EXE");
        var evidence = new OfficeAppPathInspector().Inspect(app);
        Assert.Equal(OfficeAppPathState.Uncertain, evidence.State);
        Assert.Contains("OfficeResidueWithoutConfiguration", new OfficeInventoryManager().Analyze(Active, new[] { app }, new[] { evidence }).Unknowns);
        Assert.Throws<OperationCanceledException>(() => new OfficeInventoryManager().Read(new CancellationToken(true)));
        Assert.Throws<OperationCanceledException>(() => new OfficeActivationManager().GetStatus(OfficeProduct.Standard2019Volume, TimeSpan.FromSeconds(1), new CancellationToken(true)));
        Assert.Equal(OfficeActivationState.UserActivationRequired, new OfficeActivationManager().GetStatus(OfficeProduct.O365BusinessRetail, TimeSpan.FromSeconds(1)));
    }
}
