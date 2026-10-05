using System;
using System.Linq;
using System.Threading.Tasks;
using AdNoctem.Substrate.Registry;
using Xunit;

namespace AdNoctem.Substrate.Windows.Tests;

public sealed class DotNetManagerTests
{
    [Fact]
    public async Task FrameworkInventoryUsesExplicitRegistryRootAndRetainsRawRegistrationEvidence()
    {
        var registry = new RegistryManager();
        var root = RegistryPath.Parse(@"HKCU\Software\AdNoctem.Substrate.Tests\DotNet-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manager = new DotNetManager(registry, root);
            Assert.Empty(manager.GetFrameworkRegistrations().Registrations);
            var full = root.Combine(@"v4\Full");
            registry.CreateKey(full);
            registry.SetValue(full, "Release", RegistryValue.DWord(533509));
            registry.SetValue(full, "Version", RegistryValue.String("4.8.09037"));
            registry.SetValue(full, "Install", RegistryValue.DWord(1));
            var inventory = await manager.GetFrameworkRegistrationsAsync();
            var registration = Assert.Single(inventory.Registrations);
            Assert.True(inventory.IsComplete);
            Assert.Equal(full, registration.Path);
            Assert.Equal(new Version(4, 8, 1), registration.MinimumVersion);
            Assert.True(registration.Installed);
            registry.SetValue(full, "Release", RegistryValue.DWord(394802));
            Assert.Equal((uint)533509, registration.Release);
            Assert.Equal(new Version(4, 6, 2), Assert.Single(manager.GetFrameworkRegistrations().Registrations).MinimumVersion);
        }
        finally { registry.DeleteKey(root, true); }
    }

    [Fact]
    public void CliListingsPreservePrereleaseVersionsPathsAndUnknownEvidence()
    {
        var listing = DotNetListing.Parse("Microsoft.NETCore.App 10.0.0-preview.1 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]\r\nunexpected output", DotNetComponentKind.Runtime);
        Assert.Equal("10.0.0-preview.1", Assert.Single(listing.Components).Version);
        Assert.Single(listing.UnrecognizedLines);
        var sdk = Assert.Single(DotNetListing.Parse("10.0.401 [C:\\Program Files\\dotnet\\sdk]", DotNetComponentKind.Sdk).Components);
        Assert.Null(sdk.Name);
        Assert.Equal(@"C:\Program Files\dotnet\sdk", sdk.BaseDirectory);
        Assert.Equal("10.0.401", sdk.Version);
    }
}
