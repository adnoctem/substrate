using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Interop;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Windows;

/// <summary>A detached update identity, revision, classification, and installation/download observation.</summary>
public sealed class WindowsUpdateInfo
{
    public Guid UpdateId { get; }
    public int RevisionNumber { get; }
    public string Title { get; }
    public IReadOnlyList<string> KBArticleIds { get; }
    public IReadOnlyList<string> Categories { get; }
    public bool IsDownloaded { get; }
    public bool IsInstalled { get; }

    internal WindowsUpdateInfo(AutomationObject update)
    {
        using var identity = update.Child("Identity");

        UpdateId = Guid.Parse(
            Convert.ToString(identity.Get("UpdateID"), CultureInfo.InvariantCulture)!
        );
        RevisionNumber = Convert.ToInt32(
            identity.Get("RevisionNumber"),
            CultureInfo.InvariantCulture
        );
        Title = Convert.ToString(update.Get("Title"), CultureInfo.InvariantCulture) ?? "";
        IsDownloaded = Convert.ToBoolean(update.Get("IsDownloaded"), CultureInfo.InvariantCulture);
        IsInstalled = Convert.ToBoolean(update.Get("IsInstalled"), CultureInfo.InvariantCulture);

        using var articles = update.Child("KBArticleIDs");

        var ids = new List<string>();

        for (
            var i = 0;
            i < Convert.ToInt32(articles.Get("Count"), CultureInfo.InvariantCulture);
            i++
        )
            ids.Add(Convert.ToString(articles.Get("Item", i), CultureInfo.InvariantCulture) ?? "");

        KBArticleIds = ids.AsReadOnly();

        using var categories = update.Child("Categories");

        var names = new List<string>();

        for (
            var i = 0;
            i < Convert.ToInt32(categories.Get("Count"), CultureInfo.InvariantCulture);
            i++
        )
        {
            using var category = categories.Child("Item", i);

            names.Add(Convert.ToString(category.Get("Name"), CultureInfo.InvariantCulture) ?? "");
        }

        Categories = names.AsReadOnly();
    }
}

/// <summary>Per-update native operation status, HRESULT, and reboot requirement.</summary>
public sealed class WindowsUpdateResult
{
    public WindowsUpdateInfo Update { get; }
    public int HResult { get; }
    public int ResultCode { get; }
    public bool RebootRequired { get; }

    internal WindowsUpdateResult(WindowsUpdateInfo update, AutomationObject result)
    {
        Update = update;
        HResult = Convert.ToInt32(result.Get("HResult"));
        ResultCode = Convert.ToInt32(result.Get("ResultCode"));
        RebootRequired = Convert.ToBoolean(result.Get("RebootRequired"));
    }
}

/// <summary>A detached historical update operation with its time and native outcome.</summary>
public sealed class WindowsUpdateHistoryEntry
{
    public Guid UpdateId { get; }
    public string Title { get; }
    public DateTime Date { get; }
    public int Operation { get; }
    public int ResultCode { get; }
    public int HResult { get; }

    internal WindowsUpdateHistoryEntry(AutomationObject entry)
    {
        using var identity = entry.Child("UpdateIdentity");

        UpdateId = Guid.Parse(Convert.ToString(identity.Get("UpdateID"))!);
        Title = Convert.ToString(entry.Get("Title")) ?? "";
        Date = Convert.ToDateTime(entry.Get("Date"));
        Operation = Convert.ToInt32(entry.Get("Operation"));
        ResultCode = Convert.ToInt32(entry.Get("ResultCode"));
        HResult = Convert.ToInt32(entry.Get("HResult"));
    }
}

/// <summary>Local Windows Update Agent operations. Never changes update sources, prompts, elevates or reboots.</summary>
/// <remarks>Cancellation requests WUA abort and waits for the native job to settle before releasing its objects. An in-flight installation may have changed the machine.</remarks>
public sealed class WindowsUpdateManager
{
    /// <summary>Scans the configured Windows Update source for visible, uninstalled updates.</summary>
    public IReadOnlyList<WindowsUpdateInfo> FindAvailable(
        CancellationToken cancellationToken = default
    ) => Find(false, cancellationToken);

