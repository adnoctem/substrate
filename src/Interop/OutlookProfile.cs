using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Interop;

/// <summary>OlAccountType. Unknown preserves an unrecognized provider value instead of inferring a behavior from it.</summary>
public enum OutlookAccountKind
{
    Unknown = -1,
    Exchange = 0,
    Imap = 1,
    Pop3 = 2,
    Http = 3,
    ExchangeActiveSync = 4,
    Other = 5,
}

/// <summary>Backing file shape of a store. None describes a store with no local file, such as Exchange in online mode.</summary>
public enum OutlookDataFileFormat
{
    None = 0,
    Pst = 1,
    Ost = 2,
    Other = 3,
}

/// <summary>One account in the current profile and the store it delivers new mail into.</summary>
public sealed class OutlookAccountInfo
{
    public string DisplayName { get; }
    public string SmtpAddress { get; }
    public string UserName { get; }

    /// <summary>Raw OlAccountType, retained so an unmapped provider value stays reportable.</summary>
    public int AccountType { get; }
    public OutlookAccountKind Kind { get; }

    /// <summary>Provider label as Outlook's own account list names it, or the raw value when unmapped.</summary>
    public string KindName { get; }
    public string DeliveryStoreId { get; }
    public string DeliveryStoreName { get; }
    public string DeliveryFilePath { get; }

    /// <summary>Provider errors met while reading this account. Empty when every property was readable.</summary>
    public string Evidence { get; }

    /// <summary>Folders mirror server state, so local removals can reach the server and other clients.</summary>
    public bool IsServerSynchronized =>
        Kind == OutlookAccountKind.Exchange
        || Kind == OutlookAccountKind.Imap
        || Kind == OutlookAccountKind.Http
        || Kind == OutlookAccountKind.ExchangeActiveSync;

    /// <summary>
    /// The provider can leave an item as a header-only stub. A file extension cannot establish this: Outlook 2007 backs an
    /// IMAP account with a PST, so the owning account is the only reliable signal that bodies may still be on the server.
    /// </summary>
    public bool MayDeferItemBodies =>
        Kind == OutlookAccountKind.Imap
        || Kind == OutlookAccountKind.Http
        || Kind == OutlookAccountKind.ExchangeActiveSync;

    internal OutlookAccountInfo(
        string displayName,
        string smtpAddress,
        string userName,
        int accountType,
        string deliveryStoreId,
        string deliveryStoreName,
        string deliveryFilePath,
        string evidence
    )
    {
        DisplayName = displayName;
        SmtpAddress = smtpAddress;
        UserName = userName;
        AccountType = accountType;
        Kind = Enum.IsDefined(typeof(OutlookAccountKind), accountType)
            ? (OutlookAccountKind)accountType
            : OutlookAccountKind.Unknown;
        KindName = Label(accountType);
        DeliveryStoreId = deliveryStoreId;
        DeliveryStoreName = deliveryStoreName;
        DeliveryFilePath = deliveryFilePath;
        Evidence = evidence;
    }

