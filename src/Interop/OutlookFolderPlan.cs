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
    internal OutlookFolderPlanEntry(string entry, string store, string path, string relative, OutlookFolderKind? kind, bool process, bool traverse, string reason)
    { EntryId = entry; StoreId = store; FolderPath = path; RelativePath = relative; StandardKind = kind; Process = process; Traverse = traverse; Reason = reason; }
}

public sealed partial class OutlookManager
{
    /// <summary>Collects a complete read-only folder plan. Null selects Inbox by identity, empty selects the root, otherwise an exact relative path.</summary>
    /// <remarks>No COM references escape in the plan. Cancellation and inspection failures discard partial results. Plans do not authorize mutation.</remarks>
    public IReadOnlyList<OutlookFolderPlanEntry> GetFolderPlan(object session, object storeRoot, string? folderName = null, bool recurse = false,
        IEnumerable<OutlookFolderKind>? include = null, IEnumerable<string>? exclusions = null, Action<string>? progress = null, CancellationToken cancellationToken = default)
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
        var identities = GetStandardFolderIdentities(session, storeRoot, cancellationToken);
        var kinds = new Dictionary<string, OutlookFolderKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in identities)
        {
            if (identity.State == OutlookFolderIdentityState.Unresolved && !included.Contains(identity.Kind))
                throw new InvalidOperationException("Cannot enforce exclusion of '" + identity.Kind + "' on this store and Outlook version. Select a supported store or explicitly include this kind.");
            if (!string.IsNullOrEmpty(identity.EntryId))
                kinds[identity.EntryId!] = identity.Kind;
        }
        var storeId = OutlookDispatch.Text(storeRoot, "StoreID");
        string? implicitInboxId = null;
        if (folderName == null)
        {
            var inboxIdentity = identities.Single(item => item.Kind == OutlookFolderKind.Inbox);
            if (inboxIdentity.State != OutlookFolderIdentityState.Resolved || string.IsNullOrWhiteSpace(inboxIdentity.EntryId) || !OutlookDispatch.Equal(inboxIdentity.StoreId, storeId))
                throw new InvalidOperationException("Cannot resolve the selected store Inbox identity. Supply FolderName explicitly.");
            implicitInboxId = inboxIdentity.EntryId;
            var inbox = OutlookDispatch.Call(session, "GetFolderFromID", implicitInboxId, storeId);
            try
            {
                if (inbox == null || !OutlookDispatch.Equal(OutlookDispatch.Text(inbox, "StoreID"), storeId) || !OutlookDispatch.Equal(OutlookDispatch.Text(inbox, "EntryID"), implicitInboxId))
                    throw new InvalidOperationException("Resolved Inbox does not match the selected store and folder identity.");
                var prefix = OutlookDispatch.Text(storeRoot, "FolderPath").TrimEnd('\\') + "\\";
                var path = OutlookDispatch.Text(inbox, "FolderPath");
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Resolved Inbox is outside the selected store root.");
                folderName = path.Substring(prefix.Length);
                ValidateFolderPath(folderName, false);
            }
            finally { OutlookDispatch.Release(inbox); }
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
                        throw new InvalidOperationException("FolderName '" + folderName + "' was not found. Use the displayed folder path, for example Posteingang.");
                    if (!ReferenceEquals(selected, storeRoot))
                        OutlookDispatch.Release(selected);
                    selected = next;
                    relative = Join(relative, OutlookDispatch.Text(selected, "Name"));
                    var decision = Decide(selected, relative);
                    if (!decision.Traverse)
                        throw new InvalidOperationException("Selected folder is excluded by '" + decision.Reason + "' at '" + relative + "'.");
                }
            }
            if (implicitInboxId != null && !OutlookDispatch.Equal(OutlookDispatch.Text(selected, "EntryID"), implicitInboxId))
                throw new InvalidOperationException("Inbox identity changed while resolving its path. Request a new folder plan.");
            Walk(selected, relative, 0);
            return result.AsReadOnly();
        }
        finally { if (!ReferenceEquals(selected, storeRoot)) OutlookDispatch.Release(selected); }

        OutlookFolderPlanEntry Decide(object folder, string path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryId = OutlookDispatch.Text(folder, "EntryID");
            var currentStore = OutlookDispatch.Text(folder, "StoreID");
            if (string.IsNullOrWhiteSpace(entryId) || !OutlookDispatch.Equal(currentStore, storeId))
                throw new InvalidOperationException("Folder identity is empty or belongs to another store.");
            OutlookFolderKind? kind = kinds.TryGetValue(entryId, out var known) ? known : (OutlookFolderKind?)null;
            string? reason = excluded.Any(item => OutlookDispatch.Equal(path, item) || path.StartsWith(item + "\\", StringComparison.OrdinalIgnoreCase)) ? "CustomExclusion" : null;
            if (reason == null && kind.HasValue && !included.Contains(kind.Value))
                reason = "StandardFolder:" + kind + ":Include" + kind + " required";
            var accessor = OutlookDispatch.Require(OutlookDispatch.Get(folder, "PropertyAccessor"));
            try
            { if (Convert.ToInt32(OutlookDispatch.Call(accessor, "GetProperty", MapiTag + "36010003"), System.Globalization.CultureInfo.InvariantCulture) == 2) reason = "SearchFolder"; }
            finally { OutlookDispatch.Release(accessor); }
            var mail = Convert.ToInt32(OutlookDispatch.Get(folder, "DefaultItemType"), System.Globalization.CultureInfo.InvariantCulture) == 0;
            return new OutlookFolderPlanEntry(entryId, currentStore, OutlookDispatch.Text(folder, "FolderPath"), path, kind, reason == null && mail, reason == null, reason ?? (mail ? "Included" : "NonMailContainer"));
        }
        void Walk(object folder, string path, int depth)
        {
            if (depth > 256 || result.Count >= 100000)
                throw new InvalidOperationException("Outlook folder traversal exceeds its supported bounds.");
            var decision = Decide(folder, path);
            if (!visited.Add(decision.EntryId))
                throw new InvalidOperationException("Outlook returned a repeated folder identity.");
            progress?.Invoke(decision.FolderPath);
            result.Add(decision);
            if (!recurse || !decision.Traverse)
                return;
            var folders = OutlookDispatch.Require(OutlookDispatch.Get(folder, "Folders"));
            try
            {
                var count = OutlookDispatch.Count(folders);
                for (var index = 1; index <= count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var child = OutlookDispatch.Require(OutlookDispatch.Call(folders, "Item", index));
                    try
                    { Walk(child, Join(path, OutlookDispatch.Text(child, "Name")), depth + 1); }
                    finally { OutlookDispatch.Release(child); }
                }
            }
            finally { OutlookDispatch.Release(folders); }
        }
    }
    private static string Join(string parent, string name) => parent.Length == 0 ? name : parent + "\\" + name;
    private static void ValidateFolderPath(string? path, bool allowEmpty)
    {
        if (path == null || path == "" && !allowEmpty || path != "" && path.Split('\\').Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".."))
            throw new ArgumentException("Folder paths must be exact, nonblank store-relative paths. Only FolderName may be empty for the store root.", nameof(path));
    }
}
