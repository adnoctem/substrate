using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Xml;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.Office;

// Internal substitution boundary for offline lifecycle tests. Production always constructs this native implementation.
internal class OfficeDeploymentRuntime
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    internal virtual OfficeInventory Inventory(CancellationToken cancellation) => new OfficeInventoryManager().Read(cancellation);
    internal virtual OfficeMediaAssessment? Media(OfficeDeploymentPlanDocument plan, CancellationToken cancellation)
        => string.IsNullOrEmpty(plan.Request.SourcePath) || plan.Request.Configuration == null ? null
            : new OfficeMediaManager().Inspect(FileSystemPath.Parse(plan.Request.SourcePath!), plan.Request.Configuration, cancellation,
                plan.Request.Configuration.Version == null ? null : plan.Data.Object("Configuration").Text("Version"));
    internal virtual OfficeActivationState Activation(OfficeProduct product, CancellationToken cancellation)
        => new OfficeActivationManager().GetStatus(product, Timeout, cancellation);
    internal virtual void RequireReady(OfficeDeploymentOptions options, CancellationToken cancellation)
    {
        new OfficeDeploymentEnvironment().RequireReady(Timeout, cancellation);
        if (!new OdtTool().Check(options.OdtPath, cancellationToken: cancellation).Valid)
            throw new OfficeException(OfficeFailureReason.UntrustedTool, "ODT verification failed.");
        if (options.LogRoot.Value.StartsWith(@"\\", StringComparison.Ordinal))
            throw new OfficeException(OfficeFailureReason.UnsafePath, "Recovery records must be stored locally.");
        new OfficePathGuard().ValidatePath(options.LogRoot.Value, cancellation);
        if (!options.ForceCloseApplications && new OfficeApplicationManager().Read(cancellation).Count != 0)
            throw new OfficeException(OfficeFailureReason.ApplicationsRunning, "Close Office applications in all sessions or authorize ForceCloseApps.");
    }
    internal virtual IDisposable AcquireLock(CancellationToken cancellation) => OfficeDeploymentLock.Acquire(cancellation);
    internal virtual void CreateWork(FileSystemPath logRoot, FileSystemPath work, CancellationToken cancellation)
    {
        var guard = new OfficePathGuard();
        guard.EnsureProtectedDirectory(logRoot.Value, cancellation);
        guard.CreateProtectedDirectory(work.Value, cancellation);
    }
    internal virtual void WriteJournal(FileSystemPath path, string json) => new OfficeJournalStore().Write(path, json, true);
    internal virtual void WriteLog(FileSystemPath path, string json)
    {
        new OfficePathGuard().RequireProtected(Path.GetDirectoryName(path.Value)!, "RecoveryDirectory");
        var result = OfficeDocument.Parse(json);
        var entry = OfficeDocument.Create("Timestamp", DateTime.Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture), "Script", result.Text("Action"));
        foreach (var name in result.Names)
            entry[name] = result[name];
        using (var input = new MemoryStream(new UTF8Encoding(false).GetBytes(entry.ToJson() + Environment.NewLine)))
            new FileManager().WriteAtomically(path, input);
    }
    internal virtual void Cleanup(FileSystemPath work, FileSystemPath root) => new OfficePathGuard().RemoveWorkDirectory(work.Value, root.Value);
    internal virtual void Stage(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, FileSystemPath work, CancellationToken cancellation)
    {
        var guard = new OfficePathGuard();
        Copy(options.OdtPath, work.Combine("setup.exe"), cancellation);
        if (!new OdtTool().Check(work.Combine("setup.exe"), cancellationToken: cancellation).Valid)
            throw new OfficeException(OfficeFailureReason.UntrustedTool, "Staged ODT verification failed.");
        if (string.IsNullOrEmpty(plan.Request.SourcePath))
            return;
        var media = Media(plan, cancellation)!;
        RequireMatchingMedia(plan, media, OfficeFailureReason.StaleMedia);
        var bytes = media.Files.Aggregate(0L, (sum, file) => checked(sum + file.Length));
        if (new DriveInfo(Path.GetPathRoot(work.Value)!).AvailableFreeSpace < checked(2 * bytes + 4L * 1024 * 1024 * 1024))
            throw new OfficeException(OfficeFailureReason.InsufficientSpace, "Insufficient capacity for staged media and installation.");
        var destination = work.Combine("Media");
        guard.CreateProtectedDirectory(destination.Value, cancellation);
        foreach (var file in media.Files)
        {
            cancellation.ThrowIfCancellationRequested();
            var target = destination.Combine(file.Path.Replace('/', Path.DirectorySeparatorChar));
            var relativeDirectory = Path.GetDirectoryName(file.Path.Replace('/', Path.DirectorySeparatorChar))!;
            var parent = destination;
            foreach (var segment in relativeDirectory.Split(Path.DirectorySeparatorChar))
            { parent = parent.Combine(segment); guard.EnsureProtectedDirectory(parent.Value, cancellation); }
            var source = FileSystemPath.Parse(plan.Request.SourcePath!).Combine(file.Path.Replace('/', Path.DirectorySeparatorChar));
            guard.RequireProtected(source.Value, cancellationToken: cancellation);
            Copy(source, target, cancellation);
        }
        using (var input = new MemoryStream(new UTF8Encoding(false).GetBytes(media.ManifestJson!)))
            new FileManager().WriteAtomically(destination.Combine(OfficeMediaManifest.FileName), input, cancellationToken: cancellation);
        var staged = new OfficeMediaManager().Inspect(destination, plan.Request.Configuration, cancellation, plan.Data.Object("Configuration").Text("Version"));
        RequireMatchingMedia(plan, staged, OfficeFailureReason.MediaIntegrityFailed);
    }
    internal virtual void CloseApplications(bool authorized, Action closed, CancellationToken cancellation)
    {
        var applications = new OfficeApplicationManager();
        var observed = applications.Read(cancellation);
        if (observed.Count != 0 && !authorized)
            throw new OfficeException(OfficeFailureReason.ApplicationsRunning, "Close Office applications in all sessions or explicitly authorize ForceCloseApps.");
        foreach (var process in observed)
        {
            // Once termination is requested, uncertainty must not be reported as unchanged.
            closed();
            applications.ForceClose(process, cancellation);
        }
        if (applications.Read(cancellation).Count != 0)
            throw new OfficeException(OfficeFailureReason.ApplicationsRunning, "Office applications are still running.");
    }
    internal virtual int RunPhase(OfficeDeploymentPlanDocument plan, OfficeDeploymentAction action, FileSystemPath work, SecureString? key, CancellationToken cancellation)
    {
        var request = plan.Request;
        var builder = new OdtConfigurationBuilder();
        var media = work.Combine("Media").Value;
        XmlDocument xml;
        switch (action)
        {
            case OfficeDeploymentAction.Install:
                xml = builder.Install(request.Configuration!, media);
                break;
            case OfficeDeploymentAction.Remove:
                xml = builder.RemoveProducts(request.RemoveProductIds);
                break;
            case OfficeDeploymentAction.Migrate:
                xml = builder.Migrate(request.Configuration!, media, request.RemoveMsi);
                break;
            case OfficeDeploymentAction.Update:
                xml = builder.Update(request.Configuration!, media);
                break;
            case OfficeDeploymentAction.AddLanguage:
                xml = builder.AddLanguage(request.Configuration!, media);
                break;
            case OfficeDeploymentAction.RemoveLanguage:
                xml = builder.RemoveLanguage(request.Configuration!.Product, request.Languages);
                break;
            case OfficeDeploymentAction.SetApplicationSelection:
                xml = builder.SetApplicationSelection(request.Configuration!, media);
                break;
            case OfficeDeploymentAction.SetUpdateConfiguration:
                xml = builder.SetUpdateConfiguration(request.UpdateConfiguration!);
                break;
            case OfficeDeploymentAction.SetApplicationPreference:
                xml = builder.SetApplicationPreferences(request.Preferences);
                break;
            default:
                throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Unsupported deployment operation.");
        }
        // The version's spelling belongs to the pinned media contract, not Version.ToString().
        if (plan.Data["Configuration"] is OfficeDocument config && xml.SelectSingleNode("/Configuration/Add") is XmlElement add && request.Configuration!.Version != null)
            add.SetAttribute("Version", config.Text("Version")!);
        return new OdtTool().InvokeConfiguration(work.Combine("setup.exe"), xml, work,
            action == OfficeDeploymentAction.SetApplicationPreference ? OdtMode.Customize : OdtMode.Configure, key, cancellation).ExitCode;
    }
    internal virtual OfficeRecoveryRecord ReadRecovery(string runId, FileSystemPath logRoot, CancellationToken cancellation) => new OfficeRecoveryManager().Read(runId, logRoot, cancellation);
    internal virtual bool DeploymentBusy(CancellationToken cancellation) => new OfficeDeploymentEnvironment().ReadActivity(Timeout, cancellation).Busy;
    internal virtual bool RebootPending(CancellationToken cancellation)
    {
        var status = new RebootManager().GetStatus(Timeout, cancellation);
        if (!status.IsComplete)
            throw new OfficeException(OfficeFailureReason.PreflightFailed, "Reboot state could not be fully inspected.");
        return status.IsPending;
    }
    internal static void RequireMatchingMedia(OfficeDeploymentPlanDocument plan, OfficeMediaAssessment? media, OfficeFailureReason reason)
    {
        var matches = media?.Valid == true ? OfficeDocument.MatchesFingerprint(OfficeDocument.Parse(media.ManifestJson!), plan.MediaFingerprint) : plan.MediaFingerprint == null;
        if (media != null && !media.Valid || !matches)
        {
            var error = new OfficeException(reason, "Prepared media changed or could not be verified; create a new plan.");
            if (media?.Error?.Data["OfficeDiagnostic"] != null)
                error.Data["OfficeDiagnostic"] = media.Error.Data["OfficeDiagnostic"];
            throw error;
        }
    }
    private static void Copy(FileSystemPath source, FileSystemPath target, CancellationToken cancellation)
    {
        using (var input = new FileStream(source.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            new FileManager().WriteAtomically(target, input, cancellationToken: cancellation);
    }
}
