using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry;

/// <summary>Host-independent registry access for one machine and resolved view. No shared open handles.</summary>
/// <remarks>
/// <para>Reads distinguish missing data from access failures. Mutations use the caller's existing Windows authority.
/// Multi-step operations are not transactions; inspect their results before assuming all requested changes completed.</para>
/// <para>The view is fixed at construction. Remote access uses the caller's credentials and supports only HKLM and HKU;
/// use an explicit SID for user data. Reachability, Remote Registry service configuration and permissions remain caller concerns.
/// File/hive operations and notifications require a local manager.</para>
/// <para>Value names are literal and case-insensitive. An empty name selects the default value; "(default)" is an ordinary name.
/// String data comparisons remain case-sensitive. Async traversal offloads synchronous native calls and checks cancellation between
/// operations; it cannot interrupt an individual registry call or remote RPC.</para>
/// </remarks>
/// <example><code>
/// var registry = new RegistryManager(RegistryView.Registry64);
/// var path = RegistryPath.Parse(@"HKCU\Software\ExampleApplication");
/// registry.CreateKey(path);
/// registry.SetValue(path, "Enabled", RegistryValue.DWord(1));
/// registry.SetValue(path, "Cache", RegistryValue.ExpandString(@"%LOCALAPPDATA%\ExampleApplication"));
/// if (registry.TryGetValue(path, "Cache", out var value))
/// {
///     var raw = value!.GetString();
///     var expanded = value.GetString(expandEnvironmentVariables: true);
/// }
/// </code></example>
public sealed class RegistryManager
{
    /// <summary>The concrete view resolved at construction, including when the caller requested Default.</summary>
    public RegistryView View { get; }
    public string? MachineName { get; }
    public RegistrySnapshotService Snapshots => new RegistrySnapshotService(this);
    public RegistryFileService Files => new RegistryFileService(this);
    public RegistryHiveService Hives => new RegistryHiveService(this);
    public RegistrySecurityService Security => new RegistrySecurityService(this);

    /// <param name="view">Default resolves once to the calling process architecture.</param>
    /// <param name="machineName">Null for local access; a remote host name uses the caller's credentials.</param>
    public RegistryManager(RegistryView view = RegistryView.Default, string? machineName = null)
    {
        if (!Enum.IsDefined(typeof(RegistryView), view))
            throw new ArgumentOutOfRangeException(nameof(view));

        if (
            machineName != null
            && (
                string.IsNullOrWhiteSpace(machineName)
                || machineName.IndexOfAny(new[] { '\\', '/', '\0' }) >= 0
            )
        )
            throw new ArgumentException(
                "Use a machine name without path separators.",
                nameof(machineName)
            );

        View =
            view == RegistryView.Default
                ? (Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32)
                : view;
        MachineName = machineName;
    }

    internal RegistryKey OpenBase(RegistryHive hive)
    {
        if (MachineName != null && hive != RegistryHive.LocalMachine && hive != RegistryHive.Users)
            throw new NotSupportedException(
                "Remote access supports HKEY_LOCAL_MACHINE and HKEY_USERS. Use an explicit user SID under HKEY_USERS."
            );

        return MachineName == null
            ? RegistryKey.OpenBaseKey(hive, View)
            : RegistryKey.OpenRemoteBaseKey(hive, MachineName, View);
    }

    /// <summary>Returns a caller-owned handle, or null only when the key is missing. Access errors propagate.</summary>
    public RegistryKey? OpenKey(RegistryPath path, bool writable = false)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        var root = OpenBase(path.Hive);

        if (path.IsRoot)
            return root;

