using System;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;

namespace PSFoundation.Office;

/// <summary>Explicit scoped Office operations. No prompts, implicit elevation, downloads, retries or reboots.</summary>
public sealed partial class OfficeDeploymentManager
{
    private readonly OfficeDeploymentRuntime runtime;
    public OfficeDeploymentManager() : this(new OfficeDeploymentRuntime()) { }
    internal OfficeDeploymentManager(OfficeDeploymentRuntime runtime) { this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); }

    public OfficeDeploymentPlanDocument GetPlan(OfficeDeploymentRequest request, CancellationToken cancellationToken = default)
    {
        var inventory = runtime.Inventory(cancellationToken);
        var plan = OfficeDeploymentPlanDocument.Create(request, inventory);
        return plan.Refresh(inventory, runtime.Media(plan, cancellationToken));
    }
    public Task<OfficeDeploymentPlanDocument> GetPlanAsync(OfficeDeploymentRequest request, CancellationToken cancellationToken = default)
        => Task.Run(() => GetPlan(request, cancellationToken), cancellationToken);

    /// <summary>Revalidates a reviewed plan without writing, closing applications or invoking ODT. Preview is an observation, never an execution token.</summary>
    public OfficeDeploymentResult Preview(OfficeDeploymentPlanDocument plan, OfficeDeploymentAction expectedAction, OfficeDeploymentOptions options,
        SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return Preflight(plan, expectedAction, options, key, cancellationToken);
    }
    public async Task<OfficeDeploymentResult> PreviewAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentAction expectedAction, OfficeDeploymentOptions options,
        SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        using (var key = productKey?.Copy())
            return await OnWorker(() => Preview(plan, expectedAction, options, key, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public OfficeDeploymentResult Install(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.Install, options, productKey, cancellationToken);
    public OfficeDeploymentResult Remove(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.Remove, options, null, cancellationToken);
    public OfficeDeploymentResult Migrate(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.Migrate, options, productKey, cancellationToken);
    public OfficeDeploymentResult Update(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.Update, options, null, cancellationToken);
    public OfficeDeploymentResult AddLanguage(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.AddLanguage, options, null, cancellationToken);
    public OfficeDeploymentResult RemoveLanguage(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.RemoveLanguage, options, null, cancellationToken);
    public OfficeDeploymentResult SetApplicationSelection(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.SetApplicationSelection, options, null, cancellationToken);
    public OfficeDeploymentResult SetUpdateConfiguration(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.SetUpdateConfiguration, options, null, cancellationToken);
    public OfficeDeploymentResult SetApplicationPreferences(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => Execute(plan, OfficeDeploymentAction.SetApplicationPreference, options, null, cancellationToken);

    /// <remarks>The complete operation stays on one worker so deployment mutex ownership never crosses an await. Once ODT starts, cancellation
    /// waits for it to exit, records observed progress, prevents another phase and performs cleanup before returning a result.</remarks>
    public Task<OfficeDeploymentResult> InstallAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.Install, options, productKey, cancellationToken);
    public Task<OfficeDeploymentResult> RemoveAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.Remove, options, null, cancellationToken);
    public Task<OfficeDeploymentResult> MigrateAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, SecureString? productKey = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.Migrate, options, productKey, cancellationToken);
    public Task<OfficeDeploymentResult> UpdateAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.Update, options, null, cancellationToken);
    public Task<OfficeDeploymentResult> AddLanguageAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.AddLanguage, options, null, cancellationToken);
    public Task<OfficeDeploymentResult> RemoveLanguageAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.RemoveLanguage, options, null, cancellationToken);
    public Task<OfficeDeploymentResult> SetApplicationSelectionAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.SetApplicationSelection, options, null, cancellationToken);
    public Task<OfficeDeploymentResult> SetUpdateConfigurationAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.SetUpdateConfiguration, options, null, cancellationToken);
    public Task<OfficeDeploymentResult> SetApplicationPreferencesAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentOptions options, CancellationToken cancellationToken = default)
        => ExecuteAsync(plan, OfficeDeploymentAction.SetApplicationPreference, options, null, cancellationToken);

    private async Task<OfficeDeploymentResult> ExecuteAsync(OfficeDeploymentPlanDocument plan, OfficeDeploymentAction action, OfficeDeploymentOptions options, SecureString? productKey, CancellationToken cancellation)
    {
        using (var key = productKey?.Copy())
            return await OnWorker(() => Execute(plan, action, options, key, cancellation), cancellation).ConfigureAwait(false);
    }
    private static Task<T> OnWorker<T>(Func<T> operation, CancellationToken cancellation) => Task.Run(operation, cancellation);

    private OfficeDeploymentPlanDocument Confirm(OfficeDeploymentPlanDocument plan, OfficeDeploymentAction action, CancellationToken cancellation)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (plan.Request.Action != action)
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Plan action does not match this operation.");
        cancellation.ThrowIfCancellationRequested();
        var current = runtime.Inventory(cancellation);
        if (!OfficeDocument.Equal(current.MachineId, plan.MachineId) || !OfficeDocument.MatchesFingerprint(OfficeInventorySerializer.Document(current), plan.InventoryFingerprint))
            throw new OfficeException(OfficeFailureReason.StalePlan, "Machine or Office inventory changed; create and review a new plan.");
        var media = runtime.Media(plan, cancellation);
        var fresh = plan.Refresh(current, media);
        if (!OfficeDocument.Equal(plan.MediaFingerprint, fresh.MediaFingerprint)
            && !(media?.Valid == true && OfficeDocument.MatchesFingerprint(OfficeDocument.Parse(media.ManifestJson!), plan.MediaFingerprint)))
            throw new OfficeException(OfficeFailureReason.StaleMedia, "Prepared media changed; create a new plan.");
        return fresh;
    }

    private OfficeDeploymentResult Preflight(OfficeDeploymentPlanDocument plan, OfficeDeploymentAction action, OfficeDeploymentOptions options, SecureString? key, CancellationToken cancellation)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        var fresh = Confirm(plan, action, cancellation);
        var result = new OfficeDeploymentResult(fresh) { ExecutionMetadata = options.ExecutionMetadata };
        if (!fresh.Eligible)
        { result.Status = OfficeDeploymentStatus.Blocked; result.ReasonCode = fresh.Blockers.FirstOrDefault() ?? "InvalidConfiguration"; result.WrapperExitCode = 1; return result; }
        if (key != null)
        {
            if ((action != OfficeDeploymentAction.Install && action != OfficeDeploymentAction.Migrate) || fresh.Request.Configuration?.IsVolumeProduct != true)
                throw new OfficeException(OfficeFailureReason.InvalidAuthority, "ProductKey is restricted to volume installation and migration.");
            OdtTool.ValidateProductKey(key);
        }
        var absent = action == OfficeDeploymentAction.Remove && !OfficeDeploymentVerification.Rows(fresh.Data.Object("Before"), "Products")
            .Any(product => fresh.Request.RemoveProductIds.Contains(product.Text("ProductId")!, StringComparer.OrdinalIgnoreCase));
        if (absent || fresh.State == OfficeDeploymentState.Compliant && action != OfficeDeploymentAction.Remove && action != OfficeDeploymentAction.SetUpdateConfiguration && action != OfficeDeploymentAction.SetApplicationPreference)
        {
            result.Status = OfficeDeploymentStatus.Completed;
            result.AlreadyCompliant = !absent;
            result.ReasonCode = absent ? "AlreadyAbsent" : "AlreadyCompliant";
            result.After = runtime.Inventory(cancellation);
            // A no-op also needs an unchanged observation, rather than asserting success after a concurrent alteration.
            if (!OfficeDocument.Equal(OfficeInventorySerializer.GetFingerprint(result.After), fresh.InventoryFingerprint))
                throw new OfficeException(OfficeFailureReason.StalePlan, "Inventory changed while verifying a no-op.");
            if (fresh.Request.Configuration != null)
            { result.Verification = OfficeDeploymentVerification.AssessTarget(fresh, result.After); SetActivation(result, cancellation); }
            return result;
        }
        try
        { runtime.RequireReady(options, cancellation); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { Fail(result, error, OfficeDeploymentStatus.Blocked, "PreflightFailed"); return result; }
        result.Status = OfficeDeploymentStatus.Preview;
        result.ReasonCode = "NotExecuted";
        return result;
    }

    private OfficeDeploymentResult Execute(OfficeDeploymentPlanDocument plan, OfficeDeploymentAction action, OfficeDeploymentOptions options, SecureString? productKey, CancellationToken cancellation)
    {
        using (var key = productKey?.Copy())
        {
            var result = Preflight(plan, action, options, key, cancellation);
            if (result.Status != OfficeDeploymentStatus.Preview)
                return result;
            result.Status = OfficeDeploymentStatus.Planned;
            result.ReasonCode = null;
            IDisposable? deploymentLock = null;
            FileSystemPath? work = null;
            OfficeDocument? journal = null;
            var completed = false;
            try
            {
                runtime.RequireReady(options, cancellation);
                deploymentLock = runtime.AcquireLock(cancellation);
                var fresh = Confirm(result.Plan, action, cancellation);
                if (!fresh.Eligible)
                    throw new OfficeException(OfficeFailureReason.StalePlan, "Preconditions changed before lock acquisition.");
                result.Plan = fresh;
                var candidate = options.LogRoot.Combine("Work-" + result.RunId);
                runtime.CreateWork(options.LogRoot, candidate, cancellation);
                work = candidate;
                result.RecoveryPath = options.LogRoot.Combine(result.RunId + ".json").Value;
                var now = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                journal = OfficeDocument.Create("SchemaVersion", 1, "RunId", result.RunId, "MachineId", fresh.MachineId, "Action", action.ToString(), "Plan", fresh.Data,
                    "ConfigurationFingerprint", OfficeDocument.Fingerprint(fresh.Data["Configuration"]), "MediaFingerprint", fresh.MediaFingerprint,
                    "Phase", "StageMedia", "PhaseCompleted", false, "NativeResults", Array.Empty<object>(), "RebootRequired", false, "CreatedAt", now, "UpdatedAt", now, "Result", null);
                Checkpoint("StageMedia", false);
                runtime.Stage(fresh, options, work, cancellation);
                Checkpoint("StageMedia", true);
                _ = Confirm(fresh, action, cancellation);
                runtime.RequireReady(options, cancellation);
                Checkpoint("CloseApplications", false);
                runtime.CloseApplications(options.ForceCloseApplications, () => { result.Changed = null; result.ChangeKnown = false; }, cancellation);
                Checkpoint("CloseApplications", true);
                if (action == OfficeDeploymentAction.Migrate && fresh.Request.RemoveProductIds.Count != 0)
                {
                    Phase(OfficeDeploymentAction.Remove, null);
                    if (result.RebootRequired)
                        throw new OfficeException(OfficeFailureReason.RebootRequired, "Removal requested a reboot. Installation was not started.");
                    var afterRemoval = runtime.Inventory(cancellation);
                    if (afterRemoval.Products.Count != 0 || afterRemoval.Unknowns.Count != 0)
                        throw new OfficeException(OfficeFailureReason.VerificationFailed, "Click-to-Run removal was not verified; installation was not started.");
                }
                Phase(action, key);
                Checkpoint("Verify", false);
                result.After = runtime.Inventory(cancellation);
                result.Verification = new OfficeDeploymentVerification().Evaluate(fresh, result.After);
                if (!result.Verification.Compliant)
                {
                    if (action == OfficeDeploymentAction.SetUpdateConfiguration || action == OfficeDeploymentAction.SetApplicationPreference)
                    { result.Status = OfficeDeploymentStatus.AppliedUnverified; result.ReasonCode = "EffectiveSettingsUnknown"; result.WrapperExitCode = 1; }
                    else
                        throw new OfficeException(OfficeFailureReason.VerificationFailed, "ODT finished but deployment postconditions were not fully verified.");
                }
                else
                {
                    result.Status = OfficeDeploymentStatus.Completed;
                    result.Changed = true;
                    result.ChangeKnown = true;
                    result.RecoveryRequired = false;
                    if (fresh.Request.Configuration != null)
                        SetActivation(result, cancellation);
                }
                Checkpoint("Verify", true);
            }
            catch (Exception error)
            {
                Fail(result, error, OfficeDeploymentStatus.Failed, error is OperationCanceledException ? "Cancelled" : "OperationFailed");
                if (error.Data["OfficeExitCode"] is int exit)
                    RecordExit(result, result.Phase, exit);
                if (error.Data["OfficeCleanupError"] is string cleanup)
                    result.Cleanup.Add(cleanup);
                if (result.ReasonCode == "RebootRequired")
                    result.RebootRequired = true;
                try
                { result.After = runtime.Inventory(CancellationToken.None); }
                catch (Exception) { result.After = null; }
            }
            finally
            {
                if (work != null)
                {
                    try
                    { runtime.Cleanup(work, options.LogRoot); }
                    catch (Exception error) { result.Cleanup.Add(error.Message); result.ResidualPaths.Add(work.Value); result.Status = OfficeDeploymentStatus.Failed; result.WrapperExitCode = 1; }
                }
                if (result.RebootRequired && result.WrapperExitCode == 0)
                    result.WrapperExitCode = 3010;
                if (journal != null)
                {
                    try
                    {
                        var log = options.LogRoot.Combine(result.RunId + ".jsonl");
                        result.Logs.Add(log.Value);
                        journal["Result"] = result.Document();
                        Checkpoint(result.Phase, completed);
                        runtime.WriteLog(log, result.ToJson());
                    }
                    catch (Exception error)
                    {
                        result.Cleanup.Add(error.Message);
                        result.Status = OfficeDeploymentStatus.Failed;
                        result.WrapperExitCode = 1;
                        // A log failure must not leave a successfully written journal claiming overall success.
                        try
                        { journal["Result"] = result.Document(); Checkpoint(result.Phase, completed); }
                        catch (Exception persistenceError) { result.Cleanup.Add(persistenceError.Message); }
                    }
                }
                if (deploymentLock != null)
                {
                    try
                    { deploymentLock.Dispose(); }
                    catch (Exception error) { result.Cleanup.Add(error.Message); result.Status = OfficeDeploymentStatus.Failed; result.WrapperExitCode = 1; }
                }
            }
            return result;

            void Checkpoint(string phase, bool done)
            {
                result.Phase = phase;
                completed = done;
                journal!["Phase"] = phase;
                journal["PhaseCompleted"] = done;
                journal["UpdatedAt"] = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                journal["NativeResults"] = result.Native.Select(item => OfficeDocument.Create("Phase", item.Phase, "ExitCode", item.ExitCode)).ToArray();
                journal["RebootRequired"] = result.RebootRequired;
                runtime.WriteJournal(FileSystemPath.Parse(result.RecoveryPath!), journal.ToJson());
            }
            void Phase(OfficeDeploymentAction phase, SecureString? phaseKey)
            {
                cancellation.ThrowIfCancellationRequested();
                Checkpoint(phase.ToString(), false);
                result.Changed = null;
                result.ChangeKnown = false;
                result.RecoveryRequired = true;
                var exit = runtime.RunPhase(result.Plan, phase, work!, phaseKey, cancellation);
                RecordExit(result, phase.ToString(), exit);
                Checkpoint(phase.ToString(), true);
                if (exit != 0 && exit != 3010)
                    throw new OfficeException(OfficeFailureReason.NativeFailure, "ODT " + phase + " returned " + exit + "; current machine state requires inspection.");
                cancellation.ThrowIfCancellationRequested();
            }
        }
    }

    private void SetActivation(OfficeDeploymentResult result, CancellationToken cancellation)
    {
        result.Activation = runtime.Activation(result.Plan.Request.Configuration!.Product, cancellation);
        if (result.Activation == OfficeActivationState.NotVerified)
        { result.ReasonCode = "ActivationNotVerified"; result.WrapperExitCode = 1; }
    }
    private static void RecordExit(OfficeDeploymentResult result, string phase, int exit)
    { result.Native.Add(new OfficeNativePhaseResult(phase, exit)); result.ExitCode = exit; result.RebootRequired |= exit == 3010; }
    private static void Fail(OfficeDeploymentResult result, Exception error, OfficeDeploymentStatus status, string fallback)
    {
        result.Status = status;
        result.ReasonCode = error is OfficeException office ? office.Reason.ToString() : fallback;
        result.Error = error.Message;
        result.Diagnostic = error.Data["OfficeDiagnostic"];
        result.WrapperExitCode = 1;
    }
}