    /// <summary>Performs an available-update scan on a worker thread; cancellation requests native abort.</summary>
    public Task<IReadOnlyList<WindowsUpdateInfo>> FindAvailableAsync(
        CancellationToken cancellationToken = default
    ) => Task.Run(() => FindAvailable(cancellationToken), cancellationToken);

    /// <summary>Queries installed updates through Windows Update Agent.</summary>
    public IReadOnlyList<WindowsUpdateInfo> FindInstalled(
        CancellationToken cancellationToken = default
    ) => Find(true, cancellationToken);

    private static IReadOnlyList<WindowsUpdateInfo> Find(bool installed, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        using var session = Session();

        using var searcher = session.CallObject("CreateUpdateSearcher");

        using var result = Search(
            searcher,
            installed ? "IsInstalled=1" : "IsInstalled=0 and IsHidden=0",
            token
        );

        using var updates = result.Child("Updates");

        var records = new List<WindowsUpdateInfo>();

        for (var i = 0; i < Count(updates); i++)
        {
            token.ThrowIfCancellationRequested();

            using var update = updates.Child("Item", i);

            records.Add(new WindowsUpdateInfo(update));
        }

        return records.AsReadOnly();
    }

    /// <summary>Downloads and installs the exact selected update identities, requiring explicit acceptance of outstanding license agreements.</summary>
    public IReadOnlyList<WindowsUpdateResult> Install(
        IEnumerable<Guid> updateIds,
        bool acceptLicenseAgreements = false,
        CancellationToken cancellationToken = default
    ) => Execute(SnapshotIds(updateIds), false, acceptLicenseAgreements, cancellationToken);

    /// <summary>Downloads and installs selected identities on a worker thread. The result reports native outcomes and reboot requirements.</summary>
    public Task<IReadOnlyList<WindowsUpdateResult>> InstallAsync(
        IEnumerable<Guid> updateIds,
        bool acceptLicenseAgreements = false,
        CancellationToken cancellationToken = default
    )
    {
        var ids = SnapshotIds(updateIds);

        return Task.Run(
            () => Execute(ids, false, acceptLicenseAgreements, cancellationToken),
            cancellationToken
        );
    }

    /// <summary>Requests removal of the selected installed update identities, rejecting updates that Windows cannot uninstall.</summary>
    public IReadOnlyList<WindowsUpdateResult> Uninstall(
        IEnumerable<Guid> updateIds,
        CancellationToken cancellationToken = default
    ) => Execute(SnapshotIds(updateIds), true, false, cancellationToken);

    /// <summary>Removes selected updates on a worker thread; cancellation may follow partially completed native changes.</summary>
    public Task<IReadOnlyList<WindowsUpdateResult>> UninstallAsync(
        IEnumerable<Guid> updateIds,
        CancellationToken cancellationToken = default
    )
    {
        var ids = SnapshotIds(updateIds);

        return Task.Run(() => Execute(ids, true, false, cancellationToken), cancellationToken);
    }

    /// <summary>Sets visibility for selected update identities. Earlier changes remain if a later change fails.</summary>
    public void SetHidden(
        IEnumerable<Guid> updateIds,
        bool hidden,
        CancellationToken cancellationToken = default
    )
    {
        var ids = SnapshotIds(updateIds);
        cancellationToken.ThrowIfCancellationRequested();

        using var session = Session();

        using var searcher = session.CallObject("CreateUpdateSearcher");

        using var updates = Select(searcher, ids, null, cancellationToken);

        for (var i = 0; i < Count(updates); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var update = updates.Child("Item", i);

            update.Set("IsHidden", hidden);
        }
    }

