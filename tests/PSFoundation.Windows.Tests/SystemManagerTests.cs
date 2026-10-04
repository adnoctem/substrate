using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;
using PSFoundation.Interop;
using PSFoundation.IO;
using PSFoundation.Registry;
using PSFoundation.Windows;
using Xunit;

namespace PSFoundation.Windows.Tests;

public sealed class SystemManagerTests
{
    [Theory]
    [InlineData("Windows 10 Pro", 22000, "Windows 11 Pro")]
    [InlineData("windows 10 Enterprise", 21999, "windows 10 Enterprise")]
    [InlineData("Windows Server 2025", 26100, "Windows Server 2025")]
    [InlineData(null, 26100, null)]
    public void ProductNameUsesBuildWithoutRelabelingServers(string? input, int build, string? expected)
        => Assert.Equal(expected, WindowsVersionInfo.NormalizeProductName(input, build));

    [Fact]
    public void OptionalMetadataAndInstallDateRemainExplicit()
    {
        var empty = new WindowsVersionInfo(Array.Empty<RegistryValueEntry>());
        Assert.Null(empty.DisplayVersion);
        Assert.Null(empty.InstalledAt);
        Assert.Equal(0, empty.BuildNumber);
        var snapshot = new WindowsVersionInfo(new[] { new RegistryValueEntry("currentbuild", RegistryValue.String("26100")),
            new RegistryValueEntry("ProductName", RegistryValue.String("Windows 10 Pro")), new RegistryValueEntry("InstallDate", RegistryValue.DWord(1)) });
        Assert.Equal("Windows 11 Pro", snapshot.ProductName);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1), snapshot.InstalledAt);
        var bad = new WindowsVersionInfo(new[] { new RegistryValueEntry("InstallDate", RegistryValue.String("bad date")) });
        Assert.True(bad.InvalidInstallDate);
        Assert.Null(bad.InstalledAt);
    }

    [Fact]
    public void ApplicabilityHonorsExplicitZeroAndCaseInsensitiveEditions()
    {
        Assert.True(HostValidator.IsApplicable(26100, "Professional", true, 26100, 26100, new[] { "professional" }, true));
        Assert.False(HostValidator.IsApplicable(26100, "Professional", true, maximumBuild: 0));
        Assert.False(HostValidator.IsApplicable(26100, "Professional", true, require64Bit: false));
        Assert.True(HostValidator.IsApplicable(26100, null, true, editions: Array.Empty<string>()));
        Assert.False(HostValidator.IsApplicable(26100, "Professional", true, editions: new[] { "Enterprise" }));
    }

    [Fact]
    public void LiveLocalInventoryHasUsableDetachedValues()
    {
        var manager = new SystemManager();
        var os = manager.GetOperatingSystem();
        Assert.Equal(manager.GetBuildNumber(), os.BuildNumber);
        Assert.True(os.BuildNumber > 0);
        Assert.Equal(manager.GetEdition(), os.EditionId);
        var memory = manager.GetMemory();
        Assert.True(memory.TotalBytes > 0);
        Assert.Equal(memory.TotalBytes, memory.UsedBytes + memory.AvailableBytes);
        Assert.True(manager.GetUptime() > TimeSpan.Zero);
        Assert.Throws<ArgumentException>(() => new SystemManager(new RegistryManager(machineName: "synthetic-host")));
    }

    [Fact]
    public async Task NativeDiskAndHostnameReadsSupportCancellationAndDetachedResults()
    {
        var manager = new SystemManager();
        var token = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetDisksAsync(TimeSpan.FromSeconds(30), cancellationToken: token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetHostnameAsync(TimeSpan.FromSeconds(30), token));
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.GetDisks(TimeSpan.Zero));
        var disks = await manager.GetDisksAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(disks.Disks.Count, disks.Disks.Select(disk => disk.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(disks.Disks, disk => { Assert.Equal(DriveType.Fixed, disk.DriveType); Assert.True(disk.TotalBytes > 0); Assert.InRange(disk.PercentFree, 0, 100); });
        var hostname = await manager.GetHostnameAsync(TimeSpan.FromSeconds(30));
        Assert.False(string.IsNullOrWhiteSpace(hostname.Hostname));
    }

    [Fact]
    public void IdentityTranslationRoundTripsWithoutRetainingTokens()
    {
        var manager = new IdentityManager();
        var identity = manager.GetCurrentIdentity();
        Assert.Equal(identity.SecurityIdentifier, manager.GetSecurityIdentifier(identity.Name));
        Assert.Equal(identity.Name, manager.GetAccountName(identity.SecurityIdentifier!), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(identity.IsAdministrator, manager.IsElevated());
        Assert.Throws<ArgumentException>(() => manager.GetSecurityIdentifier(" "));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../escape")]
    [InlineData("name.")]
    [InlineData("C:\\escape")]
    public void ProductPathsRejectEscapes(string name)
    {
        var root = FileSystemPath.Parse(Path.GetTempPath());
        Assert.Throws<ArgumentException>(() => new ApplicationPaths(name, root, root, root, root));
    }

    [Fact]
    public void ProductPathsDoNotCreateDirectories()
    {
        var root = FileSystemPath.Parse(Path.GetTempPath());
        var name = "psf-path-" + Guid.NewGuid().ToString("N");
        var paths = new ApplicationPaths(name, root, root, root, root);
        Assert.Equal(Path.Combine(root.Value, name, "logs"), paths.Logs.Value);
        Assert.False(Directory.Exists(paths.Home.Value));
    }

    [Theory]
    [InlineData(0, true, "No Change")]
    [InlineData(7, true, "OKCOPY + MISMATCHES + XTRA")]
    [InlineData(8, false, "FAIL")]
    [InlineData(16, false, "***FATAL ERROR***")]
    [InlineData(-1, false, "Unknown")]
    [InlineData(32, false, "Unknown")]
    public void RobocopyFailuresAreNotTreatedAsOrdinaryExitCodes(int code, bool success, string description)
    { var result = new RobocopyExitResult(code); Assert.Equal(success, result.IsSuccess); Assert.Equal(description, result.Description); }

    [Fact]
    public void ComCleanupDoesNotDisposeOrdinaryManagedObjects()
    {
        using (var stream = new MemoryStream())
        {
            var manager = new ComManager();
            Assert.Null(manager.ReleaseReference(null));
            Assert.Null(manager.ReleaseReference(stream));
            stream.WriteByte(1);
            Assert.Equal(1, stream.Length);
        }
    }
}
