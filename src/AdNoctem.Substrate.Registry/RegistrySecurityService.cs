using System;
using System.Security.AccessControl;
using Microsoft.Win32.SafeHandles;

namespace AdNoctem.Substrate.Registry;

[Flags]
public enum RegistrySecurityParts { Owner = 1, Group = 2, Access = 4, Audit = 8 }

/// <summary>An immutable self-relative Windows security descriptor. Does not enable privileges or change identity.</summary>
public sealed class RegistrySecurityDescriptor
{
    private readonly byte[] bytes;
    public byte[] GetBinaryForm() => (byte[])bytes.Clone();
    public string GetSddl() => new RawSecurityDescriptor(bytes, 0).GetSddlForm(AccessControlSections.All);
    public RegistrySecurityDescriptor(byte[] descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        var parsed = new RawSecurityDescriptor(descriptor, 0);
        bytes = new byte[parsed.BinaryLength];
        parsed.GetBinaryForm(bytes, 0);
    }
    public static RegistrySecurityDescriptor FromSddl(string sddl)
    {
        var parsed = new RawSecurityDescriptor(sddl ?? throw new ArgumentNullException(nameof(sddl)));
        var data = new byte[parsed.BinaryLength];
        parsed.GetBinaryForm(data, 0);
        return new RegistrySecurityDescriptor(data);
    }
}

/// <summary>Reads and writes selected registry security sections without enabling privileges or choosing an inheritance policy.</summary>
public sealed class RegistrySecurityService
{
    private readonly RegistryManager manager;
    public RegistrySecurityService(RegistryManager manager) => this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
    public RegistrySecurityDescriptor Read(RegistryPath path, RegistrySecurityParts parts = RegistrySecurityParts.Owner | RegistrySecurityParts.Group | RegistrySecurityParts.Access)
    {
        Validate(parts);
        using (var key = Open(path, 0x20000 | ((parts & RegistrySecurityParts.Audit) != 0 ? 0x1000000 : 0)))
        {
            uint size = 0;
            var error = NativeRegistry.RegGetKeySecurity(key, (uint)parts, null, ref size);
            if (error != 122)
                NativeRegistry.ThrowIfError(error);
            // The descriptor can grow between calls. Retry without assuming a stable size.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var bytes = new byte[checked((int)size)];
                error = NativeRegistry.RegGetKeySecurity(key, (uint)parts, bytes, ref size);
                if (error == 122)
                    continue;
                NativeRegistry.ThrowIfError(error);
                return new RegistrySecurityDescriptor(bytes);
            }
            throw new System.IO.IOException("Registry security changed repeatedly during capture.");
        }
    }
    /// <summary>Writes only selected parts. Privileges and inheritance policy are the caller's responsibility.</summary>
    public void Write(RegistryPath path, RegistrySecurityDescriptor descriptor, RegistrySecurityParts parts, bool? protectAccessRules = null)
    {
        RegistryManager.RequireNonRoot(path);
        Validate(parts);
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        if (protectAccessRules.HasValue && (parts & RegistrySecurityParts.Access) == 0)
            throw new ArgumentException("Inheritance protection requires the access section.", nameof(parts));
        var rights = ((parts & (RegistrySecurityParts.Owner | RegistrySecurityParts.Group)) != 0 ? 0x80000 : 0)
            | ((parts & RegistrySecurityParts.Access) != 0 ? 0x40000 : 0) | ((parts & RegistrySecurityParts.Audit) != 0 ? 0x1000000 : 0);
        using (var key = Open(path, rights))
            NativeRegistry.ThrowIfError(NativeRegistry.RegSetKeySecurity(key, (uint)parts | (protectAccessRules.HasValue ? protectAccessRules.Value ? 0x80000000u : 0x20000000u : 0u), descriptor.GetBinaryForm()));
    }
    private SafeRegistryHandle Open(RegistryPath path, int access)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        using (var root = manager.OpenBase(path.Hive))
        {
            var error = NativeRegistry.RegOpenKeyExW(root.Handle, path.SubKey, 0, access | (int)manager.View, out var handle);
            if (error != 0)
            { handle?.Dispose(); NativeRegistry.ThrowIfError(error); }
            return handle!;
        }
    }
    private static void Validate(RegistrySecurityParts parts)
    {
        if (parts == 0 || (parts & ~(RegistrySecurityParts.Owner | RegistrySecurityParts.Group | RegistrySecurityParts.Access | RegistrySecurityParts.Audit)) != 0)
            throw new ArgumentOutOfRangeException(nameof(parts));
    }
}
