using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PSFoundation.Registry;

/// <summary>Binary hive operations. Uses existing process authority; never prompts, elevates, or forces collection.</summary>
public sealed class RegistryHiveService
{
    private readonly RegistryManager manager;
    private readonly IRegistryCommandRunner runner;
    private readonly Func<RegistryPath, bool> exists;
    public RegistryHiveService(RegistryManager manager) : this(manager, new RegistryCommandRunner(view: (manager ?? throw new ArgumentNullException(nameof(manager))).View), manager.KeyExists) { }
    internal RegistryHiveService(RegistryManager manager, IRegistryCommandRunner runner, Func<RegistryPath, bool> exists)
    { this.manager = manager; this.runner = runner; this.exists = exists; }

    public void Save(RegistryPath path, string destination, bool overwrite = false, CancellationToken cancellationToken = default) => SaveAsync(path, destination, overwrite, cancellationToken).GetAwaiter().GetResult();
    public Task SaveAsync(RegistryPath path, string destination, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        RegistryManager.RequireNonRoot(path);
        return new RegistryFileService(manager, runner).PublishAsync("save", path, destination, overwrite, cancellationToken);
    }

    /// <summary>Overwrites a key from a binary hive. Failure/cancellation does not roll back native writes.</summary>
    public void Restore(RegistryPath path, string source, CancellationToken cancellationToken = default) => RestoreAsync(path, source, cancellationToken).GetAwaiter().GetResult();
    public async Task RestoreAsync(RegistryPath path, string source, CancellationToken cancellationToken = default)
    {
        new RegistryFileService(manager, runner).RequireLocal();
        RegistryManager.RequireNonRoot(path);
        var file = RegistryFileService.RequireFile(source);
        RegistryFileService.EnsureSuccess(await runner.RunAsync(new[] { "restore", path.ToString(), file }, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Loads a new HKLM/HKU child and returns its owner. Once started, load finishes before returning ownership.</summary>
    public RegistryHiveLease Mount(RegistryPath mountPoint, string source, CancellationToken cancellationToken = default) => MountAsync(mountPoint, source, cancellationToken).GetAwaiter().GetResult();
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
