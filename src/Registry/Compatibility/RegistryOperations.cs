using System;
using System.Collections;
using System.Linq;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry.Compatibility;

/// <summary>Legacy CRUD decisions, including comparison quirks and confirmation ordering.</summary>
internal sealed class RegistryOperations
{
    private readonly RegistryStore store;
    public RegistryOperations(RegistryStore store) => this.store = store;

    public string? Create(string input, LegacyRegistryPath path, Func<string, string, bool> authorize, Action<string> verbose)
    {
        if (path.SubKey.Length == 0)
            throw new InvalidOperationException($"Cannot create a root hive key: '{input}'");
        if (store.KeyExists(path))
        {
            verbose($"Registry key already exists: '{input}'");
            return "AlreadyExists";
        }
        if (!authorize(input, "Create registry key"))
            return null;
        store.CreateKey(path);
        verbose($"Created registry key: '{input}'");
        return "Created";
    }

    public string? Remove(string input, LegacyRegistryPath[] paths, bool recurse, Func<string, string, bool> authorize,
        Func<bool> authorizeChildren, Action<string> verbose)
    {
        if (paths.Any(p => p.SubKey.Length == 0))
            throw new InvalidOperationException($"Cannot remove a root hive key: '{input}'");
        paths = paths.Where(store.KeyExists).ToArray();
        if (paths.Length == 0)
        {
            verbose($"Registry key does not exist (nothing to remove): '{input}'");
            return "NotFound";
        }
        if (!authorize(input, "Remove registry key"))
            return null;
        foreach (var path in paths)
        {
            var children = store.SubKeys(path).Length != 0;
            if (!recurse && children && !authorizeChildren())
                continue;
            store.DeleteKey(path, recurse || children);
        }
        verbose($"Removed registry key: '{input}'");
        return "Removed";
    }

    public static RegistryValueKind InferKind(object? value)
    {
        if (value is int || value is long)
            return RegistryValueKind.DWord;
        if (value is string[])
            return RegistryValueKind.MultiString;
        if (value is byte[])
            return RegistryValueKind.Binary;
        return RegistryValueKind.String;
    }

    public static bool LegacyEquals(object? left, object? right, bool joinArrays = false)
    {
        if (left == null || right == null)
            return left == right;
        string Text(object item) => item.ToString() ?? "";
        string Join(object item) => item is IEnumerable array && !(item is string)
            ? string.Join("\0", array.Cast<object>().Select(Text)) : Text(item);
        if (joinArrays && (left is Array || right is Array) || left is string[] && right is string[])
            return string.Equals(Join(left), Join(right), StringComparison.OrdinalIgnoreCase);
        if (left is byte[] a && right is byte[] b)
            return a.SequenceEqual(b);
        return string.Equals(Text(left), Text(right), StringComparison.OrdinalIgnoreCase);
    }

    public string? Set(string input, LegacyRegistryPath path, LegacyRegistryPath[] paths, string name, object? value, RegistryValueKind? requestedKind,
        Func<RegistryValueKind, object> convertValue, Func<string, string, bool> authorize, Action<string> verbose, string displayValue,
        Func<string, bool>? namePattern = null)
    {
        var nativeName = string.Equals(name, "(default)", StringComparison.OrdinalIgnoreCase) ? "" : name;
        if (!paths.Any(store.KeyExists))
        {
            verbose($"Parent key does not exist, creating: '{input}'");
            if (Create(input, path, authorize, verbose) == null)
                throw new InvalidOperationException($"Failed to ensure registry key exists: '{input}'");
            paths = new[] { path };
        }
        var kind = requestedKind ?? InferKind(value);
        var exists = paths.All(p => namePattern == null ? store.Read(p, nativeName, RegistryView.Default).Exists : store.ValueNames(p).Any(namePattern));
        object? current = !exists || namePattern != null ? null : paths.Length == 1 ? store.GetValue(paths[0], nativeName) : paths.Select(p => store.GetValue(p, nativeName)).ToArray();
        var inferred = current is long ? RegistryValueKind.QWord : InferKind(current);
        var sameKind = inferred == kind;
        if (!sameKind && kind == RegistryValueKind.ExpandString)
            sameKind = store.Read(path, name, RegistryView.Default).Kind == kind;
        if (exists && sameKind && LegacyEquals(current, value))
        {
            verbose($"Registry value '{name}' already set to the requested data in '{input}'");
            return "Unchanged";
        }
        var action = exists ? "Update" : "Create";
        if (!authorize(input + "\\" + name, action + " registry value " + (exists ? "to" : "with") + " '" + displayValue + "'"))
            return null;
        var nativeValue = convertValue(kind);
        foreach (var resolved in paths)
            store.SetValue(resolved, nativeName, nativeValue, kind);
        verbose($"{(exists ? "Updated" : "Created")} registry value '{name}' in '{input}'");
        return exists ? "Updated" : "Created";
    }

    public string? RemoveValue(string input, LegacyRegistryPath[] paths, string name, Func<string, string, bool> authorize, Action<string> verbose, Func<string, bool>? namePattern = null)
    {
        paths = paths.Where(store.KeyExists).ToArray();
        if (paths.Length == 0)
        {
            verbose($"Registry key not found (nothing to remove): '{input}'");
            return "KeyNotFound";
        }
        var nativeName = string.Equals(name, "(default)", StringComparison.OrdinalIgnoreCase) ? "" : name;
        if (!paths.All(p => namePattern == null ? store.Read(p, nativeName, RegistryView.Default).Exists : store.ValueNames(p).Any(namePattern)))
        {
            verbose($"Registry value '{name}' does not exist in '{input}' (nothing to remove)");
            return "NotFound";
        }
        if (!authorize(input + "\\" + name, "Remove registry value"))
            return null;
        foreach (var path in paths)
            foreach (var valueName in namePattern == null ? new[] { nativeName } : store.ValueNames(path).Where(namePattern))
                store.DeleteValue(path, valueName);
        verbose($"Removed registry value '{name}' from '{input}'");
        return "Removed";
    }
}