    /// <summary>Returns newest-first Windows Update history, optionally limiting the number of entries.</summary>
    public IReadOnlyList<WindowsUpdateHistoryEntry> GetHistory(
        int? last = null,
        CancellationToken cancellationToken = default
    )
    {
        if (last < 0)
            throw new ArgumentOutOfRangeException(nameof(last));

        cancellationToken.ThrowIfCancellationRequested();

        using var session = Session();

        using var searcher = session.CallObject("CreateUpdateSearcher");

        var count = Convert.ToInt32(searcher.Call("GetTotalHistoryCount"));

        if (last.HasValue)
            count = Math.Min(last.Value, count);

        var records = new List<WindowsUpdateHistoryEntry>();

        if (count == 0)
            return records.AsReadOnly();

        using var entries = searcher.CallObject("QueryHistory", 0, count);

        for (var i = 0; i < Count(entries); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var entry = entries.Child("Item", i);

            records.Add(new WindowsUpdateHistoryEntry(entry));
        }

        return records.AsReadOnly();
    }

    /// <summary>Reads the Windows Update reboot flag without scanning, installing, or restarting.</summary>
    public bool IsRebootRequired()
    {
        using var info = AutomationObject.Create("Microsoft.Update.SystemInfo");

        return Convert.ToBoolean(info.Get("RebootRequired"));
    }

