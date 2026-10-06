using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Registry;

/// <summary>Capture and restoration without prompts. Checks narrow races but do not create registry transactions.</summary>
/// <remarks>Snapshots retain raw values, empty keys, and resolved machine/view identity, but no security descriptors.
/// Callers requiring a consistent capture must arrange quiescence. Read-only cancellation throws; apply cancellation returns
/// a partial result. Completed writes are never rolled back automatically.</remarks>
/// <example><code>
/// var registry = new RegistryManager(Microsoft.Win32.RegistryView.Registry64);
/// var settings = RegistryPath.Parse(@"HKCU\Software\ExampleApplication");
/// var before = await registry.Snapshots.CaptureTreeAsync(settings, cancellationToken);
/// // Perform application-controlled changes, then review the restoration plan.
/// var plan = registry.Snapshots.PlanRestore(before, RegistryRestoreMode.Merge, cancellationToken);
/// foreach (var change in plan.Changes)
/// {
///     // Present Path, Action, Name, Before and After as appropriate to the application.
/// }
/// var result = await registry.Snapshots.ApplyAsync(plan, cancellationToken);
/// if (result.Status != RegistryApplyStatus.Completed)
/// {
///     // Inspect Completed, IncompleteChange and Error before deciding what to do next.
/// }
/// </code></example>
public sealed class RegistrySnapshotService
{
    private readonly RegistryManager manager;
    public RegistrySnapshotService(RegistryManager manager) => this.manager = manager ?? throw new ArgumentNullException(nameof(manager));

    /// <summary>Records key absence separately from value absence, together with the manager's machine and registry view.</summary>
    public RegistryValueSnapshot CaptureValue(RegistryPath path, string name = "")
    {
        RegistryValueEntry.ValidateName(name);
        using (var key = manager.OpenKey(path))
            return new RegistryValueSnapshot(path, name, manager.View, key != null, key == null ? null : RegistryManager.ReadValue(key, name), manager.MachineName);
    }

