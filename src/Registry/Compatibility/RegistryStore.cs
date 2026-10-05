using System;
using System.Linq;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry.Compatibility;

/// <summary>Native registry access. All handles except those explicitly returned by LegacyRegistryReader are owned here.</summary>
internal sealed class RegistryStore : IRegistryStateStore
{
    private readonly LegacyRegistryReader reader = new LegacyRegistryReader();
    public bool KeyExists(LegacyRegistryPath path)
    {
        using (var key = reader.Open(path))
            return key != null;
    }

    public RegistryState Read(LegacyRegistryPath path, string name, RegistryView view)
    {
        using (var key = reader.Open(path, false, view))
        {
            var exists = key != null && key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase);
            return new RegistryState(path, name, view, exists, exists ? key!.GetValueKind(name) : (RegistryValueKind?)null,
                exists ? key!.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null, key != null);
        }
    }

    public void Apply(RegistryState desired)
    {
        if (desired.Exists)
        {
            using (var root = RegistryKey.OpenBaseKey(desired.Path.Hive, desired.View))
            using (var key = desired.Path.SubKey.Length == 0 ? null : root.CreateSubKey(desired.Path.SubKey))
                (key ?? root).SetValue(desired.Name, desired.Value!, desired.Kind!.Value);
        }
        else
        {
            using (var key = reader.Open(desired.Path, true, desired.View))
                key?.DeleteValue(desired.Name, false);
        }
    }

    public object? GetValue(LegacyRegistryPath path, string name)
    {
        using (var key = reader.Open(path))
        {
            var value = key?.GetValue(name);
            // The PowerShell registry provider exposes negative native integers as unsigned.
            if (value is int integer && integer < 0)
                return unchecked((uint)integer);
            if (value is long large && large < 0)
                return unchecked((ulong)large);
            return value;
        }
    }

    public string[] SubKeys(LegacyRegistryPath path)
    {
        using (var key = reader.Open(path))
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
    }

    public string[] ValueNames(LegacyRegistryPath path)
    {
        using (var key = reader.Open(path))
            return key?.GetValueNames() ?? Array.Empty<string>();
    }

    public void CreateKey(LegacyRegistryPath path)
    {
        if (path.SubKey.Length == 0)
            throw new InvalidOperationException("Cannot create a root hive key.");
        using (var root = RegistryKey.OpenBaseKey(path.Hive, RegistryView.Default))
        using (root.CreateSubKey(path.SubKey))
        {
        }
    }

    public void DeleteKey(LegacyRegistryPath path, bool recurse)
    {
        if (path.SubKey.Length == 0)
            throw new InvalidOperationException("Cannot remove a root hive key.");
        using (var root = RegistryKey.OpenBaseKey(path.Hive, RegistryView.Default))
        {
            if (recurse)
                root.DeleteSubKeyTree(path.SubKey, false);
            else
                root.DeleteSubKey(path.SubKey, false);
        }
    }

    public void SetValue(LegacyRegistryPath path, string name, object value, RegistryValueKind kind)
    {
        using (var key = reader.Open(path, true))
            key!.SetValue(name, value, kind);
    }

    public void DeleteValue(LegacyRegistryPath path, string name)
    {
        using (var key = reader.Open(path, true))
            key?.DeleteValue(name, false);
    }
}
