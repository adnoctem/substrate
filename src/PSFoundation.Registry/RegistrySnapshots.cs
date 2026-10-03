using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace PSFoundation.Registry;

/// <summary>An immutable observation of one value, including distinct key and value absence.</summary>
public sealed class RegistryValueSnapshot
{
    public RegistryPath Path { get; }
    public string Name { get; }
    public RegistryView View { get; }
    public string? MachineName { get; }
    public bool KeyExists { get; }
    public bool Exists => Value != null;
    public RegistryValue? Value { get; }
    public RegistryValueSnapshot(RegistryPath path, string name, RegistryView view, bool keyExists, RegistryValue? value, string? machineName = null)
    {
        RegistryValueEntry.ValidateName(name);
        var endpoint = new RegistryManager(view, machineName);
        if (!keyExists && value != null)
            throw new ArgumentException("A missing key cannot contain a value.", nameof(value));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Name = name;
        View = endpoint.View;
        MachineName = endpoint.MachineName;
        KeyExists = keyExists;
        Value = value;
    }
    public bool SameValue(RegistryValueSnapshot other) => other != null && Equals(Value, other.Value);
}

/// <summary>An immutable key observation. Security descriptors are managed separately.</summary>
public sealed class RegistryKeySnapshot
{
    public RegistryPath Path { get; }
    public IReadOnlyList<RegistryValueEntry> Values { get; }
    public RegistryKeySnapshot(RegistryPath path, IEnumerable<RegistryValueEntry> values)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        var entries = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
        if (entries.Any(v => v == null) || entries.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new ArgumentException("Value names must be unique within a key.", nameof(values));
        Values = Array.AsReadOnly(entries.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}

/// <summary>A complete subtree observation, including empty keys. An empty Keys list records a missing root.</summary>
public sealed class RegistryTreeSnapshot
{
    public RegistryPath Root { get; }
    public RegistryView View { get; }
    public string? MachineName { get; }
    public bool Exists => Keys.Count != 0;
    public IReadOnlyList<RegistryKeySnapshot> Keys { get; }
    public RegistryTreeSnapshot(RegistryPath root, RegistryView view, IEnumerable<RegistryKeySnapshot> keys, string? machineName = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        var endpoint = new RegistryManager(view, machineName);
        View = endpoint.View;
        MachineName = endpoint.MachineName;
        var entries = (keys ?? throw new ArgumentNullException(nameof(keys))).ToArray();
        var addresses = new HashSet<RegistryPath>();
        foreach (var key in entries)
            if (key == null || (!key.Path.Equals(root) && !key.Path.IsDescendantOf(root)) || !addresses.Add(key.Path))
                throw new ArgumentException("Snapshot keys must be unique and remain within the root.", nameof(keys));
        if (entries.Length != 0 && (!addresses.Contains(root) || entries.Any(k => !k.Path.Equals(root) && !addresses.Contains(k.Path.Parent!))))
            throw new ArgumentException("A complete tree must include its root and every parent.", nameof(keys));
        Keys = Array.AsReadOnly(entries.OrderBy(k => k.Path.SubKey.Length).ThenBy(k => k.Path.SubKey, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>Explicitly maps data to another location. Does not read or write registry state.</summary>
    public RegistryTreeSnapshot At(RegistryPath destination, RegistryView view, string? machineName = null) => new RegistryTreeSnapshot(destination, view,
        Keys.Select(k => new RegistryKeySnapshot(k.Path.Equals(Root) ? destination : destination.Combine(k.Path.SubKey.Substring(Root.SubKey.Length).TrimStart('\\')), k.Values)), machineName);
}

public enum RegistryRestoreMode { Merge, Replace }
public enum RegistryChangeAction { CreateKey, SetValue, DeleteValue, DeleteKey }
public enum RegistryApplyStatus { Completed, Conflict, Failed, Cancelled }

public sealed class RegistryChange
{
    public RegistryChangeAction Action { get; }
    public RegistryPath Path { get; }
    public string? Name { get; }
    public RegistryValue? Before { get; }
    public RegistryValue? After { get; }
    internal RegistryChange(RegistryChangeAction action, RegistryPath path, string? name = null, RegistryValue? before = null, RegistryValue? after = null)
    { Action = action; Path = path; Name = name; Before = before; After = after; }
}

/// <summary>A reviewable, immutable plan. Its baseline is checked again when applied.</summary>
public sealed class RegistryChangePlan
{
    public RegistryTreeSnapshot Before { get; }
    public RegistryTreeSnapshot Desired { get; }
    public RegistryRestoreMode Mode { get; }
    public IReadOnlyList<RegistryChange> Changes { get; }
    internal RegistryChangePlan(RegistryTreeSnapshot before, RegistryTreeSnapshot desired, RegistryRestoreMode mode, IEnumerable<RegistryChange> changes)
    { Before = before; Desired = desired; Mode = mode; Changes = Array.AsReadOnly(changes.ToArray()); }
}

/// <summary>Records completed writes even when a later operation fails or cancellation is requested.</summary>
public sealed class RegistryApplyResult
{
    public RegistryApplyStatus Status { get; }
    public IReadOnlyList<RegistryChange> Completed { get; }
    public RegistryChange? IncompleteChange { get; }
    public Exception? Error { get; }
    internal RegistryApplyResult(RegistryApplyStatus status, IEnumerable<RegistryChange> completed, RegistryChange? incomplete = null, Exception? error = null)
    { Status = status; Completed = Array.AsReadOnly(completed.ToArray()); IncompleteChange = incomplete; Error = error; }
}
