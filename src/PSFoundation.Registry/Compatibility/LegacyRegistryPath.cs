using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PSFoundation.Registry.Compatibility;

/// <summary>Canonical registry identity independent of PowerShell providers.</summary>
internal sealed class LegacyRegistryPath
{
    private static readonly Dictionary<string, (RegistryHive Hive, string Provider)> Hives =
        new Dictionary<string, (RegistryHive, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["HKLM"] = (RegistryHive.LocalMachine, "HKLM:"),
            ["HKEY_LOCAL_MACHINE"] = (RegistryHive.LocalMachine, "HKLM:"),
            ["HKCU"] = (RegistryHive.CurrentUser, "HKCU:"),
            ["HKEY_CURRENT_USER"] = (RegistryHive.CurrentUser, "HKCU:"),
            ["HKCR"] = (RegistryHive.ClassesRoot, "Registry::HKEY_CLASSES_ROOT"),
            ["HKEY_CLASSES_ROOT"] = (RegistryHive.ClassesRoot, "Registry::HKEY_CLASSES_ROOT"),
            ["HKU"] = (RegistryHive.Users, "Registry::HKEY_USERS"),
            ["HKEY_USERS"] = (RegistryHive.Users, "Registry::HKEY_USERS"),
            ["HKCC"] = (RegistryHive.CurrentConfig, "Registry::HKEY_CURRENT_CONFIG"),
            ["HKEY_CURRENT_CONFIG"] = (RegistryHive.CurrentConfig, "Registry::HKEY_CURRENT_CONFIG")
        };

    public RegistryHive Hive { get; }
    public string SubKey { get; }
    public string ProviderPath { get; }

    private LegacyRegistryPath(RegistryHive hive, string subKey, string providerPath)
    {
        Hive = hive;
        SubKey = subKey;
        ProviderPath = providerPath;
    }

    public static LegacyRegistryPath Parse(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        var value = Regex.Replace(path, "^Registry::", "", RegexOptions.IgnoreCase);
        foreach (var entry in Hives)
        {
            if (!Regex.IsMatch(value, "^" + entry.Key + @"(?::|\\|$)", RegexOptions.IgnoreCase))
                continue;
            var rest = value.Substring(entry.Key.Length).TrimStart(':', '\\');
            var normalized = rest.Length == 0 ? entry.Value.Provider : entry.Value.Provider + "\\" + rest;
            normalized = Regex.Replace(normalized, @"\\+", @"\");
            if (normalized.Contains("\\"))
                normalized = Regex.Replace(normalized, ":+$", "");
            normalized = Regex.Replace(normalized, @"\\+$", "");
            var separator = normalized.IndexOf('\\');
            return new LegacyRegistryPath(entry.Value.Hive, separator < 0 ? "" : normalized.Substring(separator + 1), normalized);
        }
        throw new ArgumentException($"Unable to resolve registry hive from path: '{path}'", nameof(path));
    }
}
