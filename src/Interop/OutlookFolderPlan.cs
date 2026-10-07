using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AdNoctem.Substrate.Interop;

public sealed class OutlookFolderPlanEntry
{
    public string EntryId { get; }
    public string StoreId { get; }
    public string FolderPath { get; }
    public string RelativePath { get; }
    public OutlookFolderKind? StandardKind { get; }
    public bool Process { get; }
    public bool Traverse { get; }
    public string Reason { get; }

    internal OutlookFolderPlanEntry(
        string entry,
        string store,
        string path,
        string relative,
        OutlookFolderKind? kind,
        bool process,
        bool traverse,
        string reason
    )
    {
        EntryId = entry;
        StoreId = store;
        FolderPath = path;
        RelativePath = relative;
        StandardKind = kind;
        Process = process;
        Traverse = traverse;
        Reason = reason;
    }
}

public sealed partial class OutlookManager
{
    /// <summary>Collects a read-only folder plan. Null requires Inbox by identity, empty selects the root, otherwise an exact relative path.</summary>
    /// <remarks>Uncertain excluded identities cause potentially matching branches to be skipped with an IncompleteIdentity reason and a diagnostic.
    /// Resolved identities and item types constrain classification; names are never used to guess standard kinds.
    /// No COM references escape. Cancellation and source inspection failures discard partial results. Plans do not authorize mutation.</remarks>
    public IReadOnlyList<OutlookFolderPlanEntry> GetFolderPlan(
        object session,
        object storeRoot,
        string? folderName = null,
        bool recurse = false,
        IEnumerable<OutlookFolderKind>? include = null,
        IEnumerable<string>? exclusions = null,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default,
        Action<string>? diagnostic = null
    )
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        if (storeRoot == null)
            throw new ArgumentNullException(nameof(storeRoot));

        cancellationToken.ThrowIfCancellationRequested();

        if (folderName != null)
            ValidateFolderPath(folderName, true);

        var excluded = (exclusions ?? Array.Empty<string>()).ToArray();

        foreach (var path in excluded)
            ValidateFolderPath(path, false);

        var included = new HashSet<OutlookFolderKind>(include ?? new[] { OutlookFolderKind.Inbox });

        if (included.Any(kind => !Enum.IsDefined(typeof(OutlookFolderKind), kind)))
            throw new ArgumentOutOfRangeException(nameof(include));

        var identities = GetStandardFolderIdentities(
            session,
            storeRoot,
            cancellationToken,
            identity =>
            {
                if (identity.State != OutlookFolderIdentityState.Absent)
                    diagnostic?.Invoke(
                        "Standard folder '"
                            + identity.Kind
                            + "' in store '"
                            + identity.StoreId
                            + "': "
                            + identity.State
                            + ". "
                            + identity.Evidence
                            + ". Folder classification is incomplete."
                    );
            }
        );
        var uncertain = identities
            .Where(identity =>
                (
                    identity.State == OutlookFolderIdentityState.Unresolved
                    || identity.State == OutlookFolderIdentityState.Unavailable
                ) && !included.Contains(identity.Kind)
            )
            .Select(identity => identity.Kind)
            .ToArray();
        var kinds = new Dictionary<string, OutlookFolderKind>(StringComparer.OrdinalIgnoreCase);

        foreach (var identity in identities)
        {
            if (
                identity.State == OutlookFolderIdentityState.Resolved
                && !string.IsNullOrEmpty(identity.EntryId)
            )
                kinds[identity.EntryId!] = identity.Kind;
        }

        var storeId = OutlookDispatch.Text(storeRoot, "StoreID");
        var rootId = OutlookDispatch.Text(storeRoot, "EntryID");
        string? implicitInboxId = null;

