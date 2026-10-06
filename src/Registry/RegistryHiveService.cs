using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry;

/// <summary>Binary hive operations. Uses existing process authority; never prompts, elevates, or forces collection.</summary>
/// <remarks>Binary hives are distinct from .reg text files. Operations require a local manager and use the system reg.exe for
/// its selected view. Save publishes through a sibling temporary file, preserving the old destination on failure. Relative paths
/// use the process working directory. Save/restore/load privileges must already be available to the caller.</remarks>
public sealed class RegistryHiveService
{
    private readonly RegistryManager manager;
    private readonly IRegistryCommandRunner runner;
    private readonly Func<RegistryPath, bool> exists;
    public RegistryHiveService(RegistryManager manager) : this(manager, new RegistryCommandRunner(view: (manager ?? throw new ArgumentNullException(nameof(manager))).View), manager.KeyExists) { }
    internal RegistryHiveService(RegistryManager manager, IRegistryCommandRunner runner, Func<RegistryPath, bool> exists)
    { this.manager = manager; this.runner = runner; this.exists = exists; }

    /// <summary>Saves a local subtree to a binary hive file using reg.exe and the caller's existing authority.</summary>
    public void Save(RegistryPath path, string destination, bool overwrite = false, CancellationToken cancellationToken = default) => SaveAsync(path, destination, overwrite, cancellationToken).GetAwaiter().GetResult();
    /// <summary>Saves a binary hive without mounting it. Existing files require explicit overwrite.</summary>
    public Task SaveAsync(RegistryPath path, string destination, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        RegistryManager.RequireNonRoot(path);
        return new RegistryFileService(manager, runner).PublishAsync("save", path, destination, overwrite, cancellationToken);
    }

    /// <summary>Overwrites a key from a binary hive. Failure/cancellation does not roll back native writes.</summary>
    public void Restore(RegistryPath path, string source, CancellationToken cancellationToken = default) => RestoreAsync(path, source, cancellationToken).GetAwaiter().GetResult();
    /// <summary>Restores a binary hive using reg.exe; interruption does not guarantee that prior changes were undone.</summary>
    public async Task RestoreAsync(RegistryPath path, string source, CancellationToken cancellationToken = default)
    {
        new RegistryFileService(manager, runner).RequireLocal();
        RegistryManager.RequireNonRoot(path);
        var file = RegistryFileService.RequireFile(source);
        RegistryFileService.EnsureSuccess(await runner.RunAsync(new[] { "restore", path.ToString(), file }, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Loads a new HKLM/HKU child and returns its owner. Once started, load finishes before returning ownership.</summary>
    public RegistryHiveLease Mount(RegistryPath mountPoint, string source, CancellationToken cancellationToken = default) => MountAsync(mountPoint, source, cancellationToken).GetAwaiter().GetResult();
    /// <summary>Loads a hive and transfers responsibility for unloading it to the returned lease.</summary>
    /// <remarks>The mount must be a missing immediate child of HKLM or HKU. Cancellation is checked before starting;
    /// once load starts it finishes before ownership is returned, even if cancellation is requested meanwhile.
    /// Close all child handles before disposing the lease. Unload failure throws and permits retry; no forced GC occurs.
    /// Caller-managed Unmount requires ownership and coordination against external replacement or unload races.</remarks>
    public async Task<RegistryHiveLease> MountAsync(RegistryPath mountPoint, string source, CancellationToken cancellationToken = default)
    {
        ValidateMount(mountPoint);
        var file = RegistryFileService.RequireFile(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (exists(mountPoint))
            throw new InvalidOperationException("The mount point already exists; ownership was not acquired.");
        // Do not abandon a successful load between native completion and ownership transfer.
        RegistryFileService.EnsureSuccess(await runner.RunAsync(new[] { "load", mountPoint.ToString(), file }, CancellationToken.None).ConfigureAwait(false));
        return new RegistryHiveLease(mountPoint, () => Unmount(mountPoint));
    }

    /// <summary>Explicitly unloads a mount. Callers must own it and close all handles first.</summary>
    public void Unmount(RegistryPath mountPoint)
    {
        ValidateMount(mountPoint);
        RegistryFileService.EnsureSuccess(runner.Run(new[] { "unload", mountPoint.ToString() }, CancellationToken.None));
    }
    private void ValidateMount(RegistryPath path)
    {
        new RegistryFileService(manager, runner).RequireLocal();
        RegistryManager.RequireNonRoot(path);
        if ((path.Hive != RegistryHive.LocalMachine && path.Hive != RegistryHive.Users) || path.SubKey.IndexOf('\\') >= 0)
            throw new ArgumentException("Mount points must be immediate children of HKEY_LOCAL_MACHINE or HKEY_USERS.", nameof(path));
    }
}

/// <summary>Owns one successful load. Dispose unloads it; a failed unload throws and can be retried.</summary>
public sealed class RegistryHiveLease : IDisposable
{
    private readonly object gate = new object();
    private Action? unload;
    public RegistryPath MountPoint { get; }
    internal RegistryHiveLease(RegistryPath mountPoint, Action unload) { MountPoint = mountPoint; this.unload = unload; }
    public void Dispose() { lock (gate) { if (unload == null) return; unload(); unload = null; } }
}
