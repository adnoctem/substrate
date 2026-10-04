using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;
using PSFoundation.Packages.Runtime;

namespace PSFoundation.Packages;

public sealed class StoreUpdateInfo
{
    public string PackageFamilyName { get; }
    public string ProductId { get; }
    public string InstallType { get; }
    public int ErrorCode { get; }
    internal StoreUpdateInfo(RuntimeStoreUpdate item) { PackageFamilyName = item.PackageFamilyName; ProductId = item.ProductId; InstallType = item.InstallType; ErrorCode = item.ErrorCode; }
}
public enum StoreUpdateOutcome { Completed, NoUpdate }
/// <summary>Microsoft Store's native install API. Windows capability/access errors are surfaced; this library does not bypass Store policy.</summary>
public sealed class StoreUpdateManager
{
    private readonly PackageRuntimeClient runtime;
    public StoreUpdateManager(FileSystemPath? runtimeHost = null) => runtime = new PackageRuntimeClient(runtimeHost);
    public IReadOnlyList<StoreUpdateInfo> FindUpdates(CancellationToken cancellationToken = default)
        => Array.AsReadOnly(runtime.Run(new RuntimeRequest { Operation = "StoreSearch" }, cancellationToken).Updates.Select(update => new StoreUpdateInfo(update)).ToArray());
    public async Task<IReadOnlyList<StoreUpdateInfo>> FindUpdatesAsync(CancellationToken cancellationToken = default)
        => Array.AsReadOnly((await runtime.RunAsync(new RuntimeRequest { Operation = "StoreSearch" }, cancellationToken).ConfigureAwait(false)).Updates.Select(update => new StoreUpdateInfo(update)).ToArray());
    public StoreUpdateOutcome Install(string packageFamilyName, CancellationToken cancellationToken = default)
    { AppxPackageInfo.RequireIdentity(packageFamilyName); return runtime.Run(new RuntimeRequest { Operation = "StoreUpdate", Target = packageFamilyName }, cancellationToken).NoUpdate ? StoreUpdateOutcome.NoUpdate : StoreUpdateOutcome.Completed; }
    public async Task<StoreUpdateOutcome> InstallAsync(string packageFamilyName, CancellationToken cancellationToken = default)
    { AppxPackageInfo.RequireIdentity(packageFamilyName); return (await runtime.RunAsync(new RuntimeRequest { Operation = "StoreUpdate", Target = packageFamilyName }, cancellationToken).ConfigureAwait(false)).NoUpdate ? StoreUpdateOutcome.NoUpdate : StoreUpdateOutcome.Completed; }
}
