using System;
using System.Collections;
using Microsoft.Win32;

namespace PSFoundation.Registry;

/// <summary>A raw, view-specific value snapshot. A null KeyExists denotes desired state.</summary>
public sealed class RegistryState
{
    public RegistryPath Path { get; }
    public string Name { get; }
    public RegistryView View { get; }
    public bool? KeyExists { get; }
    public bool Exists { get; }
    public RegistryValueKind? Kind { get; }
    public object? Value { get; }

    public RegistryState(RegistryPath path, string name, RegistryView view, bool exists,
        RegistryValueKind? kind, object? value, bool? keyExists = null)
    {
        Path = path;
        Name = name;
        View = view == RegistryView.Default ? (Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32) : view;
        Exists = exists;
        Kind = exists ? kind : null;
        Value = exists ? value : null;
        KeyExists = keyExists;
    }

    public bool SameValue(RegistryState other)
    {
        if (Exists != other.Exists)
            return false;
        if (!Exists)
            return true;
        if (Kind != other.Kind)
            return false;
        var left = Value is Array a ? a : new object?[] { Value };
        var right = other.Value is Array b ? b : new object?[] { other.Value };
        if (left.Length != right.Length)
            return false;
        for (var index = 0; index < left.Length; index++)
            if (!Equals(left.GetValue(index), right.GetValue(index)))
                return false;
        return true;
    }

    public string Identity => Path.ProviderPath.Length + ":" + Path.ProviderPath + Name.Length + ":" + Name + ":" + View;
}

public sealed class RegistryDifference
{
    public RegistryState Before { get; }
    public RegistryState After { get; }
    public bool Changed => !Before.SameValue(After);
    public string Action => !Changed ? "None" : After.Exists ? "SetValue" : "RemoveValue";
    public RegistryDifference(RegistryState before, RegistryState after)
    {
        Before = before;
        After = after;
    }
}

public sealed class RegistryRestoreResult
{
    public RegistryDifference Difference { get; }
    public RegistryState After { get; internal set; }
    public string Status { get; internal set; } = "AlreadyCompliant";
    public bool Changed { get; internal set; }
    public bool AlreadyCompliant => !Difference.Changed;
    public string? Error { get; internal set; }
    public RegistryRestoreResult(RegistryDifference difference)
    {
        Difference = difference;
        After = difference.Before;
    }
}

/// <summary>The narrow state boundary used for conflict, failure and race testing.</summary>
public interface IRegistryStateStore
{
    RegistryState Read(RegistryPath path, string name, RegistryView view);
    void Apply(RegistryState desired);
}

public sealed class RegistryStateService
{
    private readonly IRegistryStateStore store;
    private readonly Func<Exception, bool> handleFailure;
    public RegistryStateService(IRegistryStateStore store, Func<Exception, bool>? handleFailure = null)
    {
        this.store = store;
        this.handleFailure = handleFailure ?? (_ => true);
    }
    public RegistryDifference Compare(RegistryState desired) => new RegistryDifference(store.Read(desired.Path, desired.Name, desired.View), desired);

    public RegistryRestoreResult Restore(RegistryState desired, bool checkExpected, RegistryState? expected,
        Func<string, string, bool> authorize)
    {
        var diff = Compare(desired);
        var result = new RegistryRestoreResult(diff);
        if (!diff.Changed)
            return result;
        if (checkExpected && (expected == null || !diff.Before.SameValue(expected)))
        {
            result.Status = "Conflict";
            return result;
        }
        if (!authorize(desired.Path.ProviderPath + "\\" + desired.Name + " [" + desired.View + "]", diff.Action))
        {
            result.Status = "Skipped";
            return result;
        }
        try
        {
            var latest = store.Read(desired.Path, desired.Name, desired.View);
            if (!latest.SameValue(diff.Before))
            {
                result.Status = "Conflict";
                result.After = latest;
                return result;
            }
            store.Apply(desired);
            result.After = store.Read(desired.Path, desired.Name, desired.View);
            result.Changed = !diff.Before.SameValue(result.After);
            if (!result.After.SameValue(desired))
                throw new InvalidOperationException("Registry verification failed after restoration.");
            result.Status = "Restored";
        }
        catch (Exception error) when (!(error is OperationCanceledException) && handleFailure(error))
        { result.Status = "Failed"; result.Error = error.Message; }
        return result;
    }
}
