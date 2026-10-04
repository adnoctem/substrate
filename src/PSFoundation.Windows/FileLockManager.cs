using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;

namespace PSFoundation.Windows;

public enum LockingApplicationType { Unknown = 0, MainWindow = 1, OtherWindow = 2, Service = 3, Explorer = 4, Console = 5, Critical = 1000 }
public sealed class FileLockInfo
{
    public int ProcessId { get; }
    public DateTime ProcessStartedAtUtc { get; }
    public string? ProcessName { get; }
    public string ApplicationName { get; }
    public string ServiceName { get; }
    public uint SessionId { get; }
    public LockingApplicationType ApplicationType { get; }
    public bool Restartable { get; }
    internal FileLockInfo(FileLockManager.NativeInfo info)
    {
        ProcessId = info.Process.Id;
        ProcessStartedAtUtc = DateTime.FromFileTimeUtc(unchecked((long)(((ulong)(uint)info.Process.StartTime.dwHighDateTime << 32) | (uint)info.Process.StartTime.dwLowDateTime)));
        ApplicationName = info.ApplicationName;
        ServiceName = info.ServiceName;
        SessionId = info.SessionId;
        ApplicationType = info.Type;
        Restartable = info.Restartable;
        try
        { using (var process = Process.GetProcessById(ProcessId)) if (process.StartTime.ToUniversalTime() == ProcessStartedAtUtc) ProcessName = process.ProcessName; }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}

/// <summary>Read-only Restart Manager discovery. Never shuts down or restarts applications.</summary>
public sealed class FileLockManager
{
    public IReadOnlyList<FileLockInfo> GetLockingProcesses(FileSystemPath file, CancellationToken cancellationToken = default)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
        if (!File.Exists(file.Value))
            throw new FileNotFoundException("File does not exist.", file.Value);
        cancellationToken.ThrowIfCancellationRequested();
        Check(RmStartSession(out var session, 0, new StringBuilder(33)));
        try
        {
            Check(RmRegisterResources(session, 1, new[] { file.Value }, 0, IntPtr.Zero, 0, IntPtr.Zero));
            uint needed, count = 0, reasons = 0;
            var status = RmGetList(session, out needed, ref count, null, ref reasons);
            for (var attempt = 0; attempt < 8; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (status == 0 && count == 0)
                    return Array.Empty<FileLockInfo>();
                if (status != 234)
                    Check(status);
                if (needed > 65536)
                    throw new IOException("Restart Manager reported an unexpectedly large process list.");
                var entries = new NativeInfo[needed];
                count = needed;
                status = RmGetList(session, out needed, ref count, entries, ref reasons);
                if (status == 0)
                    return Array.AsReadOnly(entries.Take(checked((int)count)).Select(info => new FileLockInfo(info)).ToArray());
            }
            throw new IOException("The Restart Manager process list kept changing during enumeration.");
        }
        finally { RmEndSession(session); }
    }
    /// <remarks>Offloads native discovery. Checks cancellation between native calls; an in-flight Restart Manager query cannot be interrupted.</remarks>
    public Task<IReadOnlyList<FileLockInfo>> GetLockingProcessesAsync(FileSystemPath file, CancellationToken cancellationToken = default)
        => Task.Run(() => GetLockingProcesses(file, cancellationToken), cancellationToken);
    private static void Check(int result) { if (result != 0) throw new Win32Exception(result); }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeProcess { internal int Id; internal System.Runtime.InteropServices.ComTypes.FILETIME StartTime; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeInfo
    {
        internal NativeProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string ApplicationName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string ServiceName;
        internal LockingApplicationType Type;
        internal uint Status, SessionId;
        [MarshalAs(UnmanagedType.Bool)] internal bool Restartable;
    }
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] private static extern int RmStartSession(out uint session, int flags, StringBuilder key);
    [DllImport("rstrtmgr.dll")] private static extern int RmEndSession(uint session);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] private static extern int RmRegisterResources(uint session, uint fileCount, string[] files, uint processCount, IntPtr processes, uint serviceCount, IntPtr services);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] private static extern int RmGetList(uint session, out uint needed, ref uint count, [In, Out] NativeInfo[]? info, ref uint reasons);
}