        if (folderName == null)
        {
            var inboxIdentity = identities.Single(item => item.Kind == OutlookFolderKind.Inbox);

            if (
                inboxIdentity.State != OutlookFolderIdentityState.Resolved
                || string.IsNullOrWhiteSpace(inboxIdentity.EntryId)
                || !OutlookDispatch.Equal(inboxIdentity.StoreId, storeId)
            )
                throw new InvalidOperationException(
                    "Cannot resolve the selected store Inbox identity (StoreID '"
                        + storeId
                        + "', "
                        + inboxIdentity.State
                        + "). "
                        + inboxIdentity.Evidence
                        + ". Supply FolderName explicitly to select an existing folder or the store root."
                );

            implicitInboxId = inboxIdentity.EntryId;
            var inbox = OutlookDispatch.Call(session, "GetFolderFromID", implicitInboxId, storeId);

            try
            {
                if (
                    inbox == null
                    || !OutlookDispatch.Equal(OutlookDispatch.Text(inbox, "StoreID"), storeId)
                    || !OutlookDispatch.Equal(
                        OutlookDispatch.Text(inbox, "EntryID"),
                        implicitInboxId
                    )
                )
                    throw new InvalidOperationException(
                        "Resolved Inbox does not match the selected store and folder identity."
                    );

                var prefix = OutlookDispatch.Text(storeRoot, "FolderPath").TrimEnd('\\') + "\\";
                var path = OutlookDispatch.Text(inbox, "FolderPath");

                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Resolved Inbox is outside the selected store root."
                    );

                folderName = path.Substring(prefix.Length);
                ValidateFolderPath(folderName, false);
            }
            finally
            {
                OutlookDispatch.Release(inbox);
            }
        }

        var result = new List<OutlookFolderPlanEntry>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        object selected = storeRoot;
        var relative = "";

        try
        {
            if (folderName != "")
            {
                foreach (var segment in folderName!.Split('\\'))
                {
                    var next = GetSubFolder(selected, segment, false, cancellationToken);

                    if (next == null)
                        throw new InvalidOperationException(
                            "FolderName '"
                                + folderName
                                + "' was not found. Use the displayed folder path, for example Posteingang."
                        );

                    if (!ReferenceEquals(selected, storeRoot))
                        OutlookDispatch.Release(selected);

                    selected = next;
                    relative = Join(relative, OutlookDispatch.Text(selected, "Name"));
                    var decision = Decide(selected, relative);

                    if (!decision.Traverse)
                        throw new InvalidOperationException(
                            "Selected folder is excluded by '"
                                + decision.Reason
                                + "' at '"
                                + relative
                                + "'."
                        );
                }
            }

            if (
                implicitInboxId != null
                && !OutlookDispatch.Equal(
                    OutlookDispatch.Text(selected, "EntryID"),
                    implicitInboxId
                )
            )
                throw new InvalidOperationException(
                    "Inbox identity changed while resolving its path. Request a new folder plan."
                );

            Walk(selected, relative, 0);

            return result.AsReadOnly();
        }
        finally
        {
            if (!ReferenceEquals(selected, storeRoot))
                OutlookDispatch.Release(selected);
        }

        OutlookFolderPlanEntry Decide(object folder, string path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryId = OutlookDispatch.Text(folder, "EntryID");
            var currentStore = OutlookDispatch.Text(folder, "StoreID");

            if (string.IsNullOrWhiteSpace(entryId) || !OutlookDispatch.Equal(currentStore, storeId))
                throw new InvalidOperationException(
                    "Folder identity is empty or belongs to another store."
                );

            OutlookFolderKind? kind = kinds.TryGetValue(entryId, out var known)
                ? known
                : (OutlookFolderKind?)null;
            string? reason = excluded.Any(item =>
                OutlookDispatch.Equal(path, item)
                || path.StartsWith(item + "\\", StringComparison.OrdinalIgnoreCase)
            )
                ? "CustomExclusion"
                : null;

            if (reason == null && kind.HasValue && !included.Contains(kind.Value))
                reason = "StandardFolder:" + kind + ":Include" + kind + " required";

            var accessor = OutlookDispatch.Require(OutlookDispatch.Get(folder, "PropertyAccessor"));

            try
            {
                if (
                    Convert.ToInt32(
                        OutlookDispatch.Call(accessor, "GetProperty", MapiTag + "36010003"),
                        System.Globalization.CultureInfo.InvariantCulture
                    ) == 2
                )
                    reason = "SearchFolder";
            }
            finally
            {
                OutlookDispatch.Release(accessor);
            }

            var itemType = Convert.ToInt32(
                OutlookDispatch.Get(folder, "DefaultItemType"),
                System.Globalization.CultureInfo.InvariantCulture
            );
            var mail = itemType == 0;
            var isRoot = OutlookDispatch.Equal(entryId, rootId);
            var ambiguous = !kind.HasValue
                ? uncertain.Where(candidate => CouldBeFolderKind(candidate, itemType)).ToArray()
                : Array.Empty<OutlookFolderKind>();

            if (reason == null && !isRoot && ambiguous.Length != 0)
                reason = "IncompleteIdentity:" + string.Join(",", ambiguous);

            // The store root is a traversal boundary, not an unidentified default folder.
            // Do not process its own items while mail-folder exclusions are uncertain.
            var rootIncomplete = reason == null && isRoot && ambiguous.Length != 0;

            return new OutlookFolderPlanEntry(
                entryId,
                currentStore,
                OutlookDispatch.Text(folder, "FolderPath"),
                path,
                kind,
                reason == null && mail && !rootIncomplete,
                reason == null,
                reason
                    ?? (
                        rootIncomplete ? "IncompleteIdentity:" + string.Join(",", ambiguous)
                        : mail ? "Included"
                        : "NonMailContainer"
                    )
            );
        }
        void Walk(object folder, string path, int depth)
        {
            if (depth > 256 || result.Count >= 100000)
                throw new InvalidOperationException(
                    "Outlook folder traversal exceeds its supported bounds."
                );

            var decision = Decide(folder, path);

            if (!visited.Add(decision.EntryId))
                throw new InvalidOperationException("Outlook returned a repeated folder identity.");

            progress?.Invoke(decision.FolderPath);
            result.Add(decision);

            if (decision.Reason.StartsWith("IncompleteIdentity:", StringComparison.Ordinal))
                diagnostic?.Invoke(
                    "Skipped "
                        + (decision.Traverse ? "items in" : "branch")
                        + " '"
                        + decision.FolderPath
                        + "' (StoreID '"
                        + decision.StoreId
                        + "', EntryID '"
                        + decision.EntryId
                        + "'): "
                        + decision.Reason
                        + "."
                );

            if (!recurse || !decision.Traverse)
                return;

            var folders = OutlookDispatch.Require(OutlookDispatch.Get(folder, "Folders"));

            try
            {
                var count = OutlookDispatch.Count(folders);

                for (var index = 1; index <= count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var child = OutlookDispatch.Require(
                        OutlookDispatch.Call(folders, "Item", index)
                    );

                    try
                    {
                        Walk(child, Join(path, OutlookDispatch.Text(child, "Name")), depth + 1);
                    }
                    finally
                    {
                        OutlookDispatch.Release(child);
                    }
                }
            }
            finally
            {
                OutlookDispatch.Release(folders);
            }
        }
    }

    // OlItemType identifies the container's default item class, not its localized name.
    // Group folders and mail kinds cannot safely be narrowed by item class.
    private static bool CouldBeFolderKind(OutlookFolderKind kind, int itemType) =>
        itemType < 0
        || itemType > 7
        || kind switch
        {
            OutlookFolderKind.Calendar => itemType == 1,
            OutlookFolderKind.Contacts or OutlookFolderKind.SuggestedContacts => itemType == 2
                || itemType == 7,
            OutlookFolderKind.Tasks => itemType == 3,
            OutlookFolderKind.Journal => itemType == 4,
            OutlookFolderKind.Notes => itemType == 5,
            _ => true,
        };

    private static string Join(string parent, string name) =>
        parent.Length == 0 ? name : parent + "\\" + name;

    private static void ValidateFolderPath(string? path, bool allowEmpty)
    {
        if (
            path == null
            || path == "" && !allowEmpty
            || path != ""
                && path.Split('\\')
                    .Any(segment =>
                        string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".."
                    )
        )
            throw new ArgumentException(
                "Folder paths must be exact, nonblank store-relative paths. Only FolderName may be empty for the store root.",
                nameof(path)
            );
    }
}
