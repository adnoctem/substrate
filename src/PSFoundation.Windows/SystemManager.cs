using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using PSFoundation.Registry;

namespace PSFoundation.Windows;

/// <summary>Local Windows inventory. Registry access uses the shared registry API; native resources never escape snapshots.</summary>
public sealed class SystemManager
{
    private static readonly RegistryPath VersionPath = RegistryPath.Parse(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion");
    private readonly RegistryManager registry;
    public SystemManager(RegistryManager? registry = null)
    {
        this.registry = registry ?? new RegistryManager();
        if (this.registry.MachineName != null)
            throw new ArgumentException("System inventory requires a local registry manager.", nameof(registry));
    }
    public WindowsVersionInfo GetOperatingSystem() => new WindowsVersionInfo(registry.GetValues(VersionPath));
    public int GetBuildNumber() => Convert.ToInt32(RequiredVersionValue("CurrentBuild"), CultureInfo.InvariantCulture);
    public string GetEdition() => Convert.ToString(RequiredVersionValue("EditionID"), CultureInfo.InvariantCulture)!;
    public string GetDisplayVersion()
    {
        var value = registry.GetValue(VersionPath, "DisplayVersion");
        var text = value == null ? null : Convert.ToString(value.Data, CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(text) ? Convert.ToString(RequiredVersionValue("ReleaseId"), CultureInfo.InvariantCulture)! : text!;
    }
    private object RequiredVersionValue(string name) => registry.GetValue(VersionPath, name)?.Data
        ?? throw new IOException("Required Windows version value is missing: " + name);

    public MemorySnapshot GetMemory()
    {
        RequireWindows();
        var data = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
        if (!GlobalMemoryStatusEx(ref data))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalMemoryStatusEx failed.");
        return new MemorySnapshot(data.TotalPhysical, data.AvailablePhysical, data.Load);
    }
    public TimeSpan GetUptime() { RequireWindows(); return TimeSpan.FromMilliseconds(GetTickCount64()); }

    /// <remarks>The native DNS resolver cannot be interrupted on every supported runtime. Cancellation/timeout ends the caller's wait;
    /// any pending resolver operation finishes in the background without retaining unmanaged resources owned by this library.</remarks>
    public async Task<HostnameInfo> GetHostnameAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        CheckTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var name = Dns.GetHostName();
        using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var query = ResolveHostnameAsync(name);
            var delay = Task.Delay(timeout, delayCancellation.Token);
            if (await Task.WhenAny(query, delay).ConfigureAwait(false) != query)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new HostnameInfo(name, null, new TimeoutException("Hostname resolution timed out."));
            }
            delayCancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return await query.ConfigureAwait(false);
        }
    }
    private static async Task<HostnameInfo> ResolveHostnameAsync(string name)
    {
        try
        {
            var resolved = (await Dns.GetHostEntryAsync(name).ConfigureAwait(false)).HostName;
            return new HostnameInfo(name, string.Equals(name, resolved, StringComparison.OrdinalIgnoreCase) ? null : resolved, null);
        }
        catch (SocketException error) { return new HostnameInfo(name, null, error); }
    }
    public HostnameInfo GetHostname(TimeSpan timeout, CancellationToken cancellationToken = default)
        => GetHostnameAsync(timeout, cancellationToken).GetAwaiter().GetResult();

    /// <param name="allowLogicalFallback">When true, failed physical associations fall back to logical volumes and retain the original error.</param>
    /// <remarks>The timeout applies to each CIM operation. Cancellation also applies between association reads.</remarks>
    public DiskInventory GetDisks(TimeSpan timeout, bool includeNonFixed = false, bool allowLogicalFallback = false, CancellationToken cancellationToken = default)
    {
        RequireWindows();
        CheckTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var disks = new Dictionary<string, DiskSnapshot>(StringComparer.OrdinalIgnoreCase);
        using (var session = CimSession.Create(null))
        using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
        {
            void Add(CimInstance value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object? Read(string name) => value.CimInstanceProperties[name]?.Value;
                var type = (DriveType)Convert.ToInt32(Read("DriveType"), CultureInfo.InvariantCulture);
                var name = Read("DeviceID") as string;
                var size = Convert.ToUInt64(Read("Size"), CultureInfo.InvariantCulture);
                if ((!includeNonFixed && type != DriveType.Fixed) || string.IsNullOrWhiteSpace(name) || size == 0)
                    return;
                disks[name!] = new DiskSnapshot(name!, Read("VolumeName") as string, type, Read("FileSystem") as string,
                    size, Convert.ToUInt64(Read("FreeSpace"), CultureInfo.InvariantCulture));
            }
            Exception? failure = null;
            try
            {
                foreach (var drive in session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_DiskDrive", options))
                    using (drive)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        foreach (var partition in session.EnumerateAssociatedInstances(@"root\cimv2", drive, "Win32_DiskDriveToDiskPartition", null, null, null, options))
                            using (partition)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                foreach (var disk in session.EnumerateAssociatedInstances(@"root\cimv2", partition, "Win32_LogicalDiskToPartition", null, null, null, options))
                                    using (disk)
                                        Add(disk);
                            }
                    }
            }
            catch (Exception error) when (error is CimException || error is UnauthorizedAccessException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!allowLogicalFallback)
                    throw;
                failure = error;
                foreach (var disk in session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_LogicalDisk", options))
                    using (disk)
                        Add(disk);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new DiskInventory(disks.Values.OrderBy(disk => disk.Name, StringComparer.OrdinalIgnoreCase), failure);
        }
    }
    /// <remarks>CIM has synchronous native calls; this overload offloads them. Providers must honor cancellation and operation timeouts.</remarks>
    public Task<DiskInventory> GetDisksAsync(TimeSpan timeout, bool includeNonFixed = false, bool allowLogicalFallback = false, CancellationToken cancellationToken = default)
        => Task.Run(() => GetDisks(timeout, includeNonFixed, allowLogicalFallback, cancellationToken), cancellationToken);

    public async Task<SystemSnapshot> GetSnapshotAsync(TimeSpan timeout, bool allowLogicalDiskFallback = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckTimeout(timeout);
        var os = GetOperatingSystem();
        var memory = GetMemory();
        var disks = await GetDisksAsync(timeout, allowLogicalFallback: allowLogicalDiskFallback, cancellationToken: cancellationToken).ConfigureAwait(false);
        var hostname = await GetHostnameAsync(timeout, cancellationToken).ConfigureAwait(false);
        return new SystemSnapshot(os, memory, disks, hostname, GetUptime());
    }
    internal static void RequireWindows()
    { if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException("This operation requires Windows."); }
    private static void CheckTimeout(TimeSpan timeout)
    { if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(timeout)); }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        internal uint Length, Load;
        internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus data);
    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