    private static string Label(int accountType)
    {
        switch (accountType)
        {
            case 0:
                return "Exchange";
            case 1:
                return "IMAP";
            case 2:
                return "POP3";
            case 3:
                return "HTTP";
            case 4:
                return "EAS";
            case 5:
                return "Other";
            default:
                return accountType < 0
                    ? "Unknown"
                    : "Type " + accountType.ToString(CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>One data file or mailbox in the current profile, with the accounts that depend on it.</summary>
public sealed class OutlookStoreInfo
{
    public string DisplayName { get; }
    public string StoreId { get; }
    public string FilePath { get; }
    public OutlookDataFileFormat DataFileFormat { get; }

    /// <summary>Backing file shape as the Outlook data file dialog names it: PST, OST, None, or Other.</summary>
    public string DataFileFormatName { get; }

    /// <summary>Raw OlExchangeStoreType, or -1 when the provider did not report one.</summary>
    public int ExchangeStoreType { get; }
    public string ExchangeStoreName { get; }

    /// <summary>File length in bytes, or null when the file is absent or unreadable. Outlook holding it open does not prevent the read.</summary>
    public long? SizeBytes { get; }

    /// <summary>Display names of the accounts that deliver new mail here. Correlated by StoreID, because display names repeat.</summary>
    public IReadOnlyList<string> DeliveredFrom { get; }

    /// <summary>olNotExchange (3) is the only value that proves a store is not served by Exchange. An unreported type counts as Exchange-backed.</summary>
    public bool IsExchangeBacked => ExchangeStoreType != 3;

    /// <summary>Changes here can reach a server and other clients.</summary>
    public bool IsSynchronized { get; }

    /// <summary>Items here can be header-only stubs, so a transfer must verify each body first.</summary>
    public bool MayContainHeaderOnlyItems { get; }

    /// <summary>Why the store carries the classification above, including any provider error behind it.</summary>
    public string Evidence { get; }

    internal OutlookStoreInfo(
        string displayName,
        string storeId,
        string filePath,
        OutlookDataFileFormat dataFileFormat,
        int exchangeStoreType,
        string exchangeStoreName,
        long? sizeBytes,
        IReadOnlyList<string> deliveredFrom,
        bool isSynchronized,
        bool mayContainHeaderOnlyItems,
        string evidence
    )
    {
        DisplayName = displayName;
        StoreId = storeId;
        FilePath = filePath;
        DataFileFormat = dataFileFormat;
        DataFileFormatName = Label(dataFileFormat);
        ExchangeStoreType = exchangeStoreType;
        ExchangeStoreName = exchangeStoreName;
        SizeBytes = sizeBytes;
        DeliveredFrom = deliveredFrom;
        IsSynchronized = isSynchronized;
        MayContainHeaderOnlyItems = mayContainHeaderOnlyItems;
        Evidence = evidence;
    }

    private static string Label(OutlookDataFileFormat format)
    {
        switch (format)
        {
            case OutlookDataFileFormat.Pst:
                return "PST";
            case OutlookDataFileFormat.Ost:
                return "OST";
            case OutlookDataFileFormat.None:
                return "None";
            default:
                return "Other";
        }
    }
}

/// <summary>Accounts and stores of one profile, read together so each store can state what depends on it.</summary>
public sealed class OutlookProfileInventory
{
    public IReadOnlyList<OutlookAccountInfo> Accounts { get; }
    public IReadOnlyList<OutlookStoreInfo> Stores { get; }

    internal OutlookProfileInventory(
        IReadOnlyList<OutlookAccountInfo> accounts,
        IReadOnlyList<OutlookStoreInfo> stores
    )
    {
        Accounts = accounts;
        Stores = stores;
    }
}

public sealed partial class OutlookManager
{
    private const int NotExchange = 3;

    private static readonly Dictionary<int, string> ExchangeStoreTypeNames = new Dictionary<
        int,
        string
    >
    {
        { 0, "PrimaryMailbox" },
        { 1, "Mailbox" },
        { 2, "PublicFolders" },
        { 3, "NotExchange" },
        { 4, "AdditionalMailbox" },
    };

    /// <summary>
    /// Reads every account and data file of the connected profile without opening a folder or touching an item. Standard
    /// folder discovery is unavailable for Exchange and OST stores below Outlook 2010; this stays readable where it is not.
    /// </summary>
    /// <returns>Plain records. No COM reference is retained and the namespace stays borrowed.</returns>
    public OutlookProfileInventory GetProfileInventory(
        object session,
        CancellationToken cancellationToken = default
    )
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        var stores = ReadStores(session, cancellationToken);
        var accounts = ReadAccounts(session, cancellationToken);

        return new OutlookProfileInventory(accounts, Classify(stores, accounts));
    }

    private sealed class StoreFacts
    {
        internal string DisplayName = "";
        internal string StoreId = "";
        internal string FilePath = "";
        internal int ExchangeStoreType = -1;
        internal string Evidence = "";
    }

    private static List<StoreFacts> ReadStores(object session, CancellationToken cancellationToken)
    {
        var result = new List<StoreFacts>();
        var stores = OutlookDispatch.Require(OutlookDispatch.Get(session, "Stores"));

        try
        {
            var count = OutlookDispatch.Count(stores);

            for (var index = 1; index <= count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var store = OutlookDispatch.Require(OutlookDispatch.Call(stores, "Item", index));

                try
                {
                    result.Add(ReadStore(store));
                }
                finally
                {
                    OutlookDispatch.Release(store);
                }
            }
        }
        finally
        {
            OutlookDispatch.Release(stores);
        }

        return result;
    }

    private static StoreFacts ReadStore(object store)
    {
        var facts = new StoreFacts
        {
            DisplayName = OutlookDispatch.Text(store, "DisplayName"),
            StoreId = OutlookDispatch.Text(store, "StoreID"),
        };

        // A provider may expose neither a file path nor an Exchange classification.
        // Record what failed; never substitute a value that would read as local.
        try
        {
            facts.FilePath = OutlookDispatch.Text(store, "FilePath");
        }
        catch (Exception error)
        {
            facts.Evidence = Append(facts.Evidence, "FilePath is unavailable: " + error.Message);
        }

        try
        {
            var value = OutlookDispatch.Get(store, "ExchangeStoreType");
            facts.ExchangeStoreType =
                value == null ? -1 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception error)
        {
            facts.Evidence = Append(
                facts.Evidence,
                "ExchangeStoreType is unavailable: " + error.Message
            );
        }

        return facts;
    }

    private static List<OutlookAccountInfo> ReadAccounts(
        object session,
        CancellationToken cancellationToken
    )
    {
        var result = new List<OutlookAccountInfo>();
        var accounts = OutlookDispatch.Require(OutlookDispatch.Get(session, "Accounts"));

        try
        {
            var count = OutlookDispatch.Count(accounts);

            for (var index = 1; index <= count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = OutlookDispatch.Require(
                    OutlookDispatch.Call(accounts, "Item", index)
                );

                try
                {
                    result.Add(ReadAccount(account));
                }
                finally
                {
                    OutlookDispatch.Release(account);
                }
            }
        }
        finally
        {
            OutlookDispatch.Release(accounts);
        }

        return result;
    }

    private static OutlookAccountInfo ReadAccount(object account)
    {
        var displayName = OutlookDispatch.Text(account, "DisplayName");
        var evidence = "";
        var accountType = -1;

        try
        {
            var value = OutlookDispatch.Get(account, "AccountType");
            accountType = value == null ? -1 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception error)
        {
            evidence = Append(evidence, "AccountType is unavailable: " + error.Message);
        }

        // SmtpAddress is absent on some Outlook 2007 account providers; UserName is not.
        var smtpAddress = "";

        try
        {
            smtpAddress = OutlookDispatch.Text(account, "SmtpAddress");
        }
        catch (Exception error)
        {
            evidence = Append(evidence, "SmtpAddress is unavailable: " + error.Message);
        }

        var userName = "";

        try
        {
            userName = OutlookDispatch.Text(account, "UserName");
        }
        catch (Exception error)
        {
            evidence = Append(evidence, "UserName is unavailable: " + error.Message);
        }

        var deliveryStoreId = "";
        var deliveryStoreName = "";
        var deliveryFilePath = "";
        object? deliveryStore = null;

        try
        {
            deliveryStore = OutlookDispatch.Get(account, "DeliveryStore");

            if (deliveryStore == null)
                evidence = Append(evidence, "The account reports no delivery store.");
            else
            {
                deliveryStoreId = OutlookDispatch.Text(deliveryStore, "StoreID");
                deliveryStoreName = OutlookDispatch.Text(deliveryStore, "DisplayName");

                try
                {
                    deliveryFilePath = OutlookDispatch.Text(deliveryStore, "FilePath");
                }
                catch (Exception error)
                {
                    evidence = Append(
                        evidence,
                        "Delivery store FilePath is unavailable: " + error.Message
                    );
                }
            }
        }
        catch (Exception error)
        {
            evidence = Append(evidence, "DeliveryStore is unavailable: " + error.Message);
        }
        finally
        {
            OutlookDispatch.Release(deliveryStore);
        }

        return new OutlookAccountInfo(
            displayName,
            smtpAddress,
            userName,
            accountType,
            deliveryStoreId,
            deliveryStoreName,
            deliveryFilePath,
            evidence
        );
    }

    private static List<OutlookStoreInfo> Classify(
        List<StoreFacts> stores,
        List<OutlookAccountInfo> accounts
    )
    {
        var result = new List<OutlookStoreInfo>(stores.Count);

        foreach (var facts in stores)
        {
            // StoreID is the identity. Two data files can share a display name.
            var delivering = string.IsNullOrEmpty(facts.StoreId)
                ? new List<OutlookAccountInfo>()
                : accounts
                    .Where(account => OutlookDispatch.Equal(account.DeliveryStoreId, facts.StoreId))
                    .ToList();

            var format = FormatOf(facts.FilePath);
            var exchangeBacked = facts.ExchangeStoreType != NotExchange;
            var synchronizing = delivering.FirstOrDefault(account => account.IsServerSynchronized);
            var deferring = delivering.FirstOrDefault(account => account.MayDeferItemBodies);
            var evidence = facts.Evidence;

            if (facts.ExchangeStoreType < 0)
                evidence = Append(
                    evidence,
                    "No Exchange store type was reported; the store is treated as server-backed."
                );
            else if (exchangeBacked)
                evidence = Append(
                    evidence,
                    "Exchange-backed store (" + NameOf(facts.ExchangeStoreType) + ")."
                );

            if (format == OutlookDataFileFormat.Ost)
                evidence = Append(
                    evidence,
                    "Offline data file: its contents are a cache of server state."
                );

            if (synchronizing != null)
                evidence = Append(
                    evidence,
                    "Delivery target of the "
                        + synchronizing.KindName
                        + " account '"
                        + synchronizing.DisplayName
                        + "'."
                );

            if (!exchangeBacked && format != OutlookDataFileFormat.Ost && synchronizing == null)
                evidence = Append(
                    evidence,
                    "Local data file with no account that synchronizes to a server."
                );

            result.Add(
                new OutlookStoreInfo(
                    facts.DisplayName,
                    facts.StoreId,
                    facts.FilePath,
                    format,
                    facts.ExchangeStoreType,
                    NameOf(facts.ExchangeStoreType),
                    SizeOf(facts.FilePath),
                    delivering.Select(account => account.DisplayName).ToList(),
                    format == OutlookDataFileFormat.Ost || exchangeBacked || synchronizing != null,
                    format == OutlookDataFileFormat.Ost || deferring != null,
                    evidence
                )
            );
        }

        return result;
    }

    private static string NameOf(int exchangeStoreType)
    {
        string name;

        if (ExchangeStoreTypeNames.TryGetValue(exchangeStoreType, out name))
            return name;

        return exchangeStoreType < 0
            ? "Unknown"
            : "Type " + exchangeStoreType.ToString(CultureInfo.InvariantCulture);
    }

    private static OutlookDataFileFormat FormatOf(string path)
    {
        if (string.IsNullOrEmpty(path))
            return OutlookDataFileFormat.None;

        var extension = System.IO.Path.GetExtension(path);

        if (OutlookDispatch.Equal(extension, ".pst"))
            return OutlookDataFileFormat.Pst;

        return OutlookDispatch.Equal(extension, ".ost")
            ? OutlookDataFileFormat.Ost
            : OutlookDataFileFormat.Other;
    }

    private static long? SizeOf(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // Size is a convenience, never a precondition. Outlook holding the file
        // open does not block a length read; an unreadable path reports none.
        try
        {
            var file = new FileInfo(FileSystemPath.Parse(path).ExpandLongName().Value);

            return file.Exists ? file.Length : (long?)null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Append(string evidence, string addition) =>
        string.IsNullOrEmpty(evidence) ? addition : evidence + " " + addition;
}
