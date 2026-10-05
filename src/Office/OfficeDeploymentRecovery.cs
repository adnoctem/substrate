using System;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Office;

public sealed partial class OfficeDeploymentManager
{
    /// <summary>Reopens protected recovery evidence and evaluates it without mutation. Only proven pre-launch or completed-removal checkpoints can continue.</summary>
    public OfficeDeploymentResult PreviewRecovery(string runId, OfficeDeploymentAction expectedAction, OfficeDeploymentOptions options,
        SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return Recover(runId, expectedAction, options, key, true, cancellationToken);
    }
    public OfficeDeploymentResult ResumeInstallation(string runId, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return Recover(runId, OfficeDeploymentAction.Install, options, key, false, cancellationToken);
    }
    public OfficeDeploymentResult ResumeMigration(string runId, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return Recover(runId, OfficeDeploymentAction.Migrate, options, key, false, cancellationToken);
    }
    public async Task<OfficeDeploymentResult> PreviewRecoveryAsync(string runId, OfficeDeploymentAction expectedAction, OfficeDeploymentOptions options,
        SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return await OnWorker(() => Recover(runId, expectedAction, options, key, true, cancellationToken), cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Continues only an installation checkpoint whose protected journal and current machine state prove continuation is safe.</summary>
    /// <remarks>Unknown native outcomes and historical pilot journals are not replayed. Recovery is not a rollback mechanism.</remarks>
    public async Task<OfficeDeploymentResult> ResumeInstallationAsync(string runId, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return await OnWorker(() => Recover(runId, OfficeDeploymentAction.Install, options, key, false, cancellationToken), cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Continues a migration only from an accepted checkpoint after revalidating journal, machine, media, and action authority.</summary>
    /// <remarks>A completed-removal checkpoint can authorize the installation phase; an uncertain in-flight native phase cannot.</remarks>
    public async Task<OfficeDeploymentResult> ResumeMigrationAsync(string runId, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return await OnWorker(() => Recover(runId, OfficeDeploymentAction.Migrate, options, key, false, cancellationToken), cancellationToken).ConfigureAwait(false);
    }
    private OfficeDeploymentResult Recover(string runId, OfficeDeploymentAction action, OfficeDeploymentOptions options, SecureString? key, bool preview, CancellationToken cancellation)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (action != OfficeDeploymentAction.Install && action != OfficeDeploymentAction.Migrate)
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Only installation and migration support continuation.");
        var record = runtime.ReadRecovery(runId, options.LogRoot, cancellation);
        if (record.SchemaVersion == 2)
            throw new OfficeException(OfficeFailureReason.UnsupportedPilotRecovery, "Pilot journals are evidence only. Review the result or restore the VM; automatic replay is not supported.");
        if (record.Action != action)
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "This recovery operation cannot resume the recorded action.");
        var plan = record.Plan!;
        var current = runtime.Inventory(cancellation);
        var result = new OfficeDeploymentResult(plan)
        {
            RecoveryObservation = true,
            ExecutionMetadata = options.ExecutionMetadata,
            RecoveryPath = options.LogRoot.Combine(runId + ".json").Value,
            After = current,
            Verification = OfficeDeploymentVerification.AssessTarget(plan, current)
        };
        // Preserve priority: busy/reboot observations block before any media inspection or continuation.
        if (runtime.DeploymentBusy(cancellation))
        { result.Status = OfficeDeploymentStatus.Blocked; result.ReasonCode = "DeploymentBusy"; result.WrapperExitCode = 1; return result; }
        result.RebootRequired = runtime.RebootPending(cancellation);
        if (result.RebootRequired)
        { result.Status = OfficeDeploymentStatus.Blocked; result.ReasonCode = "RebootRequired"; result.WrapperExitCode = 3010; return result; }
        var media = runtime.Media(plan, cancellation);
        if (media == null || !media.Valid)
            throw new OfficeException(OfficeFailureReason.StaleMedia, "Recovery media no longer matches the recorded deployment.");
        OfficeDeploymentRuntime.RequireMatchingMedia(plan, media, OfficeFailureReason.StaleMedia);
        var before = plan.Data.Object("Before");
        var checkpoint = new OfficeRecoveryCheckpoint(record.SchemaVersion, plan.Request, record.Phase, record.PhaseCompleted,
            OfficeDeploymentVerification.Rows(before, "Products").Select(value => value.Text("ProductId")!),
            OfficeDeploymentVerification.Rows(before, "Msi").Select(value => value.Text("ProductCode")!));
        var decision = new OfficeRecoveryPolicy().Evaluate(checkpoint, action, current, result.Verification.Compliant, false, false, true);
        if (decision.Disposition == OfficeRecoveryDisposition.VerifyOnly)
        {
            result.Status = OfficeDeploymentStatus.Completed;
            result.Phase = "Verify";
            result.ReasonCode = "AlreadyCompliant";
            result.AlreadyCompliant = true;
            SetActivation(result, cancellation);
            return result;
        }
        if (decision.Disposition == OfficeRecoveryDisposition.Blocked)
        {
            result.Status = OfficeDeploymentStatus.Blocked;
            result.ReasonCode = decision.ReasonCode;
            result.Error = decision.Detail;
            result.RecoveryRequired = decision.RecoveryRequired;
            result.WrapperExitCode = decision.WrapperExitCode;
            return result;
        }
        var data = plan.Data.Copy();
        data["Before"] = OfficeInventorySerializer.Document(current);
        data["InventoryFingerprint"] = OfficeInventorySerializer.GetFingerprint(current);
        data["RemoveProductId"] = decision.RemainingRemovalIds.Cast<object?>().ToArray();
        var continuation = new OfficeDeploymentPlanDocument(data).Refresh(current, media);
        return preview ? Preflight(continuation, action, options, key, cancellation) : Execute(continuation, action, options, key, cancellation);
    }
}
