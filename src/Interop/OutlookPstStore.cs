using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Interop;

/// <summary>Owns one root reference and, when indicated, the attachment created by OpenPstStore. The namespace is borrowed.</summary>
/// <remarks>Dispose on the creating thread after releasing all child references. Never serialize or share a lifetime across processes.
/// Overlapping consumers are not reference-counted. Disposal never deletes a PST or quits Outlook.</remarks>
public sealed class OutlookPstStore : IDisposable
{
    private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
    private readonly OutlookManager manager;
    public string Path { get; }
    public string StoreId { get; }
    public string DisplayName { get; }
    public object? Root { get; private set; }
    public object Namespace { get; }
    public bool AttachedByCall { get; }
    public bool Closed { get; private set; }

    internal OutlookPstStore(
        OutlookManager manager,
        string path,
        string id,
        string displayName,
        object root,
        object session,
        bool attached
    )
    {
        this.manager = manager;
        Path = path;
        StoreId = id;
        DisplayName = displayName;
        Root = root;
        Namespace = session;
        AttachedByCall = attached;
    }

    public void Dispose()
    {
        if (Closed)
            return;

        if (Thread.CurrentThread.ManagedThreadId != threadId)
            throw new InvalidOperationException("Close the PST context on its acquiring thread.");

        try
        {
            if (AttachedByCall)
                manager.RemovePstAttachment(Namespace, Path, StoreId, Root);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "Cannot detach PST '" + Path + "': " + error.Message,
                error
            );
        }
        finally
        {
            OutlookDispatch.Release(Root);
            Root = null;
            Closed = true;
        }
    }
}

public sealed partial class OutlookManager
{
    /// <summary>Returns an ownership-aware context for an existing writable local PST, or null if attachment is needed but disallowed.</summary>
    /// <remarks>Rechecks before attaching; Outlook has no atomic existing-only open. Callers must prevent concurrent file/profile changes.
    /// Rejects network/reparse paths; short names are expanded but hard-link aliases are not detected. Cancellation after attachment cleans up.</remarks>
    public OutlookPstStore? OpenPstStore(
        object session,
        FileSystemPath path,
        bool allowAttachment = true,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolved = ResolveOutlookFile(path, true);
        var snapshot = ReadPstSnapshot(session, resolved, cancellationToken);

        if (snapshot.Matches.Count > 1)
            throw new InvalidOperationException(
                "Multiple Outlook stores match PST '" + resolved + "'."
            );

        if (snapshot.Matches.Count == 0 && !allowAttachment)
            return null;

        var initialIds = snapshot.Ids;
        var attempted = false;
        string? ownedId = null;
        object? root = null;

        try
        {
            if (snapshot.Matches.Count == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (
                    !OutlookDispatch.Equal(
                        ResolveOutlookFile(FileSystemPath.Parse(resolved), true),
                        resolved
                    )
                )
                    throw new InvalidOperationException(
                        "PST path changed before attachment: '" + resolved + "'."
                    );

                attempted = true;
                OutlookDispatch.Call(session, "AddStore", resolved);
                snapshot = ReadPstSnapshot(session, resolved, cancellationToken);

                if (snapshot.Matches.Count != 1 || initialIds.Contains(snapshot.Matches[0].Id))
                    throw new InvalidOperationException(
                        "No uniquely identified new Outlook attachment for PST '" + resolved + "'."
                    );

                ownedId = snapshot.Matches[0].Id;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var match = snapshot.Matches[0];
            root = GetPstRoot(session, resolved, match.Id);
            var context = new OutlookPstStore(
                this,
                resolved,
                match.Id,
                match.DisplayName,
                root,
                session,
                ownedId != null
            );
            root = null;

            return context;
        }
        catch (Exception failure)
        {
            if (attempted)
            {
                try
                {
                    if (ownedId == null)
                    {
                        var current = ReadPstSnapshot(session, resolved, CancellationToken.None);

                        if (current.Matches.Count > 1)
                            throw new InvalidOperationException(
                                "Cannot identify a unique attachment to clean up."
                            );

                        if (
                            current.Matches.Count == 1
                            && !initialIds.Contains(current.Matches[0].Id)
                        )
                            ownedId = current.Matches[0].Id;
                    }

                    if (ownedId != null)
                        RemovePstAttachment(session, resolved, ownedId, root);
                }
                catch (Exception cleanup)
                {
                    failure.Data["OutlookPstCleanupError"] =
                        "Cleanup of PST '"
                        + resolved
                        + "' could not be completed: "
                        + cleanup.Message;
                }
            }

            throw;
        }
        finally
        {
            OutlookDispatch.Release(root);
        }
    }

    internal void RemovePstAttachment(object session, string path, string storeId, object? root)
    {
        var current = ReadPstSnapshot(session, path, CancellationToken.None);

        if (!current.Ids.Contains(storeId))
            return;

        if (current.Matches.Count(match => OutlookDispatch.Equal(match.Id, storeId)) != 1)
            throw new InvalidOperationException(
                "The recorded Outlook StoreID no longer matches PST '" + path + "'."
            );

        object? temporaryRoot = null;

        try
        {
            if (root == null)
                root = temporaryRoot = GetPstRoot(session, path, storeId);

            if (!OutlookDispatch.Equal(OutlookDispatch.Text(root, "StoreID"), storeId))
                throw new InvalidOperationException(
                    "The owned Outlook root no longer matches PST '" + path + "'."
                );

            OutlookDispatch.Call(session, "RemoveStore", root);
        }
        finally
        {
            OutlookDispatch.Release(temporaryRoot);
        }
    }

    private static PstSnapshot ReadPstSnapshot(
        object session,
        string path,
        CancellationToken cancellationToken
    )
    {
        var snapshot = new PstSnapshot();
        object? stores = null;

        try
        {
            stores = OutlookDispatch.Require(OutlookDispatch.Get(session, "Stores"));
            var count = OutlookDispatch.Count(stores);

            for (var index = 1; index <= count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var store = OutlookDispatch.Require(OutlookDispatch.Call(stores, "Item", index));

                try
                {
                    var id = OutlookDispatch.Text(store, "StoreID");

                    if (string.IsNullOrWhiteSpace(id) || !snapshot.Ids.Add(id))
                        throw new InvalidOperationException(
                            "Outlook returned missing or duplicate StoreIDs."
                        );

                    var file = OutlookDispatch.Text(store, "FilePath");

                    if (string.IsNullOrWhiteSpace(file))
                        continue;

                    if (
                        OutlookDispatch.Equal(
                            ResolveOutlookFile(FileSystemPath.Parse(file), false),
                            path
                        )
                    )
                        snapshot.Matches.Add(
                            new PstMatch(id, OutlookDispatch.Text(store, "DisplayName"))
                        );
                }
                finally
                {
                    OutlookDispatch.Release(store);
                }
            }

            return snapshot;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "Cannot inspect Outlook stores for PST '" + path + "': " + error.Message,
                error
            );
        }
        finally
        {
            OutlookDispatch.Release(stores);
        }
    }

