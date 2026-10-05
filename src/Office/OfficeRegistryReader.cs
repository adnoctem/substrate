using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Office;

/// <summary>Detached typed values and child-key names observed at one registry path and view.</summary>
public sealed class OfficeRegistryRecord
{
    public RegistryView View { get; }
    public RegistryPath Path { get; }
    public IReadOnlyDictionary<string, RegistryValue> Values { get; }
    public IReadOnlyList<string> SubKeys { get; }
    public OfficeRegistryRecord(RegistryView view, RegistryPath path, IEnumerable<KeyValuePair<string, RegistryValue>> values, IEnumerable<string>? subKeys = null)
    {
        if (view != RegistryView.Registry32 && view != RegistryView.Registry64)
            throw new ArgumentOutOfRangeException(nameof(view));
        View = view;
        Path = path ?? throw new ArgumentNullException(nameof(path));
        var copied = new Dictionary<string, RegistryValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values ?? throw new ArgumentNullException(nameof(values)))
            copied.Add(pair.Key, pair.Value ?? throw new ArgumentException("Registry values cannot contain null.", nameof(values)));
        Values = new ReadOnlyDictionary<string, RegistryValue>(copied);
        var names = (subKeys ?? Array.Empty<string>()).ToArray();
        if (names.Any(string.IsNullOrEmpty))
            throw new ArgumentException("Subkeys cannot contain empty names.", nameof(subKeys));
        SubKeys = Array.AsReadOnly(names);
    }
    public object? GetValue(string name) => Values.TryGetValue(name, out var value) ? value.Data : null;
    public string? GetString(string name) => GetValue(name) as string;
}
/// <summary>A failed Office registry observation with its location and original exception.</summary>
public sealed class OfficeRegistryReadError
{
    public RegistryView View { get; }
    public Exception Error { get; }
    internal OfficeRegistryReadError(RegistryView view, Exception error) { View = view; Error = error; }
}
/// <summary>Office registry observations and retained failures across the probed views.</summary>
public sealed class OfficeRegistrySnapshot
{
    public string MachineId { get; }
    public IReadOnlyList<OfficeRegistryRecord> Records { get; }
    public IReadOnlyList<OfficeRegistryReadError> Errors { get; }
    public bool IsComplete => Errors.Count == 0;
    internal OfficeRegistrySnapshot(string machineId, List<OfficeRegistryRecord> records, List<OfficeRegistryReadError> errors)
    { MachineId = machineId; Records = records.AsReadOnly(); Errors = errors.AsReadOnly(); }
}

