using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.Packages;

public enum ProgramRegistrationScope { Machine, CurrentUser, Other }

/// <summary>A registry location with an explicit view. Scope is caller-supplied descriptive metadata, not authority.</summary>
public sealed class ProgramRegistryLocation
{
    public RegistryPath Path { get; }
    public RegistryView View { get; }
    public ProgramRegistrationScope Scope { get; }
    public ProgramRegistryLocation(RegistryPath path, RegistryView view, ProgramRegistrationScope scope)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        View = new RegistryManager(view).View;
        if (!Enum.IsDefined(typeof(ProgramRegistrationScope), scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        Scope = scope;
    }
}

/// <summary>A detached uninstall registration, not proof that application files or a usable installation exist.</summary>
public sealed class Win32Program
{
    public ProgramRegistryLocation Location { get; }
    public string DisplayName { get; }
    public string? DisplayVersion => GetString("DisplayVersion");
    public string? Publisher => GetString("Publisher");
    public string? InstallLocation => GetString("InstallLocation");
    public string? UninstallCommand => GetString("UninstallString");
    public string? QuietUninstallCommand => GetString("QuietUninstallString");
    public string? ModifyCommand => GetString("ModifyPath");
    public uint? EstimatedSizeKiB => GetDWord("EstimatedSize");
    /// <summary>True for a nonzero DWORD marker. Other representations remain available in Values.</summary>
    public bool IsSystemComponent => GetDWord("SystemComponent").GetValueOrDefault() != 0;
    public DateTime? InstallDate => DateTime.TryParseExact(GetString("InstallDate"), "yyyyMMdd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var date) ? date : (DateTime?)null;
    /// <summary>Unexpanded known registration fields. Missing or wrongly typed optional text, size and date fields have null typed projections;
    /// raw values remain available here. Command strings are data and are never executed by inventory.</summary>
    public IReadOnlyDictionary<string, RegistryValue> Values { get; }

    internal Win32Program(ProgramRegistryLocation location, string displayName, IDictionary<string, RegistryValue> values)
    {
        Location = location;
        DisplayName = displayName;
        Values = new ReadOnlyDictionary<string, RegistryValue>(new Dictionary<string, RegistryValue>(values, StringComparer.OrdinalIgnoreCase));
    }
    private string? GetString(string name) => Values.TryGetValue(name, out var value) &&
        (value.Kind == RegistryValueKind.String || value.Kind == RegistryValueKind.ExpandString) ? value.GetString() : null;
    private uint? GetDWord(string name) => Values.TryGetValue(name, out var value) && value.Kind == RegistryValueKind.DWord
        ? unchecked((uint)value.GetData<int>()) : (uint?)null;
}

public sealed class ProgramInventoryError
{
    public ProgramRegistryLocation Location { get; }
    public Exception Exception { get; }
    internal ProgramInventoryError(ProgramRegistryLocation location, Exception exception) { Location = location; Exception = exception; }
}

/// <summary>Partial inventory is explicitly requested and retains every encountered read failure. Reads are not an atomic snapshot.</summary>
public sealed class Win32ProgramInventory
{
    public IReadOnlyList<Win32Program> Programs { get; }
    public IReadOnlyList<ProgramInventoryError> Errors { get; }
    public bool IsComplete => Errors.Count == 0;
    internal Win32ProgramInventory(List<Win32Program> programs, List<ProgramInventoryError> errors)
    { Programs = Array.AsReadOnly(programs.ToArray()); Errors = Array.AsReadOnly(errors.ToArray()); }
}