    private static object GetPstRoot(object session, string path, string storeId)
    {
        object? store = null;
        object? root = null;

        try
        {
            store = OutlookDispatch.Require(
                OutlookDispatch.Call(session, "GetStoreFromID", storeId)
            );

            if (
                !OutlookDispatch.Equal(OutlookDispatch.Text(store, "StoreID"), storeId)
                || !OutlookDispatch.Equal(
                    ResolveOutlookFile(
                        FileSystemPath.Parse(OutlookDispatch.Text(store, "FilePath")),
                        false
                    ),
                    path
                )
            )
                throw new InvalidOperationException(
                    "Outlook store identity changed for PST '" + path + "'."
                );

            root = OutlookDispatch.Require(OutlookDispatch.Call(store, "GetRootFolder"));

            if (!OutlookDispatch.Equal(OutlookDispatch.Text(root, "StoreID"), storeId))
                throw new InvalidOperationException(
                    "Outlook returned no matching root for PST '" + path + "'."
                );

            var owned = root;
            root = null;

            return owned;
        }
        finally
        {
            OutlookDispatch.Release(root);
            OutlookDispatch.Release(store);
        }
    }

    private static string ResolveOutlookFile(FileSystemPath path, bool sourcePst)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        var value = path.Value;

        if (
            value.Length < 3
            || !char.IsLetter(value[0])
            || value[1] != ':'
            || value[2] != '\\'
            || value.IndexOf(':', 2) >= 0
        )
            throw new ArgumentException(
                "A local filesystem path is required: '" + value + "'.",
                nameof(path)
            );

        var root = Path.GetPathRoot(value)!;
        var driveType = new DriveInfo(root).DriveType;

        if (driveType != DriveType.Fixed && driveType != DriveType.Removable)
            throw new IOException(
                "Network or unavailable drives are not supported: '" + value + "'."
            );

        var ancestors = new Stack<string>();
        string? current = value;

        while (!string.IsNullOrEmpty(current))
        {
            ancestors.Push(current!);
            current = Path.GetDirectoryName(current);
        }

        foreach (var ancestor in ancestors)
            if ((File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0)
                throw new IOException(
                    "Reparse-point paths are not supported for Outlook file identity: '"
                        + ancestor
                        + "'."
                );

        var attributes = File.GetAttributes(value);

        if ((attributes & FileAttributes.Directory) != 0)
            throw new IOException("Expected an existing file: '" + value + "'.");

        if (
            sourcePst
            && (
                !OutlookDispatch.Equal(Path.GetExtension(value), ".pst")
                || (attributes & FileAttributes.ReadOnly) != 0
            )
        )
            throw new IOException(
                "The source must be an existing writable .pst file: '" + value + "'."
            );

        return path.ExpandLongName().Value;
    }

    private sealed class PstMatch
    {
        internal string Id { get; }
        internal string DisplayName { get; }

        internal PstMatch(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }

    private sealed class PstSnapshot
    {
        internal HashSet<string> Ids { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal List<PstMatch> Matches { get; } = new List<PstMatch>();
    }
}
