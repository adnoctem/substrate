using System;
using System.Collections.Generic;
using System.Linq;

namespace AdNoctem.Substrate.Office;

public enum OfficeRecoveryDisposition { Blocked, VerifyOnly, Continue }

/// <summary>Detached checkpoint facts. Construction does not prove journal integrity or authorize execution.</summary>
public sealed class OfficeRecoveryCheckpoint
{
    public int SchemaVersion { get; }
    public OfficeDeploymentRequest Request { get; }
    public string Phase { get; }
    public bool PhaseCompleted { get; }
    public IReadOnlyList<string> PreviousProductIds { get; }
    public IReadOnlyList<string> PreviousMsiProductCodes { get; }
    public OfficeRecoveryCheckpoint(int schemaVersion, OfficeDeploymentRequest request, string phase, bool phaseCompleted,
        IEnumerable<string> previousProductIds, IEnumerable<string> previousMsiProductCodes)
    {
        if (schemaVersion != 1 && schemaVersion != 2)
            throw new OfficeException(OfficeFailureReason.InvalidRecoveryRecord, "Recovery schema is unsupported.");
        Request = request ?? throw new ArgumentNullException(nameof(request));
        if (request.Action != OfficeDeploymentAction.Install && request.Action != OfficeDeploymentAction.Migrate)
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Only installation or migration can be considered for continuation.");
        SchemaVersion = schemaVersion;
        Phase = phase ?? throw new ArgumentNullException(nameof(phase));
        PhaseCompleted = phaseCompleted;
        PreviousProductIds = Copy(previousProductIds);
        PreviousMsiProductCodes = Copy(previousMsiProductCodes);
    }
    private static IReadOnlyList<string> Copy(IEnumerable<string> values)
    {
        var copy = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
        if (copy.Any(string.IsNullOrWhiteSpace))
            throw new OfficeException(OfficeFailureReason.InvalidRecoveryRecord, "Checkpoint product identifiers must be nonempty.");
        return Array.AsReadOnly(copy);
    }
}
/// <summary>Whether recorded evidence permits a specific continuation, together with reasons for blocking or accepting it.</summary>
public sealed class OfficeRecoveryDecision
{
    public OfficeRecoveryDisposition Disposition { get; }
    public string ReasonCode { get; }
    public string? Detail { get; }
    public bool RecoveryRequired { get; }
    public int WrapperExitCode { get; }
    public IReadOnlyList<string> RemainingRemovalIds { get; }
    internal OfficeRecoveryDecision(OfficeRecoveryDisposition disposition, string reason, string? detail = null, bool recoveryRequired = false,
        int exitCode = 0, IEnumerable<string>? removal = null)
    {
        Disposition = disposition;
        ReasonCode = reason;
        Detail = detail;
        RecoveryRequired = recoveryRequired;
        WrapperExitCode = exitCode;
        RemainingRemovalIds = Array.AsReadOnly((removal ?? Array.Empty<string>()).ToArray());
    }
}

/// <summary>Pure continuation decisions. Callers must first reopen and validate the protected journal and collect fresh observations.</summary>
public sealed class OfficeRecoveryPolicy
{
    public OfficeRecoveryDecision Evaluate(OfficeRecoveryCheckpoint checkpoint, OfficeDeploymentAction expectedAction, OfficeInventory current,
        bool compliant, bool deploymentBusy, bool rebootPending, bool mediaMatches)
    {
        if (current == null)
            throw new ArgumentNullException(nameof(current));
        return Evaluate(checkpoint, expectedAction, current.Products.Select(product => product.ProductId), current.Msi.Select(product => product.ProductCode),
            current.Unknowns.Count != 0, compliant, deploymentBusy, rebootPending, mediaMatches);
    }
    public OfficeRecoveryDecision Evaluate(OfficeRecoveryCheckpoint checkpoint, OfficeDeploymentAction expectedAction, IEnumerable<string> currentProductIds,
        IEnumerable<string> currentMsiProductCodes, bool hasUnknownInventory, bool compliant, bool deploymentBusy, bool rebootPending, bool mediaMatches)
    {
        if (checkpoint == null)
            throw new ArgumentNullException(nameof(checkpoint));
        if (checkpoint.SchemaVersion == 2)
            throw new OfficeException(OfficeFailureReason.UnsupportedPilotRecovery, "Pilot journals are evidence only. Review the result or restore the VM; automatic replay is not supported.");
        if (checkpoint.Request.Action != expectedAction)
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "This recovery command cannot resume the recorded operation.");
        var products = (currentProductIds ?? throw new ArgumentNullException(nameof(currentProductIds))).ToArray();
        var msi = (currentMsiProductCodes ?? throw new ArgumentNullException(nameof(currentMsiProductCodes))).ToArray();
        if (deploymentBusy)
            return Blocked("DeploymentBusy");
        if (rebootPending)
            return Blocked("RebootRequired", 3010);
        if (!mediaMatches)
            throw new OfficeException(OfficeFailureReason.StaleMedia, "Recovery media no longer matches the recorded deployment.");
        if (compliant)
            return new OfficeRecoveryDecision(OfficeRecoveryDisposition.VerifyOnly, "AlreadyCompliant");
        var target = checkpoint.Request.Configuration!.Product.ToString();
        if (hasUnknownInventory || products.Any(string.IsNullOrWhiteSpace) || msi.Any(string.IsNullOrWhiteSpace)
            || products.Any(id => !checkpoint.PreviousProductIds.Contains(id, StringComparer.OrdinalIgnoreCase) && !string.Equals(id, target, StringComparison.OrdinalIgnoreCase))
            || msi.Any(id => !checkpoint.PreviousMsiProductCodes.Contains(id, StringComparer.OrdinalIgnoreCase)))
            return Blocked("Conflict");
        var beforeLaunch = string.Equals(checkpoint.Phase, "StageMedia", StringComparison.OrdinalIgnoreCase) || string.Equals(checkpoint.Phase, "CloseApplications", StringComparison.OrdinalIgnoreCase);
        var afterRemoval = expectedAction == OfficeDeploymentAction.Migrate && string.Equals(checkpoint.Phase, "Remove", StringComparison.OrdinalIgnoreCase)
            && checkpoint.PhaseCompleted && products.Length == 0;
        if (!beforeLaunch && !afterRemoval)
            return new OfficeRecoveryDecision(OfficeRecoveryDisposition.Blocked, "UnsupportedRecoveryState",
                "The recorded checkpoint and current inventory do not prove a safe continuation. Review the journal and fresh inventory; do not replay an uncertain installer.", true, 1);
        return new OfficeRecoveryDecision(OfficeRecoveryDisposition.Continue, "ContinuationAllowed", removal: checkpoint.Request.RemoveProductIds.Where(id => products.Contains(id, StringComparer.OrdinalIgnoreCase)));
    }
    private static OfficeRecoveryDecision Blocked(string reason, int exitCode = 1) => new OfficeRecoveryDecision(OfficeRecoveryDisposition.Blocked, reason, exitCode: exitCode);
}