/// <summary>Read-only Office evidence through the shared registry API. Inventory values are allowlisted; product keys are never read.</summary>
public sealed class OfficeRegistryReader
{
    internal const string ConfigurationPath = @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration";
    internal const string InstalledPath = @"SOFTWARE\Microsoft\Office\ClickToRun\Inventory\Office\16.0";
    internal const string ResourceRoot = @"SOFTWARE\Microsoft\Office\ClickToRun\ProductReleaseIDs";
    internal const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private static readonly HashSet<string> Allowed = new HashSet<string>(new[]
    {
        "OfficeProductReleaseIds", "OfficePackageVersion", "ProductReleaseIds", "Platform", "VersionToReport", "CDNBaseUrl", "UpdateChannel", "ClientCulture",
        "InstallLanguage", "SKULanguage", "Language", "ActiveConfiguration", "DisplayName", "DisplayVersion", "Publisher", "WindowsInstaller", "UninstallString",
        "SystemComponent", "ParentKeyName", "ParentDisplayName"
    }, StringComparer.OrdinalIgnoreCase);
    /// <summary>Reads Office registration evidence from both registry views, optionally retaining per-location errors.</summary>
    public OfficeRegistrySnapshot Read(bool continueOnError = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var machineId = ReadMachineId();
        var records = new List<OfficeRegistryRecord>();
        var errors = new List<OfficeRegistryReadError>();
        foreach (var view in Environment.Is64BitOperatingSystem ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
        {
            try
            { records.AddRange(ReadView(new RegistryManager(view), cancellationToken)); }
            catch (Exception error) when (continueOnError && !(error is OperationCanceledException)) { errors.Add(new OfficeRegistryReadError(view, error)); }
        }
        return new OfficeRegistrySnapshot(machineId, records, errors);
    }
    /// <summary>Reads Office registry evidence on a worker thread, checking cancellation between operations.</summary>
    /// <remarks>Offloads synchronous registry reads and checks cancellation between them; it cannot interrupt an active registry call.</remarks>
    public Task<OfficeRegistrySnapshot> ReadAsync(bool continueOnError = false, CancellationToken cancellationToken = default)
        => Task.Run(() => Read(continueOnError, cancellationToken), cancellationToken);
    /// <summary>Reads the Windows machine identifier used to bind deployment and recovery evidence to one machine.</summary>
    public string ReadMachineId()
    {
        try
        {
            var manager = new RegistryManager(Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
            var path = RegistryPath.Parse(@"HKLM\SOFTWARE\Microsoft\Cryptography");
            if (!manager.KeyExists(path))
                throw new OfficeException(OfficeFailureReason.MachineIdentityUnavailable, "Machine identity key is missing in " + manager.View + ".");
            var identity = manager.GetValue(path, "MachineGuid")?.Data as string;
            if (identity == null || !Guid.TryParse(identity, out var parsed) || parsed == Guid.Empty)
                throw new OfficeException(OfficeFailureReason.InvalidMachineIdentity, "Machine identity (MachineGuid) is missing or invalid in " + manager.View + ".");
            return identity;
        }
        catch (OfficeException) { throw; }
        catch (Exception error) { throw new OfficeException(OfficeFailureReason.MachineIdentityUnavailable, "Cannot read the machine identity.", error); }
    }
    /// <summary>Reads the operating-system language used when deployment explicitly requests that language source.</summary>
    public string ReadOperatingSystemLanguage()
    {
        var value = new RegistryManager().GetValue(RegistryPath.Parse(@"HKLM\SYSTEM\CurrentControlSet\Control\Nls\Language"), "InstallLanguage");
        if (!(value?.Data is string text))
            throw new OfficeException(OfficeFailureReason.LocaleDiscoveryFailed, "The machine installation language is unavailable.");
        return CultureInfo.GetCultureInfo(int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).Name.ToLowerInvariant();
    }
    private static IEnumerable<OfficeRegistryRecord> ReadView(RegistryManager manager, CancellationToken cancellationToken)
    {
        var paths = new List<string> { ConfigurationPath, InstalledPath };
        paths.AddRange(new[] { "12.0", "14.0", "15.0", "16.0" }.Select(version => @"SOFTWARE\Microsoft\Office\" + version + @"\Common\LanguageResources"));
        paths.AddRange(new[] { "WINWORD.EXE", "EXCEL.EXE", "OUTLOOK.EXE" }.Select(name => @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + name));
        var level = new[] { ResourceRoot };
        for (var depth = 0; depth < 4; depth++)
        {
            var next = new List<string>();
            foreach (var entry in level)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = RegistryPath.Parse("HKLM\\" + entry);
                if (!manager.KeyExists(path))
                    continue;
                paths.Add(entry);
                next.AddRange(manager.GetSubKeys(path).Select(child => child.SubKey));
                if (next.Count > 4096)
                    throw new OfficeException(OfficeFailureReason.UnknownInventory, "Office resource registration exceeds supported discovery bounds.");
            }
            level = next.ToArray();
        }
        var uninstall = RegistryPath.Parse("HKLM\\" + UninstallRoot);
        if (manager.KeyExists(uninstall))
            paths.AddRange(manager.GetSubKeys(uninstall).Select(child => child.SubKey));
        foreach (var text in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = RegistryPath.Parse("HKLM\\" + text);
            if (!manager.KeyExists(path))
                continue;
            var values = new Dictionary<string, RegistryValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in manager.GetValueNames(path))
            {
                var selected = Allowed.Contains(name) || name.EndsWith(".ExcludedApps", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Version", StringComparison.OrdinalIgnoreCase) && text.StartsWith(ResourceRoot + "\\", StringComparison.OrdinalIgnoreCase)
                    || name.Length == 0 && text.IndexOf(@"\App Paths\", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!selected)
                    continue;
                cancellationToken.ThrowIfCancellationRequested();
                var value = manager.GetValue(path, name);
                if (value != null)
                    values.Add(name, value);
            }
            yield return new OfficeRegistryRecord(manager.View, path, values, manager.GetSubKeys(path).Select(child => child.SubKey.Substring(child.SubKey.LastIndexOf('\\') + 1)));
        }
    }
}
