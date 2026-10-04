using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;
using PSFoundation.Packages.Runtime;

namespace PSFoundation.Packages;

public enum AppxPackageSource { Installed, Provisioned }
public sealed class AppxPackageInfo
{
    public AppxPackageSource Source { get; }
    public string Name { get; }
    public string FullName { get; }
    public string? FamilyName { get; }
    public string? Publisher { get; }
    public string Version { get; }
    public string Architecture { get; }
    public string? InstallLocation { get; }
    public bool IsFramework { get; }
    public bool IsResource { get; }
    public bool IsBundle { get; }
    public bool NonRemovable { get; }
    public AppxPackageInfo(AppxPackageSource source, string name, string fullName, string? familyName = null, string? publisher = null, string version = "", string architecture = "",
        string? installLocation = null, bool isFramework = false, bool isResource = false, bool isBundle = false, bool nonRemovable = false)
    {
        if (!Enum.IsDefined(typeof(AppxPackageSource), source))
            throw new ArgumentOutOfRangeException(nameof(source));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A package name is required.", nameof(name));
        RequireIdentity(fullName);
        Source = source;
        Name = name;
        FullName = fullName;
        FamilyName = familyName;
        Publisher = publisher;
        Version = version;
        Architecture = architecture;
        InstallLocation = installLocation;
        IsFramework = isFramework;
        IsResource = isResource;
        IsBundle = isBundle;
        NonRemovable = nonRemovable;
    }
    internal static void RequireIdentity(string identity)
    { if (string.IsNullOrWhiteSpace(identity) || identity.IndexOfAny(new[] { '\0', '*', '?', '/', '\\' }) >= 0) throw new ArgumentException("An exact package identity is required.", nameof(identity)); }
}
public sealed class PackageRemovalAssessment
{
    public bool Protected => Reason != null;
    public string? Reason { get; }
    internal PackageRemovalAssessment(string? reason) => Reason = reason;
}
/// <summary>Native AppX operations through PSFoundation's isolated Framework WinRT host. No PowerShell runtime or implicit elevation is used.</summary>
/// <remarks>Windows validates package signatures. Cancellation asks WinRT to cancel and waits for its completion; it cannot undo committed changes.
/// Provisioning delegates to the system DISM executable and waits for a started servicing operation to finish.</remarks>
public sealed class AppxPackageManager
{
    private readonly PackageRuntimeClient runtime;
    public AppxPackageManager(FileSystemPath? runtimeHost = null) => runtime = new PackageRuntimeClient(runtimeHost);
    public IReadOnlyList<AppxPackageInfo> GetInstalled(bool allUsers = false, bool includeFramework = false, bool includeResource = false, bool includeBundle = false, CancellationToken cancellationToken = default)
        => Array.AsReadOnly(runtime.Run(new RuntimeRequest { Operation = "Inventory", AllUsers = allUsers }, cancellationToken).Packages
            .Where(package => (includeFramework || !package.IsFramework) && (includeResource || !package.IsResource) && (includeBundle || !package.IsBundle))
            .Select(package => new AppxPackageInfo(AppxPackageSource.Installed, package.Name, package.FullName, package.FamilyName, package.Publisher, package.Version, package.Architecture,
                package.InstallLocation, package.IsFramework, package.IsResource, package.IsBundle, package.NonRemovable)).ToArray());
    public Task<IReadOnlyList<AppxPackageInfo>> GetInstalledAsync(bool allUsers = false, bool includeFramework = false, bool includeResource = false, bool includeBundle = false, CancellationToken cancellationToken = default)
        => Task.Run(() => GetInstalled(allUsers, includeFramework, includeResource, includeBundle, cancellationToken), cancellationToken);
    public IReadOnlyList<AppxPackageInfo> GetProvisioned(CancellationToken cancellationToken = default) => new DismTool().GetProvisionedPackages(cancellationToken);
    public void Install(FileSystemPath package, IEnumerable<FileSystemPath>? dependencies = null, bool forceUpdateFromAnyVersion = false, CancellationToken cancellationToken = default)
        => runtime.Run(InstallRequest(package, dependencies, forceUpdateFromAnyVersion), cancellationToken);
    public async Task InstallAsync(FileSystemPath package, IEnumerable<FileSystemPath>? dependencies = null, bool forceUpdateFromAnyVersion = false, CancellationToken cancellationToken = default)
        => await runtime.RunAsync(InstallRequest(package, dependencies, forceUpdateFromAnyVersion), cancellationToken).ConfigureAwait(false);
    public void Provision(FileSystemPath package, IEnumerable<FileSystemPath>? dependencies = null, FileSystemPath? license = null, bool skipLicense = false, CancellationToken cancellationToken = default)
        => new DismTool().ProvisionPackage(package, dependencies, license, skipLicense, cancellationToken);
    public Task ProvisionAsync(FileSystemPath package, IEnumerable<FileSystemPath>? dependencies = null, FileSystemPath? license = null, bool skipLicense = false, CancellationToken cancellationToken = default)
    { var snapshot = dependencies?.ToArray(); return Task.Run(() => Provision(package, snapshot, license, skipLicense, cancellationToken), cancellationToken); }
    public void RegisterManifest(FileSystemPath manifest, CancellationToken cancellationToken = default)
        => runtime.Run(new RuntimeRequest { Operation = "Register", Target = (manifest ?? throw new ArgumentNullException(nameof(manifest))).Value }, cancellationToken);
    public Task RegisterManifestAsync(FileSystemPath manifest, CancellationToken cancellationToken = default)
        => runtime.RunAsync(new RuntimeRequest { Operation = "Register", Target = (manifest ?? throw new ArgumentNullException(nameof(manifest))).Value }, cancellationToken);
    public void ResetData(string fullName, CancellationToken cancellationToken = default)
    { AppxPackageInfo.RequireIdentity(fullName); runtime.Run(new RuntimeRequest { Operation = "Reset", Target = fullName }, cancellationToken); }
    public Task ResetDataAsync(string fullName, CancellationToken cancellationToken = default)
    { AppxPackageInfo.RequireIdentity(fullName); return runtime.RunAsync(new RuntimeRequest { Operation = "Reset", Target = fullName }, cancellationToken); }
    public void Remove(string fullName, AppxPackageSource source = AppxPackageSource.Installed, bool allUsers = false, bool allowProtected = false, CancellationToken cancellationToken = default)
    {
        AppxPackageInfo.RequireIdentity(fullName);
        if (!Enum.IsDefined(typeof(AppxPackageSource), source))
            throw new ArgumentOutOfRangeException(nameof(source));
        var inventory = source == AppxPackageSource.Installed ? GetInstalled(allUsers, true, true, true, cancellationToken) : GetProvisioned(cancellationToken);
        var package = inventory.SingleOrDefault(item => string.Equals(item.FullName, fullName, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("The selected package is no longer present.");
        var safety = AssessRemoval(package);
        if (safety.Protected && !allowProtected)
            throw new InvalidOperationException("Package removal is protected: " + safety.Reason);
        if (source == AppxPackageSource.Provisioned)
            new DismTool().RemoveProvisionedPackage(fullName, cancellationToken);
        else
            runtime.Run(new RuntimeRequest { Operation = "Remove", Target = fullName, AllUsers = allUsers }, cancellationToken);
    }
    public Task RemoveAsync(string fullName, AppxPackageSource source = AppxPackageSource.Installed, bool allUsers = false, bool allowProtected = false, CancellationToken cancellationToken = default)
        => Task.Run(() => Remove(fullName, source, allUsers, allowProtected, cancellationToken), cancellationToken);
    public static PackageRemovalAssessment AssessRemoval(AppxPackageInfo package)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        if (package.NonRemovable)
            return new PackageRemovalAssessment("NonRemovable");
        if (package.IsFramework)
            return new PackageRemovalAssessment("Framework");
        if (package.IsResource)
            return new PackageRemovalAssessment("ResourcePackage");
        var names = new[] { package.Name, package.FullName, package.FamilyName };
        foreach (var prefix in new[] { "Microsoft.DesktopAppInstaller", "Microsoft.StorePurchaseApp", "Microsoft.WindowsStore", "Microsoft.SecHealthUI", "Microsoft.UI.Xaml", "Microsoft.VCLibs", "Microsoft.NET.Native", "Microsoft.WindowsAppRuntime", "Microsoft.Services.Store.Engagement", "Microsoft.LockApp", "Microsoft.CredDialogHost", "Microsoft.ECApp" })
            if (names.Any(name => name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true))
                return new PackageRemovalAssessment("ProtectedPattern:" + prefix + "*");
        return new PackageRemovalAssessment(null);
    }
    private static RuntimeRequest InstallRequest(FileSystemPath package, IEnumerable<FileSystemPath>? dependencies, bool force)
        => new RuntimeRequest { Operation = "Install", Target = (package ?? throw new ArgumentNullException(nameof(package))).Value, Dependencies = (dependencies ?? Array.Empty<FileSystemPath>()).Select(path => path.Value).ToArray(), ForceUpdate = force };
}
