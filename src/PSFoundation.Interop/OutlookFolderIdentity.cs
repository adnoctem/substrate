using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace PSFoundation.Interop;

public enum OutlookFolderKind
{
    DeletedItems = 3, Outbox = 4, SentItems = 5, Inbox = 6, Calendar = 9, Contacts = 10, Journal = 11,
    Notes = 12, Tasks = 13, Drafts = 16, AllPublicFolders = 18, Conflicts = 19, SyncIssues = 20,
    LocalFailures = 21, ServerFailures = 22, Junk = 23, RssFeeds = 25, ToDo = 28, ManagedEmail = 29, SuggestedContacts = 30
}
public enum OutlookFolderIdentityState { Resolved, Absent, Unavailable, Unresolved }
public sealed class OutlookFolderIdentity
{
    public OutlookFolderKind Kind { get; }
    public string StoreId { get; }
    public string? EntryId { get; }
    public OutlookFolderIdentityState State { get; }
    public string Evidence { get; }
    internal OutlookFolderIdentity(OutlookFolderKind kind, string store, string? entry, OutlookFolderIdentityState state, string evidence)
    { Kind = kind; StoreId = store; EntryId = entry; State = state; Evidence = evidence; }
}

public sealed partial class OutlookManager
{
    private const int MapiNotFound = unchecked((int)0x8004010f);
    private const int MapiNoSupport = unchecked((int)0x80040102);
    private const string MapiTag = "http://schemas.microsoft.com/mapi/proptag/0x";