    /// <summary>Captures data and empty keys, not permissions. Concurrent changes can prevent a consistent observation.</summary>
    public RegistryTreeSnapshot CaptureTree(RegistryPath root, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = new List<RegistryKeySnapshot>();
        if (manager.KeyExists(root))
            foreach (var path in manager.EnumerateKeys(root, cancellationToken: cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                keys.Add(new RegistryKeySnapshot(path, manager.GetValues(path)));
            }
        return new RegistryTreeSnapshot(root, manager.View, keys, manager.MachineName);
    }

    /// <summary>Captures a subtree on a worker thread, checking cancellation between native registry reads.</summary>
    public Task<RegistryTreeSnapshot> CaptureTreeAsync(RegistryPath root, CancellationToken cancellationToken = default) =>
        Task.Run(() => CaptureTree(root, cancellationToken), cancellationToken);

    /// <summary>Reads current state and creates a reviewable restoration plan without applying it.</summary>
    public RegistryChangePlan PlanRestore(RegistryTreeSnapshot desired, RegistryRestoreMode mode = RegistryRestoreMode.Merge, CancellationToken cancellationToken = default)
    {
        if (desired == null)
            throw new ArgumentNullException(nameof(desired));
        ValidateEndpoint(desired.View, desired.MachineName);
        RegistryManager.RequireNonRoot(desired.Root);
        return Compare(CaptureTree(desired.Root, cancellationToken), desired, mode);
    }

    /// <summary>Pure comparison. Merge preserves unmentioned keys/values; Replace removes them.</summary>
    public static RegistryChangePlan Compare(RegistryTreeSnapshot before, RegistryTreeSnapshot desired, RegistryRestoreMode mode = RegistryRestoreMode.Merge)
    {
        if (before == null)
            throw new ArgumentNullException(nameof(before));
        if (desired == null)
            throw new ArgumentNullException(nameof(desired));
        if (!before.Root.Equals(desired.Root) || before.View != desired.View || !StringComparer.OrdinalIgnoreCase.Equals(before.MachineName, desired.MachineName))
            throw new ArgumentException("Snapshot locations must match. Use At to explicitly map a snapshot.");
        if (!Enum.IsDefined(typeof(RegistryRestoreMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        var changes = new List<RegistryChange>();
        var oldKeys = before.Keys.ToDictionary(k => k.Path);
        var newKeys = desired.Keys.ToDictionary(k => k.Path);
        foreach (var key in desired.Keys)
        {
            oldKeys.TryGetValue(key.Path, out var old);
            if (old == null)
                changes.Add(new RegistryChange(RegistryChangeAction.CreateKey, key.Path));
            foreach (var value in key.Values)
            {
                var original = old?.Values.FirstOrDefault(v => StringComparer.OrdinalIgnoreCase.Equals(v.Name, value.Name))?.Value;
                if (!Equals(original, value.Value))
                    changes.Add(new RegistryChange(RegistryChangeAction.SetValue, key.Path, value.Name, original, value.Value));
            }
        }
        if (mode == RegistryRestoreMode.Replace)
        {
            foreach (var key in before.Keys.Reverse())
            {
                newKeys.TryGetValue(key.Path, out var replacement);
                foreach (var value in key.Values)
                    if (replacement == null || !replacement.Values.Any(v => StringComparer.OrdinalIgnoreCase.Equals(v.Name, value.Name)))
                        changes.Add(new RegistryChange(RegistryChangeAction.DeleteValue, key.Path, value.Name, value.Value));
                if (replacement == null)
                    changes.Add(new RegistryChange(RegistryChangeAction.DeleteKey, key.Path));
            }
        }
        return new RegistryChangePlan(before, desired, mode, changes);
    }

    /// <summary>Stops on the first conflict/failure. Cancellation returns a result retaining completed changes.</summary>
    /// <remarks>Plans apply only to their original machine/view. The entire baseline is compared before writing, then each change
    /// is checked immediately before and verified after its write. New data encountered during deletion causes a conflict rather
    /// than recursive pruning. A write is recorded before verification, so verification failure does not imply nothing changed.
    /// Progress reports verified changes; the supplied IProgress implementation determines how notifications are delivered.</remarks>
    public RegistryApplyResult Apply(RegistryChangePlan plan, CancellationToken cancellationToken = default, IProgress<RegistryChange>? progress = null)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        ValidateEndpoint(plan.Before.View, plan.Before.MachineName);
        RegistryManager.RequireNonRoot(plan.Before.Root);
        var completed = new List<RegistryChange>();
        RegistryChange? current = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SameTree(plan.Before, CaptureTree(plan.Before.Root, cancellationToken)))
                return new RegistryApplyResult(RegistryApplyStatus.Conflict, completed);
            foreach (var change in plan.Changes)
            {
                current = change;
                cancellationToken.ThrowIfCancellationRequested();
                if (!MatchesBefore(change))
                    return new RegistryApplyResult(RegistryApplyStatus.Conflict, completed, change);
                switch (change.Action)
                {
                    case RegistryChangeAction.CreateKey:
                        manager.CreateKey(change.Path);
                        break;
                    case RegistryChangeAction.SetValue:
                        manager.SetValue(change.Path, change.Name!, change.After!);
                        break;
                    case RegistryChangeAction.DeleteValue:
                        manager.DeleteValue(change.Path, change.Name!);
                        break;
                    case RegistryChangeAction.DeleteKey:
                        manager.DeleteKey(change.Path);
                        break;
                }
                // Record the write before verifying; verification failure does not imply rollback.
                completed.Add(change);
                if (!MatchesAfter(change))
                    throw new IOException("Registry verification failed after " + change.Action + ".");
                progress?.Report(change);
            }
            return new RegistryApplyResult(RegistryApplyStatus.Completed, completed);
        }
        catch (OperationCanceledException error) { return new RegistryApplyResult(RegistryApplyStatus.Cancelled, completed, current, error); }
        catch (Exception error) when (IsOperational(error)) { return new RegistryApplyResult(RegistryApplyStatus.Failed, completed, current, error); }
    }

    /// <summary>Applies a reviewed plan on a worker thread. Cancellation is recorded in the result with completed writes.</summary>
    public Task<RegistryApplyResult> ApplyAsync(RegistryChangePlan plan, CancellationToken cancellationToken = default, IProgress<RegistryChange>? progress = null) =>
        Task.Run(() => Apply(plan, cancellationToken, progress));

    /// <summary>Restores only one value, preserving unrelated data. Expected state is optional; identity must match.</summary>
    /// <remarks>Restoring absence deletes only the value, retaining its key. Restoring a present value can recreate its parent;
    /// use a tree plan when each key creation needs a separate change record. Omitting expected state allows replacement of
    /// the currently observed value, with rechecks before writing but no atomic transaction.</remarks>
    public RegistryApplyResult RestoreValue(RegistryValueSnapshot desired, RegistryValueSnapshot? expected = null, CancellationToken cancellationToken = default)
    {
        if (desired == null)
            throw new ArgumentNullException(nameof(desired));
        ValidateEndpoint(desired.View, desired.MachineName);
        if (expected != null && (!expected.Path.Equals(desired.Path) || !StringComparer.OrdinalIgnoreCase.Equals(expected.Name, desired.Name)
            || expected.View != desired.View || !StringComparer.OrdinalIgnoreCase.Equals(expected.MachineName, desired.MachineName)))
            throw new ArgumentException("Expected and desired value identities must match.", nameof(expected));
        var completed = new List<RegistryChange>();
        RegistryChange? change = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = CaptureValue(desired.Path, desired.Name);
            if (expected != null && (before.KeyExists != expected.KeyExists || !before.SameValue(expected)))
                return new RegistryApplyResult(RegistryApplyStatus.Conflict, completed);
            if (before.SameValue(desired))
                return new RegistryApplyResult(RegistryApplyStatus.Completed, completed);
            change = new RegistryChange(desired.Exists ? RegistryChangeAction.SetValue : RegistryChangeAction.DeleteValue, desired.Path, desired.Name, before.Value, desired.Value);
            cancellationToken.ThrowIfCancellationRequested();
            var latest = CaptureValue(desired.Path, desired.Name);
            if (latest.KeyExists != before.KeyExists || !latest.SameValue(before))
                return new RegistryApplyResult(RegistryApplyStatus.Conflict, completed, change);
            if (desired.Exists)
                manager.SetValue(desired.Path, desired.Name, desired.Value!, true);
            else
                manager.DeleteValue(desired.Path, desired.Name);
            completed.Add(change);
            if (!MatchesAfter(change))
                throw new IOException("Registry value verification failed.");
            return new RegistryApplyResult(RegistryApplyStatus.Completed, completed);
        }
        catch (OperationCanceledException error) { return new RegistryApplyResult(RegistryApplyStatus.Cancelled, completed, change, error); }
        catch (Exception error) when (IsOperational(error)) { return new RegistryApplyResult(RegistryApplyStatus.Failed, completed, change, error); }
    }

