using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Packages.Runtime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Store.Preview.InstallControl;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Management.Deployment;

namespace AdNoctem.Substrate.WinRtHost;

internal static class Program
{
    private static int Main(string[] args)
    {
        RuntimeResponse response;
        try
        {
            if (args.Length != 1 || args[0].Length > 30000)
                throw new ArgumentException("Expected one bounded package request.");
            var request = PackageRuntimeProtocol.Deserialize<RuntimeRequest>(Convert.FromBase64String(args[0]));
            using var cancellation = EventWaitHandle.OpenExisting(request.CancellationEvent);
            using var source = new CancellationTokenSource();
            using var finished = new ManualResetEvent(false);
            var watcher = Task.Run(() => { if (WaitHandle.WaitAny(new WaitHandle[] { finished, cancellation }) == 1) source.Cancel(); });
            try
            { response = Execute(request, source.Token).GetAwaiter().GetResult(); }
            finally { finished.Set(); watcher.GetAwaiter().GetResult(); }
        }
        catch (OperationCanceledException) { response = new RuntimeResponse { Cancelled = true }; }
        catch (Exception error) { response = new RuntimeResponse { ErrorCode = error.HResult, Error = error.Message }; }
        var bytes = PackageRuntimeProtocol.Serialize(response);
        if (bytes.Length > 16 * 1024 * 1024)
            bytes = PackageRuntimeProtocol.Serialize(new RuntimeResponse { Error = "Package inventory exceeds the response size limit." });
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.Write(Encoding.UTF8.GetString(bytes));
        return response.Succeeded ? 0 : 1;
    }
    private static async Task<RuntimeResponse> Execute(RuntimeRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request.Operation == "StoreSearch")
        {
            var manager = new AppInstallManager();
            var items = await manager.SearchForAllUpdatesAsync().AsTask(token).ConfigureAwait(false);
            return new RuntimeResponse
            {
                Succeeded = true,
                Updates = items.Select(item => new RuntimeStoreUpdate
                {
                    PackageFamilyName = item.PackageFamilyName,
                    ProductId = item.ProductId,
                    InstallType = item.InstallType.ToString(),
                    ErrorCode = item.GetCurrentStatus().ErrorCode?.HResult ?? 0
                }).ToArray()
            };
        }
        if (request.Operation == "StoreUpdate")
        {
            RequireIdentity(request.Target);
            var manager = new AppInstallManager();
            var item = await manager.UpdateAppByPackageFamilyNameAsync(request.Target).AsTask(token).ConfigureAwait(false);
            if (item == null)
                return new RuntimeResponse { Succeeded = true, NoUpdate = true };
            while (true)
            {
                if (token.IsCancellationRequested)
                { item.Cancel(); token.ThrowIfCancellationRequested(); }
                var status = item.GetCurrentStatus();
                if (status.ErrorCode != null && status.ErrorCode.HResult != 0)
                    return new RuntimeResponse { ErrorCode = status.ErrorCode.HResult, Error = status.ErrorCode.Message };
                if (status.PercentComplete >= 100)
                    return new RuntimeResponse { Succeeded = true };
                if (status.InstallState == AppInstallState.Canceled)
                    return new RuntimeResponse { Cancelled = true };
                await Task.Delay(500).ConfigureAwait(false);
            }
        }
        var packages = new PackageManager();
        if (request.Operation == "Inventory")
        {
            const PackageTypes types = PackageTypes.Main | PackageTypes.Framework | PackageTypes.Resource | PackageTypes.Bundle | PackageTypes.Optional;
            var entries = request.AllUsers ? packages.FindPackagesWithPackageTypes(types) : packages.FindPackagesForUserWithPackageTypes("", types);
            var result = new List<RuntimePackage>();
            foreach (var package in entries)
            {
                token.ThrowIfCancellationRequested();
                var id = package.Id;
                var version = id.Version;
                result.Add(new RuntimePackage
                {
                    Name = id.Name,
                    FullName = id.FullName,
                    FamilyName = id.FamilyName,
                    Publisher = id.Publisher,
                    Version = new Version(version.Major, version.Minor, version.Build, version.Revision).ToString(),
                    Architecture = id.Architecture.ToString(),
                    InstallLocation = package.InstalledLocation?.Path ?? "",
                    IsFramework = package.IsFramework,
                    IsResource = package.IsResourcePackage,
                    IsBundle = package.IsBundle,
                    NonRemovable = package.SignatureKind == PackageSignatureKind.System
                });
            }
            return new RuntimeResponse { Succeeded = true, Packages = result.ToArray() };
        }
        IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> operation;
        switch (request.Operation)
        {
            case "Install":
                operation = packages.AddPackageAsync(LocalUri(request.Target), request.Dependencies.Select(LocalUri), request.ForceUpdate ? DeploymentOptions.ForceUpdateFromAnyVersion : DeploymentOptions.None);
                break;
            case "Register":
                operation = packages.RegisterPackageAsync(LocalUri(request.Target), null, DeploymentOptions.None);
                break;
            case "Remove":
                RequireIdentity(request.Target);
                operation = packages.RemovePackageAsync(request.Target, request.AllUsers ? RemovalOptions.RemoveForAllUsers : RemovalOptions.None);
                break;
            case "Reset":
                RequireIdentity(request.Target);
                var installed = packages.FindPackageForUser("", request.Target) ?? throw new InvalidOperationException("The package is not registered to the current user.");
                await Windows.Management.Core.ApplicationDataManager.CreateForPackageFamily(installed.Id.FamilyName).ClearAsync().AsTask(token).ConfigureAwait(false);
                return new RuntimeResponse { Succeeded = true };
            default:
                throw new ArgumentException("Unknown package runtime operation.");
        }
        var deployment = await operation.AsTask(token).ConfigureAwait(false);
        var error = deployment.ExtendedErrorCode;
        return error == null || error.HResult == 0 ? new RuntimeResponse { Succeeded = true }
            : new RuntimeResponse { ErrorCode = error.HResult, Error = deployment.ErrorText };
    }
    private static Uri LocalUri(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !File.Exists(path))
            throw new FileNotFoundException("A local package or manifest file is required.", path);
        return new Uri(Path.GetFullPath(path));
    }
    private static void RequireIdentity(string value)
    { if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\0', '*', '?', '/', '\\' }) >= 0) throw new ArgumentException("An exact package identity is required."); }
}
