using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AdNoctem.Substrate.Registry;

internal static class NativeRegistry
{
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int RegRenameKey(SafeRegistryHandle key, string? subKey, string newName);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    internal static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key, [MarshalAs(UnmanagedType.Bool)] bool subtree, uint filter, SafeWaitHandle signal, [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int RegOpenKeyExW(SafeRegistryHandle root, string subKey, uint options, int access, out SafeRegistryHandle key);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    internal static extern int RegGetKeySecurity(SafeRegistryHandle key, uint sections, byte[]? descriptor, ref uint length);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    internal static extern int RegSetKeySecurity(SafeRegistryHandle key, uint sections, byte[] descriptor);

    internal static void ThrowIfError(int error)
    {
        if (error == 5)
            throw new UnauthorizedAccessException(new Win32Exception(error).Message);
        if (error != 0)
            throw new Win32Exception(error);
    }
}