    public IReadOnlyList<OutlookFolderIdentity> GetStandardFolderIdentities(object session, object storeRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storeId = OutlookDispatch.Text(storeRoot, "StoreID");
        if (string.IsNullOrWhiteSpace(storeId))
            throw new InvalidOperationException("The store identity is empty.");
        object? store = null;
        object? defaultStore = null;
        object? inbox = null;
        var accessors = new List<object>();
        try
        {
            store = OutlookDispatch.Require(OutlookDispatch.Get(storeRoot, "Store"));
            var application = OutlookDispatch.Require(OutlookDispatch.Get(session, "Application"));
            int major;
            try
            { major = int.Parse(OutlookDispatch.Text(application, "Version").Split('.')[0], CultureInfo.InvariantCulture); }
            finally { OutlookDispatch.Release(application); }
            var result = new List<OutlookFolderIdentity>();
            if (major >= 14)
            {
                foreach (OutlookFolderKind kind in Enum.GetValues(typeof(OutlookFolderKind)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    object? folder = null;
                    try
                    {
                        folder = OutlookDispatch.Call(store, "GetDefaultFolder", (int)kind);
                        var entryId = folder == null ? null : OutlookDispatch.Text(folder, "EntryID");
                        if (folder != null && (string.IsNullOrWhiteSpace(entryId) || !OutlookDispatch.Equal(OutlookDispatch.Text(folder, "StoreID"), storeId)))
                            throw new InvalidOperationException("Standard folder identity is empty or belongs to another store.");
                        result.Add(new OutlookFolderIdentity(kind, storeId, entryId, folder == null ? OutlookFolderIdentityState.Absent : OutlookFolderIdentityState.Resolved, "Store.GetDefaultFolder"));
                    }
                    catch (Exception error)
                    {
                        if (error.HResult != MapiNotFound && error.HResult != MapiNoSupport
                            && !(error.HResult == unchecked((int)0x80070057) && OptionalFolder(kind)))
                            throw new InvalidOperationException("Cannot determine standard folder '" + kind + "' in the selected store: " + error.Message, error);
                        result.Add(new OutlookFolderIdentity(kind, storeId, null, OutlookFolderIdentityState.Unavailable, "Store.GetDefaultFolder"));
                    }
                    finally { OutlookDispatch.Release(folder); }
                }
                return result.AsReadOnly();
            }
            if (!(bool)OutlookDispatch.Require(OutlookDispatch.Get(store, "IsDataFileStore")) || !OutlookDispatch.Equal(Path.GetExtension(OutlookDispatch.Text(store, "FilePath")), ".pst"))
                throw new NotSupportedException("Outlook 2007 standard-folder discovery currently supports PST stores only. Use a newer classic Outlook client for Exchange or OST stores.");
            var known = new Dictionary<OutlookFolderKind, string>();
            defaultStore = OutlookDispatch.Require(OutlookDispatch.Get(session, "DefaultStore"));
            if (OutlookDispatch.Equal(OutlookDispatch.Text(defaultStore, "StoreID"), storeId))
            {
                inbox = OutlookDispatch.Require(OutlookDispatch.Call(session, "GetDefaultFolder", 6));
                if (!OutlookDispatch.Equal(OutlookDispatch.Text(inbox, "StoreID"), storeId))
                    throw new InvalidOperationException("Default Inbox belongs to a different store.");
                known[OutlookFolderKind.Inbox] = OutlookDispatch.Text(inbox, "EntryID");
                accessors.Add(OutlookDispatch.Require(OutlookDispatch.Get(inbox, "PropertyAccessor")));
            }
            accessors.Add(OutlookDispatch.Require(OutlookDispatch.Get(storeRoot, "PropertyAccessor")));
            accessors.Add(OutlookDispatch.Require(OutlookDispatch.Get(store, "PropertyAccessor")));
            var tags = new Dictionary<OutlookFolderKind, string>
            {
                [OutlookFolderKind.Outbox] = "35E2",
                [OutlookFolderKind.DeletedItems] = "35E3",
                [OutlookFolderKind.SentItems] = "35E4",
                [OutlookFolderKind.Calendar] = "36D0",
                [OutlookFolderKind.Contacts] = "36D1",
                [OutlookFolderKind.Journal] = "36D2",
                [OutlookFolderKind.Notes] = "36D3",
                [OutlookFolderKind.Tasks] = "36D4",
                [OutlookFolderKind.Drafts] = "36D7"
            };
            foreach (var accessor in accessors)
            {
                foreach (var tag in tags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (known.ContainsKey(tag.Key))
                        continue;
                    var binary = ReadOptionalMapi(accessor, tag.Value + "0102");
                    if (binary is byte[] bytes && bytes.Length > 0)
                        known[tag.Key] = BinaryId(accessor, bytes);
                    else if (binary != null && !(binary is byte[]))
                        throw new InvalidDataException("Invalid binary folder identity property.");
                }
                var additional = ReadOptionalMapi(accessor, "36D81102");
                if (additional != null)
                {
                    if (!(additional is Array array) || array.Rank != 1)
                        throw new InvalidDataException("Invalid multi-valued folder identity property.");
                    var kinds = new[] { OutlookFolderKind.Conflicts, OutlookFolderKind.SyncIssues, OutlookFolderKind.LocalFailures, OutlookFolderKind.ServerFailures, OutlookFolderKind.Junk };
                    for (var index = 0; index < Math.Min(array.Length, kinds.Length); index++)
                    {
                        var value = array.GetValue(index + array.GetLowerBound(0));
                        if (value is byte[] bytes && bytes.Length > 0 && !known.ContainsKey(kinds[index]))
                            known[kinds[index]] = BinaryId(accessor, bytes);
                        else if (value != null && !(value is byte[]))
                            throw new InvalidDataException("Invalid additional folder identity.");
                    }
                }
                var extended = ReadOptionalMapi(accessor, "36D90102");
                if (extended != null)
                {
                    if (!(extended is byte[] data))
                        throw new InvalidDataException("Invalid extended folder identity property.");
                    foreach (var pair in ParseAdditionalFolderEntryIds(data))
                        if (!known.ContainsKey(pair.Key))
                            known[pair.Key] = BinaryId(accessor, pair.Value);
                }
            }
            foreach (OutlookFolderKind kind in Enum.GetValues(typeof(OutlookFolderKind)))
            {
                var resolved = known.TryGetValue(kind, out var entry);
                result.Add(new OutlookFolderIdentity(kind, storeId, entry, resolved ? OutlookFolderIdentityState.Resolved : kind == OutlookFolderKind.Inbox ? OutlookFolderIdentityState.Unresolved : OutlookFolderIdentityState.Absent, "Outlook2007.MAPI"));
            }
            return result.AsReadOnly();
        }
        finally
        {
            foreach (var accessor in accessors)
                OutlookDispatch.Release(accessor);
            OutlookDispatch.Release(inbox);
            OutlookDispatch.Release(defaultStore);
            OutlookDispatch.Release(store);
        }
    }

    /// <summary>Parses bounded little-endian PersistData/PersistElement records without accessing Outlook.</summary>
    public static IReadOnlyDictionary<OutlookFolderKind, byte[]> ParseAdditionalFolderEntryIds(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Extended folder identities exceed the supported size.");
        var result = new Dictionary<OutlookFolderKind, byte[]>();
        var offset = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < 4)
                throw new InvalidDataException("Truncated PersistData header.");
            var id = UInt16(offset);
            var size = UInt16(offset + 2);
            offset += 4;
            if (id == 0)
                break;
            var end = offset + size;
            if (end > data.Length)
                throw new InvalidDataException("PersistData exceeds property bounds.");
            OutlookFolderKind? kind = id == 0x8001 ? OutlookFolderKind.RssFeeds : id == 0x8004 ? OutlookFolderKind.ToDo : id == 0x8008 ? OutlookFolderKind.SuggestedContacts : (OutlookFolderKind?)null;
            if (!kind.HasValue)
            { offset = end; continue; }
            while (offset < end)
            {
                if (end - offset < 4)
                    throw new InvalidDataException("Truncated PersistElement header.");
                var element = UInt16(offset);
                var length = UInt16(offset + 2);
                offset += 4;
                if (offset + length > end)
                    throw new InvalidDataException("PersistElement exceeds block bounds.");
                if (element == 0)
                {
                    if (length != 0)
                        throw new InvalidDataException("ELEMENT_SENTINEL must have zero length.");
                    offset = end;
                    break;
                }
                if (element == 1 && length > 0 && !result.ContainsKey(kind.Value))
                { var bytes = new byte[length]; Array.Copy(data, offset, bytes, 0, length); result.Add(kind.Value, bytes); }
                offset += length;
            }
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<OutlookFolderKind, byte[]>(result);
        int UInt16(int position) => data[position] | data[position + 1] << 8;
    }

    private static string BinaryId(object accessor, byte[] value)
    {
        var result = Convert.ToString(OutlookDispatch.Call(accessor, "BinaryToString", value), CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidDataException("Outlook returned an empty binary folder identity.");
        return result!;
    }
    private static object? ReadOptionalMapi(object accessor, string tag)
    {
        try
        { return OutlookDispatch.Call(accessor, "GetProperty", MapiTag + tag); }
        catch (Exception error) when (error.HResult == MapiNotFound) { return null; }
    }
    private static bool OptionalFolder(OutlookFolderKind kind) => kind == OutlookFolderKind.AllPublicFolders || kind == OutlookFolderKind.ManagedEmail || kind == OutlookFolderKind.SuggestedContacts || kind == OutlookFolderKind.ToDo || kind == OutlookFolderKind.RssFeeds;
}
