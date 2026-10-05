using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace AdNoctem.Substrate.Security;

// Enables privileges on an owned impersonation token, never on the shared process token.
// Synchronous scope: must be disposed on its acquiring thread before returning to a pool.
internal sealed class ThreadPrivilegeScope : IDisposable
{
    private readonly int thread = Thread.CurrentThread.ManagedThreadId;
    private SafeAccessTokenHandle? previous;
    private SafeAccessTokenHandle? active;
    private bool applied;
    internal ThreadPrivilegeScope(params string[] privileges)
    {
        SafeAccessTokenHandle? source = null;
        try
        {
            if (OpenThreadToken(GetCurrentThread(), 0xE, true, out var current))
            { previous = current; source = current; }
            else
            {
                var error = Marshal.GetLastWin32Error();
                current.Dispose();
                if (error != 1008)
                    throw new Win32Exception(error);
                if (!OpenProcessToken(GetCurrentProcess(), 0xA, out source))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (!DuplicateTokenEx(source, 0x2C, IntPtr.Zero, 2, 2, out active))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            foreach (var privilege in privileges)
            {
                if (!LookupPrivilegeValue(null, privilege, out var luid))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var state = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
                if (!AdjustTokenPrivileges(active, false, ref state, 0, IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                    throw new Win32Exception(error, "The caller does not hold the required ownership privilege.");
            }
            if (!SetThreadToken(IntPtr.Zero, active.DangerousGetHandle()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            applied = true;
        }
        catch { active?.Dispose(); previous?.Dispose(); throw; }
        finally { if (source != previous) source?.Dispose(); }
    }
    public void Dispose()
    {
        if (active == null)
            return;
        if (Thread.CurrentThread.ManagedThreadId != thread)
            throw new InvalidOperationException("Privilege scopes must be disposed on the acquiring thread.");
        if (applied && !SetThreadToken(IntPtr.Zero, previous?.DangerousGetHandle() ?? IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot restore the thread identity.");
        applied = false;
        active.Dispose();
        active = null;
        previous?.Dispose();
        previous = null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] private struct TokenPrivileges { public uint Count; public Luid Luid; public uint Attributes; }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenThreadToken(IntPtr thread, uint access, [MarshalAs(UnmanagedType.Bool)] bool self, out SafeAccessTokenHandle token);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DuplicateTokenEx(SafeAccessTokenHandle token, uint access, IntPtr attributes, int level, int type, out SafeAccessTokenHandle duplicate);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token, [MarshalAs(UnmanagedType.Bool)] bool disable, ref TokenPrivileges state, uint length, IntPtr previous, IntPtr returned);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetThreadToken(IntPtr thread, IntPtr token);
}
