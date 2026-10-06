using System;
using System.IO;
using System.Linq;
using AdNoctem.Substrate.Packages;
using Xunit;

namespace AdNoctem.Substrate.Packages.Tests;

public sealed class PackageWorkflowTests
{
    [Fact]
    public void DeploymentSelectionPreservesExactIdentityAndProtectsFrameworks()
    {
        var package = Assert.Single(
            DismTool.ParseProvisionedPackages(
                "DisplayName : Example.App\r\nVersion : 1.2.3.4\r\nArchitecture : x64\r\nPackageName : Example.App_1.2.3.4_neutral_~_fixture\r\n"
            )
        );
        Assert.Equal(AppxPackageSource.Provisioned, package.Source);
        Assert.False(AppxPackageManager.AssessRemoval(package).Protected);
        Assert.True(
            AppxPackageManager
                .AssessRemoval(
                    new AppxPackageInfo(
                        AppxPackageSource.Installed,
                        "Example.Framework",
                        "Example.Framework_1_x64_fixture",
                        isFramework: true
                    )
                )
                .Protected
        );
        Assert.Throws<InvalidDataException>(() =>
            DismTool.ParseProvisionedPackages("DisplayName : Incomplete")
        );
        var request = new WinGetRequest(
            WinGetAction.Install,
            WinGetSelection.Id,
            "Example.App",
            "1.2.3",
            acceptAgreements: true
        );
        Assert.Contains("--exact", request.Arguments);
        Assert.Contains("--disable-interactivity", request.Arguments);
        Assert.Contains("--version", request.Arguments);
        Assert.Throws<ArgumentException>(() =>
            new WinGetRequest(WinGetAction.Uninstall, WinGetSelection.All)
        );
        Assert.Throws<ArgumentException>(() =>
            new WinGetRequest(WinGetAction.Install, WinGetSelection.Id, "--all")
        );
    }

    [Fact]
    public void ModuleCleanupKeepsNewestAndRefusesAnotherScope()
    {
        var root = Path.Combine(Path.GetTempPath(), "psf-module-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var modules = new[] { "1.2.0", "1.10.0" }
                .Select(v =>
                {
                    var path = Path.Combine(root, "Fixture", v);
                    Directory.CreateDirectory(path);
                    File.WriteAllText(Path.Combine(path, "Fixture.psm1"), "# fixture");

                    return new InstalledPowerShellModule("Fixture", Version.Parse(v), path);
                })
                .ToArray();
            var manager = new PowerShellModuleManager();
            var old = Assert.Single(manager.PlanRemoval(modules));
            Assert.Equal(new Version(1, 2, 0), old.Version);
            Assert.Throws<InvalidOperationException>(() =>
                manager.Remove(old, Path.Combine(root, "OtherScope"))
            );
            manager.Remove(old, root);
            Assert.False(Directory.Exists(old.DirectoryPath));
            Assert.True(Directory.Exists(modules[1].DirectoryPath));
            var manifest = Path.Combine(root, "restore.json");
            File.WriteAllText(
                manifest,
                "{\"Name\":\"Fixture\",\"Version\":\"1.10.0\",\"Scope\":\"CurrentUser\"}"
            );
            Assert.Equal("1.10.0", Assert.Single(manager.ReadRestoreManifest(manifest)).Version);
            File.WriteAllText(manifest, "[{\"Name\":\"../escape\"}]");
            Assert.Throws<ArgumentException>(() => manager.ReadRestoreManifest(manifest));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
