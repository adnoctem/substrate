using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading;
using PSFoundation.IO;
using PSFoundation.Security;

namespace PSFoundation.Office;

[Serializable]
public sealed class OfficePathDiagnostic
{
    public string Stage { get; }
    public string ObjectKind { get; }
    public string Path { get; }
    public string? Sid { get; }
    public int? AccessMask { get; }
    public bool? IsInherited { get; }
    public InheritanceFlags? InheritanceFlags { get; }
    public PropagationFlags? PropagationFlags { get; }
    internal OfficePathDiagnostic(string stage, string kind, string path, string? sid = null, int? accessMask = null, AceFlags flags = AceFlags.None)
    {
        Stage = stage;
        ObjectKind = kind;
        Path = path;
        Sid = sid;
        AccessMask = accessMask;
        if (accessMask.HasValue)
        {
            IsInherited = (flags & AceFlags.Inherited) != 0;
            InheritanceFlags = ((flags & AceFlags.ContainerInherit) != 0 ? System.Security.AccessControl.InheritanceFlags.ContainerInherit : 0)
                | ((flags & AceFlags.ObjectInherit) != 0 ? System.Security.AccessControl.InheritanceFlags.ObjectInherit : 0);
            PropagationFlags = ((flags & AceFlags.NoPropagateInherit) != 0 ? System.Security.AccessControl.PropagationFlags.NoPropagateInherit : 0)
                | ((flags & AceFlags.InheritOnly) != 0 ? System.Security.AccessControl.PropagationFlags.InheritOnly : 0);
        }
    }
}
/// <summary>Office deployment path and descriptor checks. Point-in-time validation is not a filesystem transaction.</summary>
public sealed class OfficePathGuard
{
    private const string ProtectedDirectorySddl = "O:S-1-5-32-544G:S-1-5-32-544D:P(A;OICI;FA;;;S-1-5-18)(A;OICI;FA;;;S-1-5-32-544)";
    public FileSystemPath ValidatePath(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || OfficeVersionResolver.IsMatch(path, @"(^|[\\/])\.\.([\\/]|$)")
            || path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new OfficeException(OfficeFailureReason.UnsafePath, "An absolute filesystem path without parent traversal or device namespaces is required.");
        FileSystemPath full;
        try
        { full = FileSystemPath.Parse(path); }
        catch (ArgumentException error) { throw new OfficeException(OfficeFailureReason.UnsafePath, "An absolute filesystem path is required.", error); }
        var root = Path.GetPathRoot(full.Value)!;
        if (full.Value.Substring(root.Length).IndexOf(':') >= 0)
            throw new OfficeException(OfficeFailureReason.UnsafePath, "Alternate data streams are not supported.");
        var cursor = root;
        Check(cursor);
        foreach (var segment in full.Value.Substring(root.Length).Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        { cursor = Path.Combine(cursor, segment); Check(cursor); }
        return full;
        void Check(string candidate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = Attributes(candidate);
            if (attributes.HasValue && (attributes.Value & FileAttributes.ReparsePoint) != 0)
                throw new OfficeException(OfficeFailureReason.UnsafePath, "Reparse traversal is not supported for Office deployment files.");
        }
    }
    public FileSystemPath RequireProtected(string path, string objectKind = "DeploymentFile", CancellationToken cancellationToken = default)
    {
        FileSystemPath full;
        var stage = "Path";
        try
        {
            full = ValidatePath(path, cancellationToken);
            stage = "AccessControlRead";
            var descriptor = new FileSecurityManager().Read(full);
            CheckDescriptor(descriptor, path, objectKind);
        }
        catch (OperationCanceledException) { throw; }
        catch (OfficeException error) when (error.Data.Contains("OfficeDiagnostic")) { throw; }
        catch (Exception error)
        {
            var reason = error is OfficeException office ? office.Reason : OfficeFailureReason.UntrustedMedia;
            throw Failure(reason, "Cannot validate " + objectKind + " at '" + path + "' (" + stage + ").", new OfficePathDiagnostic(stage, objectKind, path), error);
        }
        return full;
    }
    /// <summary>Checks a detached descriptor without I/O. Deny rules never excuse a separate untrusted write grant.</summary>
    public void CheckDescriptor(FileSecurityDescriptor descriptor, string path, string objectKind = "DeploymentFile")
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        var raw = descriptor.ToRawDescriptor();
        var owner = raw.Owner?.Value;
        if (!Trusted(owner))
            throw Failure(OfficeFailureReason.UntrustedMedia, "Untrusted owner SID " + owner + " on " + objectKind + " at '" + path + "'; Administrators or SYSTEM ownership is required.",
            new OfficePathDiagnostic("Owner", objectKind, path, owner));
        if (raw.DiscretionaryAcl == null)
            throw Failure(OfficeFailureReason.UntrustedMedia, "A null DACL grants unrestricted access.", new OfficePathDiagnostic("WriteGrant", objectKind, path));
        foreach (GenericAce ace in raw.DiscretionaryAcl)
        {
            if (!(ace is CommonAce rule) || rule.IsCallback)
                throw Failure(OfficeFailureReason.UntrustedMedia, "Unsupported access-control entry in deployment descriptor.", new OfficePathDiagnostic("AccessControlRead", objectKind, path));
            // Write, delete (including deleting children), ACL/owner changes and generic write/all can alter deployment content.
            const int mutation = 0x000D0156 | 0x10000000 | 0x40000000;
            if (rule.AceQualifier == AceQualifier.AccessAllowed && (rule.AccessMask & mutation) != 0 && !Trusted(rule.SecurityIdentifier.Value))
                throw Failure(OfficeFailureReason.UntrustedMedia, "Untrusted write grant for SID " + rule.SecurityIdentifier.Value + " on " + objectKind + " at '" + path + "'.",
                    new OfficePathDiagnostic("WriteGrant", objectKind, path, rule.SecurityIdentifier.Value, rule.AccessMask, rule.AceFlags));
        }
    }
    /// <summary>Creates a dedicated protected directory. The parent must exist; existing directories must already meet the policy.</summary>
    public FileSystemPath EnsureProtectedDirectory(string path, CancellationToken cancellationToken = default)
    {
        var full = ValidatePath(path, cancellationToken);
        var attributes = Attributes(full.Value);
        if (attributes.HasValue)
        {
            if ((attributes.Value & FileAttributes.Directory) == 0)
                throw new OfficeException(OfficeFailureReason.UnsafePath, "The deployment directory path identifies a file.");
            return RequireProtected(path, cancellationToken: cancellationToken);
        }
        CreateProtectedDirectory(path, cancellationToken);
        // Complete the validation even if cancellation arrives after the directory was created.
        return RequireProtected(path);
    }
    /// <summary>Creates a new directory with the protected descriptor at creation. Fails if the destination already exists.</summary>
    public FileSystemPath CreateProtectedDirectory(string path, CancellationToken cancellationToken = default)
    {
        var full = ValidatePath(path, cancellationToken);
        var parent = Path.GetDirectoryName(full.Value);
        if (parent == null || (Attributes(parent).GetValueOrDefault() & FileAttributes.Directory) == 0)
            throw new OfficeException(OfficeFailureReason.UnsafePath, "The parent directory must already exist.");
        cancellationToken.ThrowIfCancellationRequested();
        new FileSecurityManager().CreateDirectory(full, FileSecurityDescriptor.FromSddl(ProtectedDirectorySddl));
        return full;
    }
    private static FileAttributes? Attributes(string path)
    {
        try
        { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    /// <summary>Removes a caller-owned protected work directory strictly below its stated parent. Never follows links.</summary>
    /// <remarks>Ownership is a caller assertion, not inferred from the directory name. Validation is point-in-time; callers must exclude
    /// concurrent trusted writers. Each directory is checked before enumeration, and deletion uses nonrecursive filesystem operations.</remarks>
    public void RemoveWorkDirectory(string path, string parent)
    {
        var full = ValidatePath(path);
        var root = ValidatePath(parent).Value.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.Value.StartsWith(root, StringComparison.OrdinalIgnoreCase) || full.Value.TrimEnd(Path.DirectorySeparatorChar) == root.TrimEnd(Path.DirectorySeparatorChar))
            throw new OfficeException(OfficeFailureReason.UnsafePath, "Cleanup path escaped its operation root.");
        if (!Attributes(full.Value).HasValue)
            return;
        RequireProtected(full.Value);
        var visited = 0;
        Remove(full.Value, 0);
        void Remove(string candidate, int depth)
        {
            if (depth > 128 || ++visited > 100000)
                throw new OfficeException(OfficeFailureReason.UnsafePath, "Work directory cleanup exceeds traversal limits.");
            ValidatePath(candidate);
            var attributes = File.GetAttributes(candidate);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                // Enumerate one level only after checking this directory; never recurse through a discovered link.
                foreach (var entry in Directory.EnumerateFileSystemEntries(candidate))
                    Remove(entry, depth + 1);
                ValidatePath(candidate);
                Directory.Delete(candidate, false);
            }
            else
            {
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(candidate, attributes & ~FileAttributes.ReadOnly);
                File.Delete(candidate);
            }
        }
    }
    private static bool Trusted(string? sid) => sid == "S-1-5-18" || sid == "S-1-5-32-544";
    private static OfficeException Failure(OfficeFailureReason reason, string message, OfficePathDiagnostic diagnostic, Exception? inner = null)
    {
        var error = new OfficeException(reason, message, inner);
        error.Data["OfficeDiagnostic"] = diagnostic;
        return error;
    }
}
