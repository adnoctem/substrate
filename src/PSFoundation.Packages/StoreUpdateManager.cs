using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;
using PSFoundation.Packages.Runtime;

namespace PSFoundation.Packages;

/// <summary>An available Store update identified by package family.</summary>
public sealed class StoreUpdateInfo
{
    public string PackageFamilyName { get; }
    public string ProductId { get; }
    public string InstallType { get; }
    public int ErrorCode { get; }
    internal StoreUpdateInfo(RuntimeStoreUpdate item) { PackageFamilyName = item.PackageFamilyName; ProductId = item.ProductId; InstallType = item.InstallType; ErrorCode = item.ErrorCode; }
}
/// <summary>The result of a Store update request, including native status evidence.</summary>
public enum StoreUpdateOutcome { Completed, NoUpdate }
/// <summary>Microsoft Store's native install API. Windows capability/access errors are surfaced; this library does not bypass Store policy.</summary>
public sealed class StoreUpdateManager
{
    private readonly PackageRuntimeClient runtime;
    public StoreUpdateManager(FileSystemPath? runtimeHost = null) => runtime = new PackageRuntimeClient(runtimeHost);
    /// <summary>Queries Store updates through the capability-restricted Windows Store API.</summary>
    public IReadOnlyList<StoreUpdateInfo> FindUpdates(CancellationToken cancellationToken = default)
        => Array.AsReadOnly(runtime.Run(new RuntimeRequest { Operation = "StoreSearch" }, cancellationToken).Updates.Select(update => new StoreUpdateInfo(update)).ToArray());
    /// <summary>Queries Store update availability without assuming that the caller possesses the required Windows capability.</summary>
    public async Task<IReadOnlyList<StoreUpdateInfo>> FindUpdatesAsync(CancellationToken cancellationToken = default)
        => Array.AsReadOnly((await runtime.RunAsync(new RuntimeRequest { Operation = "StoreSearch" }, cancellationToken).ConfigureAwait(false)).Updates.Select(update => new StoreUpdateInfo(update)).ToArray());
    /// <summary>Requests a Store update for an exact package family and returns its native outcome.</summary>
    public StoreUpdateOutcome Install(string packageFamilyName, CancellationToken cancellationToken = default)
    { AppxPackageInfo.RequireIdentity(packageFamilyName); return runtime.Run(new RuntimeRequest { Operation = "StoreUpdate", Target = packageFamilyName }, cancellationToken).NoUpdate ? StoreUpdateOutcome.NoUpdate : StoreUpdateOutcome.Completed; }
    /// <summary>Requests and observes a selected Store update through the isolated runtime host.</summary>
    public async Task<StoreUpdateOutcome> InstallAsync(string packageFamilyName, CancellationToken cancellationToken = default)
    { AppxPackageInfo.RequireIdentity(packageFamilyName); return (await runtime.RunAsync(new RuntimeRequest { Operation = "StoreUpdate", Target = packageFamilyName }, cancellationToken).ConfigureAwait(false)).NoUpdate ? StoreUpdateOutcome.NoUpdate : StoreUpdateOutcome.Completed; }
}