        using (root)
            return root.OpenSubKey(path.SubKey, writable);
    }

    internal RegistryKey RequireKey(RegistryPath path, bool writable = false) =>
        OpenKey(path, writable) ?? throw new IOException("Registry key does not exist: " + path);

    /// <summary>Tests whether a key can be opened. Access failures propagate instead of being reported as absence.</summary>
    public bool KeyExists(RegistryPath path)
    {
        using (var key = OpenKey(path))
            return key != null;
    }

    /// <summary>Creates missing ancestors. Returns false if the key was already present when checked.</summary>
    public bool CreateKey(RegistryPath path)
    {
        RequireNonRoot(path);

        if (KeyExists(path))
            return false;

        using (var root = OpenBase(path.Hive))
        using (root.CreateSubKey(path.SubKey)) { }

        return true;
    }

    /// <summary>Deletes a key. Recursion is opt-in; missing keys return false and access errors propagate.</summary>
    public bool DeleteKey(RegistryPath path, bool recursive = false)
    {
        RequireNonRoot(path);

        if (!KeyExists(path))
            return false;

        using (var root = OpenBase(path.Hive))
            if (recursive)
                root.DeleteSubKeyTree(path.SubKey, false);
            else
                root.DeleteSubKey(path.SubKey, false);

        return true;
    }

    /// <summary>Returns immediate children; missing parents throw rather than appearing empty.</summary>
    public IReadOnlyList<RegistryPath> GetSubKeys(RegistryPath path)
    {
        using (var key = RequireKey(path))
            return Array.AsReadOnly(
                key.GetSubKeyNames()
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .Select(path.Combine)
                    .ToArray()
            );
    }

    /// <summary>Raw read without environment expansion. Null means missing key or value, never access denied.</summary>
    public RegistryValue? GetValue(RegistryPath path, string name = "")
    {
        RegistryValueEntry.ValidateName(name);

        using (var key = OpenKey(path))
            return key == null ? null : ReadValue(key, name);
    }

    /// <summary>Returns false only for a missing key or value; unsupported data and access errors propagate.</summary>
    public bool TryGetValue(RegistryPath path, string name, out RegistryValue? value)
    {
        value = GetValue(path, name);

        return value != null;
    }

    /// <summary>Checks for a named value without interpreting its data. An empty name denotes the default value.</summary>
    public bool ValueExists(RegistryPath path, string name = "")
    {
        RegistryValueEntry.ValidateName(name);

        using (var key = OpenKey(path))
            return key != null
                && key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Lists value names without reading their contents, for callers that must select an allowlist before collecting data.</summary>
    public IReadOnlyList<string> GetValueNames(RegistryPath path)
    {
        using (var key = RequireKey(path))
            return Array.AsReadOnly(key.GetValueNames());
    }

    /// <summary>Reads typed values in name order. A value disappearing during enumeration causes an error.</summary>
    public IReadOnlyList<RegistryValueEntry> GetValues(RegistryPath path)
    {
        using (var key = RequireKey(path))
        {
            var values = new List<RegistryValueEntry>();

            foreach (
                var name in key.GetValueNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            )
            {
                var value = ReadValue(key, name);

                if (value == null)
                    throw new IOException("Registry value disappeared during enumeration: " + name);

                values.Add(new RegistryValueEntry(name, value));
            }

            return values.AsReadOnly();
        }
    }

    internal static RegistryValue? ReadValue(RegistryKey key, string name)
    {
        var missing = new object();
        var data = key.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames);

        if (ReferenceEquals(data, missing))
        {
            if (key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    "The registry value exists but its native representation cannot be read by this API."
                );

            return null;
        }

        var kind = key.GetValueKind(name);

        if (kind == RegistryValueKind.Unknown)
            throw new NotSupportedException("The registry value has an unsupported native kind.");

        try
        {
            return new RegistryValue(kind, data!);
        }
        catch (ArgumentException error)
        {
            throw new IOException(
                "Registry data is malformed or changed kind while it was being read.",
                error
            );
        }
    }

    /// <summary>Writes explicit data and kind. Creating a missing key is opt-in.</summary>
    public void SetValue(
        RegistryPath path,
        string name,
        RegistryValue value,
        bool createKey = false
    )
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        RegistryValueEntry.ValidateName(name);

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (createKey && !path.IsRoot)
            CreateKey(path);

        using (var key = RequireKey(path, true))
            key.SetValue(name, value.Data, value.Kind);
    }

    /// <summary>Deletes one value and returns whether it was present; never deletes the containing key.</summary>
    public bool DeleteValue(RegistryPath path, string name = "")
    {
        RegistryValueEntry.ValidateName(name);

        using (var key = OpenKey(path, true))
        {
            if (
                key == null
                || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)
            )
                return false;

            key.DeleteValue(name, false);

            return true;
        }
    }

    /// <summary>Depth-first traversal including the root at depth zero. No snapshot isolation is implied.</summary>
    public IEnumerable<RegistryPath> EnumerateKeys(
        RegistryPath root,
        int maxDepth = int.MaxValue,
        CancellationToken cancellationToken = default
    )
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));

        var stack = new Stack<KeyValuePair<RegistryPath, int>>();
        stack.Push(new KeyValuePair<RegistryPath, int>(root, 0));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = stack.Pop();

            using (RequireKey(item.Key)) { }

            yield return item.Key;

            if (item.Value == maxDepth)
                continue;

            foreach (var child in GetSubKeys(item.Key).Reverse())
                stack.Push(new KeyValuePair<RegistryPath, int>(child, item.Value + 1));
        }
    }

    /// <summary>Searches literal substrings and returns structured matches. Binary data is matched as hexadecimal.</summary>
    public IReadOnlyList<RegistrySearchMatch> Search(
        RegistryPath root,
        string text,
        RegistrySearchOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        options = options ?? new RegistrySearchOptions();
        var matches = new List<RegistrySearchMatch>();
        bool Matches(string value) =>
            value.IndexOf(
                text,
                options.CaseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase
            ) >= 0;

        foreach (var path in EnumerateKeys(root, options.MaxDepth, cancellationToken))
        {
            if ((options.Targets & RegistrySearchTargets.KeyNames) != 0 && Matches(path.Name))
                matches.Add(new RegistrySearchMatch(path, null, RegistrySearchTargets.KeyNames));

            if (
                (
                    options.Targets
                    & (RegistrySearchTargets.ValueNames | RegistrySearchTargets.ValueData)
                ) == 0
            )
                continue;

            foreach (var entry in GetValues(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = (RegistrySearchTargets)0;

                if (
                    (options.Targets & RegistrySearchTargets.ValueNames) != 0
                    && Matches(entry.Name)
                )
                    target |= RegistrySearchTargets.ValueNames;

                if (
                    (options.Targets & RegistrySearchTargets.ValueData) != 0
                    && Matches(RegistrySearchMatch.Format(entry.Value))
                )
                    target |= RegistrySearchTargets.ValueData;

                if (target != 0)
                    matches.Add(new RegistrySearchMatch(path, entry, target));
            }
        }

        return matches.AsReadOnly();
    }

    /// <summary>Offloads synchronous Windows traversal; cancellation is checked between native operations.</summary>
    public Task<IReadOnlyList<RegistrySearchMatch>> SearchAsync(
        RegistryPath root,
        string text,
        RegistrySearchOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => Search(root, text, options, cancellationToken), cancellationToken);

    /// <summary>Renames within the same parent using the native registry operation. The target must not exist.</summary>
    public RegistryPath RenameKey(RegistryPath path, string newName)
    {
        RequireNonRoot(path);

        if (
            string.IsNullOrEmpty(newName)
            || newName.IndexOfAny(new[] { '\\', '\0' }) >= 0
            || newName.Length > 255
        )
            throw new ArgumentException("A single nonempty key name is required.", nameof(newName));

        var target = path.Parent!.Combine(newName);

        using (var key = RequireKey(path, true))
            NativeRegistry.ThrowIfError(NativeRegistry.RegRenameKey(key.Handle, null, newName));

        return target;
    }

    /// <summary>Creates a caller-owned local registry watcher. Dispose it to release the notification handle.</summary>
    public RegistryChangeWatcher Watch(
        RegistryPath path,
        bool includeSubKeys = false,
        RegistryChangeKinds kinds = RegistryChangeKinds.Names | RegistryChangeKinds.Values
    ) => new RegistryChangeWatcher(this, path, includeSubKeys, kinds);

    /// <summary>Copies data and empty keys. Destination permissions are inherited, not copied. Multi-step and non-atomic.</summary>
    /// <remarks>Overlapping trees are rejected. FailIfExists refuses an existing destination; Merge preserves unmentioned data;
    /// Replace removes unmentioned data inside the destination. Capture is not a point-in-time snapshot of a changing source.
    /// Cancellation during capture throws; cancellation during application returns completed writes without rollback.</remarks>
    public RegistryApplyResult CopyKey(
        RegistryPath source,
        RegistryPath destination,
        RegistryCopyMode mode = RegistryCopyMode.FailIfExists,
        CancellationToken cancellationToken = default
    )
    {
        ValidateTransfer(source, destination);

        if (!Enum.IsDefined(typeof(RegistryCopyMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        var original = Snapshots.CaptureTree(source, cancellationToken);

        if (!original.Exists)
            throw new IOException("The source key does not exist.");

        return Copy(original, destination, mode, cancellationToken);
    }

    /// <summary>Copies a subtree on a worker thread, retaining completed changes if application fails or is cancelled.</summary>
    public Task<RegistryApplyResult> CopyKeyAsync(
        RegistryPath source,
        RegistryPath destination,
        RegistryCopyMode mode = RegistryCopyMode.FailIfExists,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => CopyKey(source, destination, mode, cancellationToken));

    /// <summary>Copies data to a missing destination, then removes an unchanged source. Permissions are not transferred.</summary>
    /// <remarks>Overlapping trees are rejected. Inspect both copy and source-removal results; a completed copy does not establish
    /// successful removal. Use RenameKey for an in-place rename. No retry silently overwrites newer state.</remarks>
    public RegistryMoveResult MoveKey(
        RegistryPath source,
        RegistryPath destination,
        CancellationToken cancellationToken = default
    )
    {
        ValidateTransfer(source, destination);
        var original = Snapshots.CaptureTree(source, cancellationToken);

        if (!original.Exists)
            throw new IOException("The source key does not exist.");

        var copied = Copy(original, destination, RegistryCopyMode.FailIfExists, cancellationToken);

        if (copied.Status != RegistryApplyStatus.Completed)
            return new RegistryMoveResult(copied, null);

        // Recheck the complete source through Apply before removing anything from it.
        var absent = new RegistryTreeSnapshot(
            source,
            View,
            Array.Empty<RegistryKeySnapshot>(),
            MachineName
        );
        var removed = Snapshots.Apply(
            RegistrySnapshotService.Compare(original, absent, RegistryRestoreMode.Replace),
            cancellationToken
        );

        return new RegistryMoveResult(copied, removed);
    }

    /// <summary>Copies and then conditionally removes a subtree on a worker thread. Inspect both phases before treating the move as complete.</summary>
    public Task<RegistryMoveResult> MoveKeyAsync(
        RegistryPath source,
        RegistryPath destination,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => MoveKey(source, destination, cancellationToken));

    private RegistryApplyResult Copy(
        RegistryTreeSnapshot original,
        RegistryPath destination,
        RegistryCopyMode mode,
        CancellationToken cancellation
    )
    {
        var before = Snapshots.CaptureTree(destination, cancellation);

        if (mode == RegistryCopyMode.FailIfExists && before.Exists)
            throw new IOException("The destination key already exists.");

        var plan = RegistrySnapshotService.Compare(
            before,
            original.At(destination, View, MachineName),
            mode == RegistryCopyMode.Merge ? RegistryRestoreMode.Merge : RegistryRestoreMode.Replace
        );

        return Snapshots.Apply(plan, cancellation);
    }

    private static void ValidateTransfer(RegistryPath source, RegistryPath destination)
    {
        RequireNonRoot(source);
        RequireNonRoot(destination);

        if (
            source.Equals(destination)
            || source.IsDescendantOf(destination)
            || destination.IsDescendantOf(source)
        )
            throw new ArgumentException("Source and destination trees must not overlap.");
    }

    internal static void RequireNonRoot(RegistryPath path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        if (path.IsRoot)
            throw new InvalidOperationException(
                "This operation requires a subkey, not a root hive."
            );
    }
}

public enum RegistryCopyMode
{
    FailIfExists,
    Merge,
    Replace,
}

/// <summary>Separate copy and removal outcomes; a completed copy does not imply that the source was removed.</summary>
public sealed class RegistryMoveResult
{
    public RegistryApplyResult Copy { get; }
    public RegistryApplyResult? Removal { get; }
    public bool SourceRemoved => Removal?.Status == RegistryApplyStatus.Completed;

    internal RegistryMoveResult(RegistryApplyResult copy, RegistryApplyResult? removal)
    {
        Copy = copy;
        Removal = removal;
    }
}