    /// <summary>Observes update service selection, automatic-update settings, and configured target group without changing policy.</summary>
    public IReadOnlyDictionary<string, object?> GetConfiguration()
    {
        using var session = Session();

        using var searcher = session.CallObject("CreateUpdateSearcher");

        using var automatic = AutomationObject.Create("Microsoft.Update.AutoUpdate");

        using var settings = automatic.Child("Settings");

        var registry = new RegistryManager();

        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ServerSelection"] = searcher.Get("ServerSelection"),
                ["ServiceID"] = searcher.Get("ServiceID"),
                ["TargetGroup"] = registry
                    .GetValue(
                        RegistryPath.Parse(
                            @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"
                        ),
                        "TargetGroup"
                    )
                    ?.Data,
                ["NotificationLevel"] = settings.Get("NotificationLevel"),
                ["ScheduledInstallationDay"] = settings.Get("ScheduledInstallationDay"),
                ["ScheduledInstallationTime"] = settings.Get("ScheduledInstallationTime"),
                ["ReadOnly"] = settings.Get("ReadOnly"),
            }
        );
    }

    private static IReadOnlyList<WindowsUpdateResult> Execute(
        Guid[] ids,
        bool uninstall,
        bool accept,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();

        using var session = Session();

        using var searcher = session.CallObject("CreateUpdateSearcher");

        using var updates = Select(searcher, ids, uninstall, token);

        var info = new List<WindowsUpdateInfo>();

        for (var i = 0; i < Count(updates); i++)
        {
            token.ThrowIfCancellationRequested();

            using var update = updates.Child("Item", i);

            info.Add(new WindowsUpdateInfo(update));

            if (uninstall && !Convert.ToBoolean(update.Get("IsUninstallable")))
                throw new InvalidOperationException(
                    "The selected update cannot be uninstalled: " + info[i].UpdateId
                );

            if (!uninstall && !Convert.ToBoolean(update.Get("EulaAccepted")))
            {
                if (!accept)
                    throw new InvalidOperationException(
                        "The selected update requires explicit license acceptance: "
                            + info[i].UpdateId
                    );

                update.Call("AcceptEula");
            }
        }

        if (!uninstall)
        {
            using var downloader = session.CallObject("CreateUpdateDownloader");

            downloader.Set("Updates", updates.Value);

            using var downloaded = RunJob(downloader, "Download", token);

            for (var i = 0; i < Count(updates); i++)
            {
                using var update = updates.Child("Item", i);

                if (!Convert.ToBoolean(update.Get("IsDownloaded")))
                    throw new InvalidOperationException(
                        "An update did not download successfully; installation was not started."
                    );
            }
        }

        token.ThrowIfCancellationRequested();

        using var installer = session.CallObject("CreateUpdateInstaller");

        installer.Set("Updates", updates.Value);
        installer.Set("AllowSourcePrompts", false);
        installer.Set("ForceQuiet", true);

        using var installed = RunJob(installer, uninstall ? "Uninstall" : "Install", token);

        var records = new List<WindowsUpdateResult>();

        for (var i = 0; i < info.Count; i++)
        {
            using var item = installed.CallObject("GetUpdateResult", i);

            records.Add(new WindowsUpdateResult(info[i], item));
        }

        return records.AsReadOnly();
    }

    private static AutomationObject Select(
        AutomationObject searcher,
        Guid[] ids,
        bool? installed,
        CancellationToken token
    )
    {
        var selected = AutomationObject.Create("Microsoft.Update.UpdateColl");

        try
        {
            foreach (var id in ids)
            {
                var criteria =
                    "UpdateID='"
                    + id.ToString("D")
                    + "'"
                    + (
                        installed.HasValue
                            ? " and IsInstalled=" + (installed.Value ? "1" : "0")
                            : ""
                    );

                using var result = Search(searcher, criteria, token);

                using var updates = result.Child("Updates");

                if (Count(updates) != 1)
                    throw new InvalidOperationException(
                        "Selected update is unavailable or ambiguous: " + id
                    );

                using var update = updates.Child("Item", 0);

                selected.Call("Add", update.Value);
            }

            return selected;
        }
        catch
        {
            selected.Dispose();

            throw;
        }
    }

    private static Guid[] SnapshotIds(IEnumerable<Guid> ids)
    {
        if (ids == null)
            throw new ArgumentNullException(nameof(ids));

        var snapshot = ids.Distinct().ToArray();

        if (snapshot.Length == 0 || snapshot.Contains(Guid.Empty))
            throw new ArgumentException(
                "At least one nonempty update ID is required; unfiltered operations are forbidden.",
                nameof(ids)
            );

        return snapshot;
    }

    private static AutomationObject Session()
    {
        var session = AutomationObject.Create("Microsoft.Update.Session");

        try
        {
            session.Set("ClientApplicationID", "AdNoctem.Substrate");

            return session;
        }
        catch
        {
            session.Dispose();

            throw;
        }
    }

    private static int Count(AutomationObject collection) =>
        Convert.ToInt32(collection.Get("Count"));

    private static AutomationObject Search(
        AutomationObject searcher,
        string criteria,
        CancellationToken token
    )
    {
        var result = RunJob(searcher, "Search", token, criteria);

        try
        {
            var code = Convert.ToInt32(result.Get("ResultCode"));

            if (code != 2)
                throw new InvalidOperationException(
                    "Windows Update search did not complete successfully (result "
                        + code
                        + "). Partial results are not an authoritative inventory."
                );

            return result;
        }
        catch
        {
            result.Dispose();

            throw;
        }
    }

    private static AutomationObject RunJob(
        AutomationObject owner,
        string operation,
        CancellationToken token,
        string? criteria = null
    )
    {
        token.ThrowIfCancellationRequested();
        var callback = new WindowsUpdateCallback();

        using var job =
            operation == "Search"
                ? owner.CallObject("BeginSearch", criteria, callback, null)
                : owner.CallObject("Begin" + operation, callback, callback, null);

        var aborted = false;

        try
        {
            while (!Convert.ToBoolean(job.Get("IsCompleted")))
            {
                if (token.IsCancellationRequested && !aborted)
                {
                    job.Call("RequestAbort");
                    aborted = true;
                }

                Thread.Sleep(100);
            }

            var result = owner.CallObject("End" + operation, job.Value);

            if (token.IsCancellationRequested)
            {
                result.Dispose();
                token.ThrowIfCancellationRequested();
            }

            return result;
        }
        finally
        {
            job.Call("CleanUp");
            GC.KeepAlive(callback);
        }
    }
}

// A deliberately inert IDispatch callback; all result access and cleanup remains on the owning worker.
[
    ComVisible(true),
    Guid("E6017CEB-F9FA-4A9A-9E37-060639A342A4"),
    InterfaceType(ComInterfaceType.InterfaceIsIDispatch)
]
public interface IWindowsUpdateCallback
{
    [DispId(0)]
    void Invoke(
        [MarshalAs(UnmanagedType.Interface)] object job,
        [MarshalAs(UnmanagedType.Interface)] object arguments
    );
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class WindowsUpdateCallback : IWindowsUpdateCallback
{
    public void Invoke(object job, object arguments) { }
}
