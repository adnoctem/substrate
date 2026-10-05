using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Windows;

/// <summary>A .NET Framework registration with its registry location, reported version, and release evidence.</summary>
public sealed class FrameworkRegistration
{
    public RegistryPath Path { get; }
    public RegistryView View { get; }
    public IReadOnlyDictionary<string, RegistryValue> Values { get; }
    public string? ReportedVersion => Value("Version")?.Data as string;
    public uint? Release => Number("Release");
    public uint? ServicePack => Number("SP");
    public bool? Installed => Number("Install") is uint number ? number != 0 : (bool?)null;
    /// <summary>The known minimum version for a release number, or the parsed pre-4.5 version. Future release numbers are not an exact version claim.</summary>
    public Version? MinimumVersion => Release.HasValue ? DotNetManager.GetMinimumFrameworkVersion(Release.Value)
        : Version.TryParse(ReportedVersion, out var version) ? version : null;
    internal FrameworkRegistration(RegistryPath path, RegistryView view, IEnumerable<RegistryValueEntry> values)
    {
        Path = path;
        View = view;
        Values = new ReadOnlyDictionary<string, RegistryValue>(values.ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase));
    }
    private RegistryValue? Value(string name) => Values.TryGetValue(name, out var value) ? value : null;
    private uint? Number(string name) => Value(name)?.Kind == RegistryValueKind.DWord ? unchecked((uint)Value(name)!.GetData<int>()) : (uint?)null;
}
/// <summary>A .NET Framework registration location that could not be inspected.</summary>
public sealed class FrameworkInventoryError
{
    public RegistryPath Path { get; }
    public Exception Error { get; }
    internal FrameworkInventoryError(RegistryPath path, Exception error) { Path = path; Error = error; }
}
/// <summary>Observed .NET Framework installations together with any retained read errors.</summary>
public sealed class FrameworkInventory
{
    public IReadOnlyList<FrameworkRegistration> Registrations { get; }
    public IReadOnlyList<FrameworkInventoryError> Errors { get; }
    public bool IsComplete => Errors.Count == 0;
    internal FrameworkInventory(IEnumerable<FrameworkRegistration> registrations, IEnumerable<FrameworkInventoryError> errors)
    { Registrations = Array.AsReadOnly(registrations.ToArray()); Errors = Array.AsReadOnly(errors.ToArray()); }
}

/// <summary>Reads .NET Framework registrations through the shared registry API. Registration is evidence, not a runtime health test.</summary>
public sealed class DotNetManager
{
    private readonly RegistryManager registry;
    public RegistryPath FrameworkRoot { get; }
    public DotNetManager(RegistryManager? registry = null, RegistryPath? frameworkRoot = null)
    {
        this.registry = registry ?? new RegistryManager();
        FrameworkRoot = frameworkRoot ?? RegistryPath.Parse(@"HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP");
    }
    /// <summary>Reads descendants of the configured NDP root, retaining raw fields and their registry kinds. Missing roots produce an empty inventory.</summary>
    public FrameworkInventory GetFrameworkRegistrations(bool continueOnError = false, CancellationToken cancellationToken = default)
    {
        var entries = new List<FrameworkRegistration>();
        var errors = new List<FrameworkInventoryError>();
        var pending = new Stack<RegistryPath>();
        pending.Push(FrameworkRoot);
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = pending.Pop();
            try
            {
                if (path.Equals(FrameworkRoot) && !registry.KeyExists(path))
                    continue;
                if (!path.Equals(FrameworkRoot))
                {
                    var values = registry.GetValues(path).Where(value => new[] { "Version", "Release", "Install", "SP" }.Contains(value.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
                    if (values.Length != 0)
                        entries.Add(new FrameworkRegistration(path, registry.View, values));
                }
                foreach (var child in registry.GetSubKeys(path).Reverse())
                    pending.Push(child);
            }
            catch (Exception error) when (continueOnError && (error is IOException || error is UnauthorizedAccessException || error is SecurityException))
            { errors.Add(new FrameworkInventoryError(path, error)); }
        }
        return new FrameworkInventory(entries, errors);
    }
    /// <remarks>Offloads synchronous registry traversal. Cancellation is checked between native calls.</remarks>
    public Task<FrameworkInventory> GetFrameworkRegistrationsAsync(bool continueOnError = false, CancellationToken cancellationToken = default)
        => Task.Run(() => GetFrameworkRegistrations(continueOnError, cancellationToken), cancellationToken);

    /// <summary>Uses Microsoft's minimum release thresholds for .NET Framework 4.5 through 4.8.1.</summary>
    public static Version? GetMinimumFrameworkVersion(uint release)
    {
        if (release >= 533320)
            return new Version(4, 8, 1);
        if (release >= 528040)
            return new Version(4, 8);
        if (release >= 461808)
            return new Version(4, 7, 2);
        if (release >= 461308)
            return new Version(4, 7, 1);
        if (release >= 460798)
            return new Version(4, 7);
        if (release >= 394802)
            return new Version(4, 6, 2);
        if (release >= 394254)
            return new Version(4, 6, 1);
        if (release >= 393295)
            return new Version(4, 6);
        if (release >= 379893)
            return new Version(4, 5, 2);
        if (release >= 378675)
            return new Version(4, 5, 1);
        if (release >= 378389)
            return new Version(4, 5);
        return null;
    }
}
