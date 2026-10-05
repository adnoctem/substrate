using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using AdNoctem.Substrate.IO;
using Microsoft.Win32.SafeHandles;

namespace AdNoctem.Substrate.Security;

[Flags]
public enum FileSecurityParts { Owner = 1, Group = 2, Access = 4, Audit = 8 }
/// <summary>A copied Windows security descriptor; returned binary or mutable forms do not mutate this instance.</summary>
public sealed class FileSecurityDescriptor
{
    private readonly byte[] bytes;
    public FileSecurityDescriptor(byte[] descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        var value = new RawSecurityDescriptor(descriptor, 0);
        bytes = new byte[value.BinaryLength];
        value.GetBinaryForm(bytes, 0);
    }
    public byte[] GetBinaryForm() => (byte[])bytes.Clone();
    public string GetSddl(AccessControlSections sections = AccessControlSections.All) => new RawSecurityDescriptor(bytes, 0).GetSddlForm(sections);
    public RawSecurityDescriptor ToRawDescriptor() => new RawSecurityDescriptor(bytes, 0);
    public static FileSecurityDescriptor FromSddl(string sddl)
    {
        var value = new RawSecurityDescriptor(sddl ?? throw new ArgumentNullException(nameof(sddl)));
        var bytes = new byte[value.BinaryLength];
        value.GetBinaryForm(bytes, 0);
        return new FileSecurityDescriptor(bytes);
    }
}

/// <summary>Native Windows file/directory security operations. Authority, privileges and link policy remain explicit caller responsibilities.</summary>
public sealed class FileSecurityManager
{
    /// <summary>Creates a new file with its security applied before any bytes are written. The caller owns the stream; existing paths are refused.</summary>
    public FileStream CreateFile(FileSystemPath path, FileSecurityDescriptor descriptor)
    {
        Require(path, FileSecurityParts.Access);
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        var pinned = GCHandle.Alloc(descriptor.GetBinaryForm(), GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = pinned.AddrOfPinnedObject() };
            var handle = CreateFileNative(path.Value, 0x40000000, 0, ref attributes, 1, 0x00200080, IntPtr.Zero);
            if (handle.IsInvalid)
            { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error, "Cannot create file with explicit security."); }
            try
            { return new FileStream(handle, FileAccess.Write); }
            catch { handle.Dispose(); throw; }
        }
        finally { pinned.Free(); }
    }
    public FileSecurityDescriptor Read(FileSystemPath path, FileSecurityParts parts = FileSecurityParts.Owner | FileSecurityParts.Group | FileSecurityParts.Access)
    {
        Require(path, parts);
        _ = GetFileSecurity(path.Value, (uint)parts, null, 0, out var size);
        var error = Marshal.GetLastWin32Error();
        if (error != 122)
            throw new Win32Exception(error, "Cannot read filesystem security descriptor.");
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (size == 0 || size > 16 * 1024 * 1024)
                throw new IOException("Unsupported filesystem security descriptor size.");
            var bytes = new byte[checked((int)size)];
            if (GetFileSecurity(path.Value, (uint)parts, bytes, size, out size))
                return new FileSecurityDescriptor(bytes);
            error = Marshal.GetLastWin32Error();
            if (error != 122)
                throw new Win32Exception(error, "Cannot read filesystem security descriptor.");
        }
        throw new IOException("Filesystem security changed repeatedly during capture.");
    }
    /// <summary>Writes only selected sections. Access-rule inheritance protection changes only when explicitly supplied.</summary>
    public void Write(FileSystemPath path, FileSecurityDescriptor descriptor, FileSecurityParts parts, bool? protectAccessRules = null)
    {
        Require(path, parts);
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        if (protectAccessRules.HasValue && (parts & FileSecurityParts.Access) == 0)
            throw new ArgumentException("Inheritance protection requires the access section.", nameof(parts));
        var flags = (uint)parts | (protectAccessRules.HasValue ? protectAccessRules.Value ? 0x80000000u : 0x20000000u : 0u);
        if (!SetFileSecurity(path.Value, flags, descriptor.GetBinaryForm()))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot write filesystem security descriptor.");
    }
    /// <summary>Creates one new directory with its security descriptor applied at creation. The parent must exist; an existing destination is never changed.</summary>
    public void CreateDirectory(FileSystemPath path, FileSecurityDescriptor descriptor)
    {
        Require(path, FileSecurityParts.Access);
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        var bytes = descriptor.GetBinaryForm();
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = pinned.AddrOfPinnedObject(), InheritHandle = false };
            if (!CreateDirectoryNative(path.Value, ref attributes))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create directory with explicit security.");
        }
        finally { pinned.Free(); }
    }
    private static void Require(FileSystemPath path, FileSecurityParts parts)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (parts == 0 || (parts & ~(FileSecurityParts.Owner | FileSecurityParts.Group | FileSecurityParts.Access | FileSecurityParts.Audit)) != 0)
            throw new ArgumentOutOfRangeException(nameof(parts));
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new PlatformNotSupportedException("Filesystem security requires Windows.");
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle; }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "GetFileSecurityW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileSecurity(string path, uint parts, byte[]? descriptor, uint length, out uint needed);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "SetFileSecurityW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileSecurity(string path, uint parts, byte[] descriptor);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryNative(string path, ref SecurityAttributes attributes);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileNative(string path, uint access, uint share, ref SecurityAttributes attributes, uint disposition, uint flags, IntPtr template);
}