    internal static bool SameTree(RegistryTreeSnapshot left, RegistryTreeSnapshot right) => Compare(left, right, RegistryRestoreMode.Replace).Changes.Count == 0;
    internal static bool IsOperational(Exception error) => error is IOException || error is UnauthorizedAccessException || error is SecurityException
        || error is System.ComponentModel.Win32Exception || error is NotSupportedException;
    private void ValidateEndpoint(Microsoft.Win32.RegistryView view, string? machine)
    {
        if (view != manager.View || !StringComparer.OrdinalIgnoreCase.Equals(machine, manager.MachineName))
            throw new ArgumentException("The snapshot belongs to a different machine or registry view.");
    }
    private bool MatchesBefore(RegistryChange change) => change.Action == RegistryChangeAction.CreateKey ? !manager.KeyExists(change.Path)
        : change.Action == RegistryChangeAction.DeleteKey ? manager.KeyExists(change.Path) && manager.GetValues(change.Path).Count == 0 && manager.GetSubKeys(change.Path).Count == 0
        : manager.KeyExists(change.Path) && Equals(manager.GetValue(change.Path, change.Name!), change.Before);
    private bool MatchesAfter(RegistryChange change) => change.Action == RegistryChangeAction.CreateKey ? manager.KeyExists(change.Path)
        : change.Action == RegistryChangeAction.DeleteKey ? !manager.KeyExists(change.Path) : Equals(manager.GetValue(change.Path, change.Name!), change.After);
}
