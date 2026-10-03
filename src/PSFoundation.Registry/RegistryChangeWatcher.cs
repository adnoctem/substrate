using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PSFoundation.Registry;

[Flags]
public enum RegistryChangeKinds { Names = 1, Attributes = 2, Values = 4, Security = 8 }

/// <summary>Local Windows 8+ change signals. Notifications can coalesce and do not identify individual writes.</summary>
public sealed class RegistryChangeWatcher : IDisposable
{
    private readonly object gate = new object();
    private readonly RegistryKey key;
    private readonly ManualResetEvent signal = new ManualResetEvent(false);
    private readonly bool subtree;
    private readonly RegistryChangeKinds kinds;
    private bool waiting;
    private bool disposed;
    private RegisteredWaitHandle? registration;
    private CancellationTokenRegistration cancellation;
    private TaskCompletionSource<bool>? completion;

    internal RegistryChangeWatcher(RegistryManager manager, RegistryPath path, bool subtree, RegistryChangeKinds kinds)
    {
        if (manager.MachineName != null)
        { signal.Dispose(); throw new NotSupportedException("Registry notifications require a local key."); }
        if (kinds == 0 || ((uint)kinds & ~15u) != 0)
        { signal.Dispose(); throw new ArgumentOutOfRangeException(nameof(kinds)); }
        this.subtree = subtree;
        this.kinds = kinds;
        try
        { key = manager.RequireKey(path); }
        catch { signal.Dispose(); throw; }
        try
        { Arm(); }
        catch { key.Dispose(); signal.Dispose(); throw; }
    }

    /// <summary>One waiter at a time. Cancellation cancels the wait; Dispose releases the native registration.</summary>
    public Task WaitForChangeAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(RegistryChangeWatcher));
            if (waiting)
                throw new InvalidOperationException("Only one concurrent notification wait is supported.");
            cancellationToken.ThrowIfCancellationRequested();
            waiting = true;
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = pending;
            try
            {
                registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => pending.TrySetResult(true), null, Timeout.Infinite, true);
                cancellation = cancellationToken.Register(() => pending.TrySetCanceled());
                return WaitCore(pending.Task);
            }
            catch { StopWait(); waiting = false; throw; }
        }
    }
    private async Task WaitCore(Task pending)
    {
        try
        {
            await pending.ConfigureAwait(false);
            lock (gate)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(RegistryChangeWatcher));
                signal.Reset();
                Arm();
            }
        }
        finally
        {
            lock (gate)
            { StopWait(); waiting = false; }
        }
    }
    private void Arm() => NativeRegistry.ThrowIfError(NativeRegistry.RegNotifyChangeKeyValue(key.Handle, subtree, (uint)kinds | 0x10000000u, signal.SafeWaitHandle, true));
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            completion?.TrySetCanceled();
            StopWait();
            // In-flight callbacks only touch their completion source, never these handles.
            key.Dispose();
            signal.Dispose();
        }
    }
    private void StopWait()
    {
        registration?.Unregister(null);
        registration = null;
        cancellation.Dispose();
        cancellation = default;
        completion = null;
    }
}
