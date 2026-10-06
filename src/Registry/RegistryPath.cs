using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry;

/// <summary>A literal hive-relative key address. View and machine belong to the manager.</summary>
/// <remarks>Accepts hive names such as HKCU or HKEY_CURRENT_USER followed by a backslash and subkey path.
/// PowerShell provider syntax such as HKCU: is not supported. Wildcards and dot segments are literal key names;
/// comparison is case-insensitive. This type does not select a registry view or resolve filesystem paths.</remarks>
public sealed class RegistryPath : IEquatable<RegistryPath>
{
    private static readonly Dictionary<string, RegistryHive> Hives = new Dictionary<
        string,
        RegistryHive
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["HKLM"] = RegistryHive.LocalMachine,
        ["HKEY_LOCAL_MACHINE"] = RegistryHive.LocalMachine,
        ["HKCU"] = RegistryHive.CurrentUser,
        ["HKEY_CURRENT_USER"] = RegistryHive.CurrentUser,
        ["HKCR"] = RegistryHive.ClassesRoot,
        ["HKEY_CLASSES_ROOT"] = RegistryHive.ClassesRoot,
        ["HKU"] = RegistryHive.Users,
        ["HKEY_USERS"] = RegistryHive.Users,
        ["HKCC"] = RegistryHive.CurrentConfig,
        ["HKEY_CURRENT_CONFIG"] = RegistryHive.CurrentConfig,
    };

    public RegistryHive Hive { get; }
    public string SubKey { get; }
    public bool IsRoot => SubKey.Length == 0;
    public string Name => IsRoot ? HiveName : SubKey.Substring(SubKey.LastIndexOf('\\') + 1);
    public RegistryPath? Parent =>
        IsRoot
            ? null
            : new RegistryPath(
                Hive,
                SubKey.LastIndexOf('\\') < 0 ? "" : SubKey.Substring(0, SubKey.LastIndexOf('\\'))
            );
    public string HiveName =>
        Hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE"
        : Hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER"
        : Hive == RegistryHive.ClassesRoot ? "HKEY_CLASSES_ROOT"
        : Hive == RegistryHive.Users ? "HKEY_USERS"
        : "HKEY_CURRENT_CONFIG";

    public RegistryPath(RegistryHive hive, string subKey = "")
    {
        if (!Hives.ContainsValue(hive))
            throw new ArgumentOutOfRangeException(nameof(hive));

        if (subKey == null)
            throw new ArgumentNullException(nameof(subKey));

        if (subKey.IndexOf('\0') >= 0)
            throw new ArgumentException("Registry paths cannot contain NUL.", nameof(subKey));

        Hive = hive;
        SubKey = string.Join(
            "\\",
            subKey.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries)
        );

        foreach (var segment in SubKey.Split('\\'))
            if (segment.Length > 255)
                throw new ArgumentException(
                    "Registry key names cannot exceed 255 characters.",
                    nameof(subKey)
                );
    }

    public static RegistryPath Parse(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        var separator = path.IndexOf('\\');
        var root = separator < 0 ? path : path.Substring(0, separator);

        if (!Hives.TryGetValue(root, out var hive))
            throw new ArgumentException(
                "Expected a registry hive followed by a literal subkey path.",
                nameof(path)
            );

        return new RegistryPath(hive, separator < 0 ? "" : path.Substring(separator + 1));
    }

    public static bool TryParse(string? path, out RegistryPath? result)
    {
        try
        {
            result = Parse(path!);

            return true;
        }
        catch (ArgumentException)
        {
            result = null;

            return false;
        }
    }

    /// <summary>Appends a relative literal path. Dot segments are registry names, not filesystem navigation.</summary>
    public RegistryPath Combine(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath![0] == '\\')
            throw new ArgumentException(
                "A nonempty relative path is required.",
                nameof(relativePath)
            );

        return new RegistryPath(Hive, IsRoot ? relativePath : SubKey + "\\" + relativePath);
    }

    public bool IsDescendantOf(RegistryPath ancestor) =>
        ancestor != null
        && Hive == ancestor.Hive
        && !Equals(ancestor)
        && (
            ancestor.IsRoot
            || SubKey.StartsWith(ancestor.SubKey + "\\", StringComparison.OrdinalIgnoreCase)
        );

    public bool Equals(RegistryPath? other) =>
        other != null
        && Hive == other.Hive
        && StringComparer.OrdinalIgnoreCase.Equals(SubKey, other.SubKey);

    public override bool Equals(object? obj) => obj is RegistryPath other && Equals(other);

    public override int GetHashCode() =>
        ((int)Hive * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(SubKey);

    public override string ToString() => IsRoot ? HiveName : HiveName + "\\" + SubKey;
}
