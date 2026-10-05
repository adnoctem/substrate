using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;
using PSFoundation.Registry;

namespace PSFoundation.Security;

/// <summary>Explicit administrative ownership changes. Uses a temporary thread token and restores the caller's identity.</summary>
/// <remarks>Changes are not transactional. Cancellation or failure preserves prior successful changes. Never deletes contents or follows filesystem reparse points.</remarks>
public sealed class OwnershipManager
{
    /// <summary>Changes filesystem ownership and optionally grants full control to a selected SID, skipping reparse points.</summary>
    public void SetFileOwner(FileSystemPath path, SecurityIdentifier owner, SecurityIdentifier? grantFullControl = null, bool recurse = false, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        cancellationToken.ThrowIfCancellationRequested();
        using var privilege = new ThreadPrivilegeScope("SeTakeOwnershipPrivilege", "SeRestorePrivilege");
        var pending = new Stack<string>();
        pending.Push(path.Value);
        var manager = new FileSecurityManager();
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = FileSystemPath.Parse(pending.Pop());
            var attributes = File.GetAttributes(current.Value);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Ownership traversal refuses reparse points: " + current.Value);
            var directory = (attributes & FileAttributes.Directory) != 0;
            manager.Write(current, FileSecurityDescriptor.FromSddl("O:" + owner.Value), FileSecurityParts.Owner);
            if (grantFullControl != null)
            {
                FileSystemSecurity security = directory ? (FileSystemSecurity)new DirectorySecurity() : new FileSecurity();
                security.SetSecurityDescriptorBinaryForm(manager.Read(current, FileSecurityParts.Access).GetBinaryForm(), AccessControlSections.Access);
                security.AddAccessRule(new FileSystemAccessRule(grantFullControl, FileSystemRights.FullControl, directory ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
                manager.Write(current, new FileSecurityDescriptor(security.GetSecurityDescriptorBinaryForm()), FileSecurityParts.Access);
            }
            progress?.Report(current.Value);
            if (recurse && directory)
                foreach (var child in Directory.EnumerateFileSystemEntries(current.Value))
                    pending.Push(child);
        }
    }
    /// <summary>Offloads filesystem ownership changes; cancellation retains changes already made.</summary>
    public Task SetFileOwnerAsync(FileSystemPath path, SecurityIdentifier owner, SecurityIdentifier? grantFullControl = null, bool recurse = false, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() => SetFileOwner(path, owner, grantFullControl, recurse, progress, cancellationToken), cancellationToken);
    /// <summary>Changes registry ownership, optionally recursively and with a full-control grant to the owner.</summary>
    public void SetRegistryOwner(RegistryPath path, SecurityIdentifier owner, bool grantFullControl = true, bool recurse = false, RegistryManager? registry = null, IProgress<RegistryPath>? progress = null, CancellationToken cancellationToken = default)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (path.IsRoot)
            throw new ArgumentException("Hive roots cannot be taken over.", nameof(path));
        var manager = registry ?? new RegistryManager();
        if (manager.MachineName != null)
            throw new NotSupportedException("Ownership takeover with scoped privileges is local only.");
        cancellationToken.ThrowIfCancellationRequested();
        using var privilege = new ThreadPrivilegeScope("SeTakeOwnershipPrivilege", "SeRestorePrivilege");
        var pending = new Stack<RegistryPath>();
        pending.Push(path);
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            manager.Security.Write(current, RegistrySecurityDescriptor.FromSddl("O:" + owner.Value), RegistrySecurityParts.Owner);
            if (grantFullControl)
            {
                var security = new RegistrySecurity();
                security.SetSecurityDescriptorBinaryForm(manager.Security.Read(current, RegistrySecurityParts.Access).GetBinaryForm(), AccessControlSections.Access);
                security.ResetAccessRule(new RegistryAccessRule(owner, RegistryRights.FullControl, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
                security.SetAccessRuleProtection(false, false);
                manager.Security.Write(current, new RegistrySecurityDescriptor(security.GetSecurityDescriptorBinaryForm()), RegistrySecurityParts.Access, false);
            }
            progress?.Report(current);
            if (recurse)
                foreach (var child in manager.GetSubKeys(current))
                    pending.Push(child);
        }
    }
    /// <summary>Offloads registry ownership changes while preserving the caller's identity after temporary privilege use.</summary>
    public Task SetRegistryOwnerAsync(RegistryPath path, SecurityIdentifier owner, bool grantFullControl = true, bool recurse = false, RegistryManager? registry = null, IProgress<RegistryPath>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() => SetRegistryOwner(path, owner, grantFullControl, recurse, registry, progress, cancellationToken), cancellationToken);
}
