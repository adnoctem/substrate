using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PSFoundation.Packages;

public enum PowerShellModuleScope { CurrentUser, AllUsers }
/// <summary>An observed module version and installation directory, used as input to selection and removal planning.</summary>
public sealed class InstalledPowerShellModule
{
    public string Name { get; }
    public Version Version { get; }
    public string DirectoryPath { get; }
    public string Repository { get; }
    public DateTime? InstalledDate { get; }
    public InstalledPowerShellModule(string name, Version version, string directoryPath, string repository = "Unknown", DateTime? installedDate = null)
    {
        PowerShellModuleInstallRequest.ValidateName(name);
        Name = name;
        Version = version ?? throw new ArgumentNullException(nameof(version));
        DirectoryPath = Path.GetFullPath(directoryPath);
        Repository = repository;
        InstalledDate = installedDate;
    }
}
/// <summary>A repository-independent module request with explicit version constraints, scope, and replacement permission.</summary>
public sealed class PowerShellModuleInstallRequest
{
    public string Name { get; }
    public string? Version { get; }
    public string? MinimumVersion { get; }
    public PowerShellModuleScope Scope { get; }
    public bool Force { get; }
    public PowerShellModuleInstallRequest(string name, string? version = null, string? minimumVersion = null, PowerShellModuleScope scope = PowerShellModuleScope.CurrentUser, bool force = false)
    {
        ValidateName(name);
        if (!Enum.IsDefined(typeof(PowerShellModuleScope), scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(minimumVersion))
            throw new ArgumentException("Version and MinimumVersion are mutually exclusive.");
        foreach (var value in new[] { version, minimumVersion })
            if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, @"^\d+(?:\.\d+){1,3}(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                throw new ArgumentException("An exact module version is required.");
        Name = name;
        Version = string.IsNullOrEmpty(version) ? null : version;
        MinimumVersion = string.IsNullOrEmpty(minimumVersion) ? null : minimumVersion;
        Scope = scope;
        Force = force;
    }
    internal static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, @"^[A-Za-z0-9_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) || name.Contains("..")) throw new ArgumentException("An exact module name is required.", nameof(name)); }
}
/// <summary>Explicit repository boundary. Implementations own repository trust, package verification and dependency resolution.</summary>
/// <remarks>The library never bootstraps providers or silently trusts repositories. A PowerShell-host adapter is supplied in the PowerShell assembly;
/// other applications can supply their own repository without loading PowerShell into the domain library.</remarks>
public interface IPowerShellModuleRepository
{
    Task InstallAsync(PowerShellModuleInstallRequest request, CancellationToken cancellationToken = default);
}
/// <summary>Module version planning, bounded restore manifests and explicit, scope-contained filesystem removal.</summary>
public sealed class PowerShellModuleManager
{
    /// <summary>Filters observed modules with an optional name regular expression.</summary>
    public IReadOnlyList<InstalledPowerShellModule> Select(IEnumerable<InstalledPowerShellModule> modules, string? nameExpression = null)
    {
        if (modules == null)
            throw new ArgumentNullException(nameof(modules));
        var pattern = string.IsNullOrEmpty(nameExpression) ? null : new Regex(nameExpression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return Array.AsReadOnly(modules.Where(m => pattern == null || pattern.IsMatch(m.Name)).GroupBy(m => m.DirectoryPath, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Version.ToString(), StringComparer.OrdinalIgnoreCase).ToArray());
    }
    /// <summary>Selects older versions per module name, preserving the requested number unless removal of all versions was explicitly chosen.</summary>
    public IReadOnlyList<InstalledPowerShellModule> PlanRemoval(IEnumerable<InstalledPowerShellModule> modules, int latestToKeep = 1, bool all = false)
    {
        if (latestToKeep < 1)
            throw new ArgumentOutOfRangeException(nameof(latestToKeep));
        return Array.AsReadOnly(Select(modules).GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase).SelectMany(g => g.OrderByDescending(m => m.Version).Skip(all ? 0 : latestToKeep)).ToArray());
    }
    /// <summary>Deletes one installed module directory after validating containment and rejecting reparse points; removal is not transactional.</summary>
    /// <remarks>Removal is not transactional. The exact module directory must be under the supplied root, without reparse points.
    /// The caller must prevent concurrent writes to these directories; a filesystem check cannot authorize untrusted concurrent mutation.</remarks>
    public void Remove(InstalledPowerShellModule module, string scopeDirectory, CancellationToken cancellationToken = default)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));
        var root = Path.GetFullPath(scopeDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var path = module.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar);
        var expected = Path.Combine(root, module.Name);
        if (!string.Equals(path, expected, StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetDirectoryName(path), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Module removal must target its exact directory within the selected scope.");
        if (string.Equals(path, Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot remove a filesystem root.");
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Module removal does not traverse reparse points.");
        if (string.Equals(path, expected, StringComparison.OrdinalIgnoreCase) && Directory.EnumerateDirectories(path).Any(d => System.Version.TryParse(Path.GetFileName(d), out _)))
            throw new IOException("An unversioned module directory contains other versions; refusing to remove the parent.");
        var directories = new List<string>();
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            directories.Add(directory);
            foreach (var item in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(item);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Module removal does not follow reparse points.");
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(item);
            }
        }
        foreach (var directory in directories.AsEnumerable().Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var file in Directory.EnumerateFiles(directory))
            { cancellationToken.ThrowIfCancellationRequested(); File.Delete(file); }
            Directory.Delete(directory, false);
        }
    }
    /// <summary>Offloads module removal and checks cancellation between filesystem operations.</summary>
    public Task RemoveAsync(InstalledPowerShellModule module, string scopeDirectory, CancellationToken cancellationToken = default) => Task.Run(() => Remove(module, scopeDirectory, cancellationToken), cancellationToken);
    /// <summary>Delegates installation to a caller-supplied repository provider without selecting a host or repository policy.</summary>
    /// <remarks>The provider is borrowed. Repository trust, authentication, consent, and installation scope enforcement belong to that provider.</remarks>
    public Task InstallAsync(PowerShellModuleInstallRequest request, IPowerShellModuleRepository repository, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (repository == null)
            throw new ArgumentNullException(nameof(repository));
        cancellationToken.ThrowIfCancellationRequested();
        return repository.InstallAsync(request, cancellationToken);
    }
    /// <summary>Parses a bounded JSON restore manifest into explicit installation requests; does not install anything.</summary>
    public IReadOnlyList<PowerShellModuleInstallRequest> ReadRestoreManifest(string path, PowerShellModuleScope defaultScope = PowerShellModuleScope.CurrentUser, bool force = false)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length > 4 * 1024 * 1024)
                throw new InvalidDataException("The module manifest exceeds 4 MiB.");
            string json;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                json = reader.ReadToEnd().Trim();
            if (json.Length == 0 || json == "null")
                return Array.Empty<PowerShellModuleInstallRequest>();
            if (!json.StartsWith("[", StringComparison.Ordinal))
                json = "[" + json + "]";
            using (var data = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var records = (ManifestEntry[]?)new DataContractJsonSerializer(typeof(ManifestEntry[])).ReadObject(data) ?? Array.Empty<ManifestEntry>();
                return Array.AsReadOnly(records.Select(r => new PowerShellModuleInstallRequest(r.Name, r.Version, scope: string.IsNullOrEmpty(r.Scope) ? defaultScope : (PowerShellModuleScope)Enum.Parse(typeof(PowerShellModuleScope), r.Scope, true), force: force)).ToArray());
            }
        }
    }
    [DataContract]
    private sealed class ManifestEntry
    {
        [DataMember] public string Name { get; set; } = "";
        [DataMember] public string? Version { get; set; }
        [DataMember] public string? Scope { get; set; }
    }
}
