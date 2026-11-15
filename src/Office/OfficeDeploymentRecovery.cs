using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Office;

/// <summary>Evidence that the machine changed since a recovery journal was recorded. A difference alone does not authorize or block continuation.</summary>
public sealed class OfficeRecoveryDivergence
{
    public bool DiffersFromBefore { get; }
    public bool HasRecordedAfter { get; }
    public bool DiffersFromAfter { get; }
    public string RecordedBeforeFingerprint { get; }
    public string? RecordedAfterFingerprint { get; }
    public string CurrentFingerprint { get; }
    public IReadOnlyList<string> ChangedFieldsSinceBefore { get; }
    public IReadOnlyList<string> ChangedFieldsSinceAfter { get; }

    internal OfficeRecoveryDivergence(
        string recordedBeforeFingerprint,
        string? recordedAfterFingerprint,
        string currentFingerprint,
        IEnumerable<string> changedFieldsSinceBefore,
        IEnumerable<string> changedFieldsSinceAfter
    )
    {
        RecordedBeforeFingerprint = recordedBeforeFingerprint;
        RecordedAfterFingerprint = recordedAfterFingerprint;
        CurrentFingerprint = currentFingerprint;
        DiffersFromBefore = !OfficeDocument.Equal(recordedBeforeFingerprint, currentFingerprint);
        HasRecordedAfter = recordedAfterFingerprint != null;
        DiffersFromAfter =
            HasRecordedAfter && !OfficeDocument.Equal(recordedAfterFingerprint, currentFingerprint);
        ChangedFieldsSinceBefore = Array.AsReadOnly(changedFieldsSinceBefore.ToArray());
        ChangedFieldsSinceAfter = Array.AsReadOnly(changedFieldsSinceAfter.ToArray());
    }

    internal static OfficeRecoveryDivergence Create(
        OfficeDocument recordedBefore,
        OfficeDocument? recordedAfter,
        OfficeDocument current
    )
    {
        var beforeFingerprint = OfficeDocument.Fingerprint(recordedBefore);
        var afterFingerprint =
            recordedAfter == null ? null : OfficeDocument.Fingerprint(recordedAfter);
        var currentFingerprint = OfficeDocument.Fingerprint(current);

        return new OfficeRecoveryDivergence(
            beforeFingerprint,
            afterFingerprint,
            currentFingerprint,
            ChangedFields(recordedBefore, current),
            recordedAfter == null ? Array.Empty<string>() : ChangedFields(recordedAfter, current)
        );
    }

    internal OfficeDocument Document() =>
        OfficeDocument.Create(
            "DiffersFromBefore",
            DiffersFromBefore,
            "HasRecordedAfter",
            HasRecordedAfter,
            "DiffersFromAfter",
            DiffersFromAfter,
            "RecordedBeforeFingerprint",
            RecordedBeforeFingerprint,
            "RecordedAfterFingerprint",
            RecordedAfterFingerprint,
            "CurrentFingerprint",
            CurrentFingerprint,
            "ChangedFieldsSinceBefore",
            ChangedFieldsSinceBefore.Cast<object?>().ToArray(),
            "ChangedFieldsSinceAfter",
            ChangedFieldsSinceAfter.Cast<object?>().ToArray()
        );

    private static string[] ChangedFields(OfficeDocument recorded, OfficeDocument current) =>
        recorded
            .Names.Concat(current.Names)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => !Same(recorded, current, name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool Same(OfficeDocument recorded, OfficeDocument current, string name)
    {
        if (recorded.Has(name) != current.Has(name))
            return false;

        return !recorded.Has(name)
            || OfficeDocument.Equal(
                OfficeDocument.Fingerprint(recorded[name]),
                OfficeDocument.Fingerprint(current[name])
            );
    }
}

/// <summary>Evidence produced by retiring a recovery journal and archiving its journal files.</summary>
public sealed class OfficeRecoveryRetirement
{
    public string RunId { get; }
    public string RetiredAt { get; }
    public string AuditPath { get; }
    public string ArchivePath { get; }
    public IReadOnlyList<string> ArchivedPaths { get; }
    public string CurrentInventoryFingerprint { get; }

    internal OfficeRecoveryRetirement(
        string runId,
        string retiredAt,
        string auditPath,
        string archivePath,
        IEnumerable<string> archivedPaths,
        string currentInventoryFingerprint
    )
    {
        RunId = runId;
        RetiredAt = retiredAt;
        AuditPath = auditPath;
        ArchivePath = archivePath;
        ArchivedPaths = Array.AsReadOnly(archivedPaths.ToArray());
        CurrentInventoryFingerprint = currentInventoryFingerprint;
    }

    public string ToJson() => Document().ToJson();

    internal OfficeDocument Document() =>
        OfficeDocument.Create(
            "SchemaVersion",
            1,
            "RunId",
            RunId,
            "RetiredAt",
            RetiredAt,
            "AuditPath",
            AuditPath,
            "ArchivePath",
            ArchivePath,
            "ArchivedPaths",
            ArchivedPaths.Cast<object?>().ToArray(),
            "CurrentInventoryFingerprint",
            CurrentInventoryFingerprint
        );
}

public sealed partial class OfficeDeploymentManager
{
    /// <summary>Reopens protected recovery evidence and evaluates it without mutation. Only proven pre-launch or completed-removal checkpoints can continue.</summary>
    public OfficeDeploymentResult PreviewRecovery(
        string runId,
        OfficeDeploymentAction expectedAction,
        OfficeDeploymentOptions options,
        SecureString? productKey = null,
        CancellationToken cancellationToken = default
    )
    {
        using (var key = productKey?.Copy())
            return Recover(runId, expectedAction, options, key, true, cancellationToken);
    }

    public OfficeDeploymentResult ResumeInstallation(
        string runId,
        OfficeDeploymentOptions options,
        SecureString? productKey = null,
        CancellationToken cancellationToken = default
    )
    {
        using (var key = productKey?.Copy())
            return Recover(
                runId,
                OfficeDeploymentAction.Install,
                options,
                key,
                false,
                cancellationToken
            );
    }

    public OfficeDeploymentResult ResumeMigration(
        string runId,
        OfficeDeploymentOptions options,
        SecureString? productKey = null,
        CancellationToken cancellationToken = default
    )
    {
        using (var key = productKey?.Copy())
            return Recover(
                runId,
                OfficeDeploymentAction.Migrate,
                options,
                key,
                false,
                cancellationToken
            );
    }

    public async Task<OfficeDeploymentResult> PreviewRecoveryAsync(
        string runId,
        OfficeDeploymentAction expectedAction,
        OfficeDeploymentOptions options,
        SecureString? productKey = null,
        CancellationToken cancellationToken = default
    )
    {
        using (var key = productKey?.Copy())
            return await OnWorker(
                    () => Recover(runId, expectedAction, options, key, true, cancellationToken),
                    cancellationToken
                )
                .ConfigureAwait(false);
    }

    /// <summary>Continues only an installation checkpoint whose protected journal and current machine state prove continuation is safe.</summary>
    /// <remarks>Unknown native outcomes and historical pilot journals are not replayed. Recovery is not a rollback mechanism.</remarks>
    public async Task<OfficeDeploymentResult> ResumeInstallationAsync(
        string runId,
        OfficeDeploymentOptions options,
        SecureString? productKey = null,
        CancellationToken cancellationToken = default
    )
    {
        using (var key = productKey?.Copy())
            return await OnWorker(
                    () =>
                        Recover(
                            runId,
                            OfficeDeploymentAction.Install,
                            options,
                            key,
                            false,
                            cancellationToken
                        ),
                    cancellationToken
                )
                .ConfigureAwait(false);
    }

    /// <summary>Continues a migration only from an accepted checkpoint after revalidating journal, machine, media, and action authority.</summary>
    /// <remarks>A completed-removal checkpoint can authorize the installation phase; an uncertain in-flight native phase cannot.</remarks>
    public async Task<OfficeDeploymentResult> ResumeMigrationAsync(
        string runId,
        OfficeDeploymentOptions options,
        SecureString? productKey = null,
        CancellationToken cancellationToken = default
    )
    {
        using (var key = productKey?.Copy())
            return await OnWorker(
                    () =>
                        Recover(
                            runId,
                            OfficeDeploymentAction.Migrate,
                            options,
                            key,
                            false,
                            cancellationToken
                        ),
                    cancellationToken
                )
                .ConfigureAwait(false);
    }

    /// <summary>Retires a validated recovery journal, writes an audit record, and archives its files under Abandoned.</summary>
    /// <remarks>Retirement is evidence preservation, not rollback. It never executes ODT or changes Office.</remarks>
    public OfficeRecoveryRetirement RetireRecovery(
        string runId,
        string reason,
        FileSystemPath logRoot,
        string? executionMetadataJson = null,
        CancellationToken cancellationToken = default
    )
    {
        if (logRoot == null)
            throw new ArgumentNullException(nameof(logRoot));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A retirement reason is required.", nameof(reason));

        if (!OfficeVersionResolver.IsMatch(runId, "^[a-fA-F0-9]{32}$"))
            throw new ArgumentException(
                "A 32-digit hexadecimal run identifier is required.",
                nameof(runId)
            );

        var record = runtime.ReadRecovery(runId, logRoot, cancellationToken);

        if (!OfficeDocument.Equal(record.RunId, runId))
            throw new OfficeException(
                OfficeFailureReason.InvalidRecoveryRecord,
                "Recovery run identity does not match the requested run."
            );

        var current = runtime.Inventory(cancellationToken);

        if (!OfficeDocument.Equal(record.MachineId, current.MachineId))
            throw new OfficeException(
                OfficeFailureReason.InvalidRecoveryRecord,
                "Recovery journal machine identity does not match the current machine."
            );

        var retiredAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var auditPath = logRoot.Combine(runId + ".abandoned.json");
        var archivePath = logRoot.Combine("Abandoned");
        var fingerprint = OfficeInventorySerializer.GetFingerprint(current);
        var audit = OfficeDocument.Create(
            "SchemaVersion",
            1,
            "RunId",
            record.RunId,
            "MachineId",
            record.MachineId,
            "RetiredAt",
            retiredAt,
            "Reason",
            reason,
            "InventoryFingerprint",
            fingerprint,
            "Execution",
            OfficeDeploymentOptions.BuildExecutionMetadata(executionMetadataJson)
        );
        runtime.WriteRecoveryRetirement(auditPath, audit.ToJson(), cancellationToken);
        var archived = runtime.ArchiveRecoveryArtifacts(logRoot, runId, cancellationToken);

        return new OfficeRecoveryRetirement(
            record.RunId,
            retiredAt,
            auditPath.Value,
            archivePath.Value,
            archived,
            fingerprint
        );
    }

    /// <summary>Retires recovery evidence on a worker thread without executing Office deployment.</summary>
    public Task<OfficeRecoveryRetirement> RetireRecoveryAsync(
        string runId,
        string reason,
        FileSystemPath logRoot,
        string? executionMetadataJson = null,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () => RetireRecovery(runId, reason, logRoot, executionMetadataJson, cancellationToken),
            cancellationToken
        );

    private OfficeDeploymentResult Recover(
        string runId,
        OfficeDeploymentAction action,
        OfficeDeploymentOptions options,
        SecureString? key,
        bool preview,
        CancellationToken cancellation
    )
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (action != OfficeDeploymentAction.Install && action != OfficeDeploymentAction.Migrate)
            throw new OfficeException(
                OfficeFailureReason.InvalidAuthority,
                "Only installation and migration support continuation."
            );

        var record = runtime.ReadRecovery(runId, options.LogRoot, cancellation);

        if (record.SchemaVersion == 2)
            throw new OfficeException(
                OfficeFailureReason.UnsupportedPilotRecovery,
                "Pilot journals are evidence only. Review the result or restore the VM; automatic replay is not supported."
            );

        if (record.Action != action)
            throw new OfficeException(
                OfficeFailureReason.InvalidAuthority,
                "This recovery operation cannot resume the recorded action."
            );

        var plan = record.Plan!;
        var current = runtime.Inventory(cancellation);
        var divergence = OfficeRecoveryDivergence.Create(
            plan.Data.Object("Before"),
            RecordedAfter(record),
            OfficeInventorySerializer.Document(current)
        );
        var result = new OfficeDeploymentResult(plan)
        {
            RecoveryObservation = true,
            ExecutionMetadata = options.ExecutionMetadata,
            RecoveryPath = options.LogRoot.Combine(runId + ".json").Value,
            After = current,
            Verification = OfficeDeploymentVerification.AssessTarget(plan, current),
            RecoveryDivergence = divergence,
        };

        // Preserve priority: busy/reboot observations block before any media inspection or continuation.
        if (runtime.DeploymentBusy(cancellation))
        {
            result.Status = OfficeDeploymentStatus.Blocked;
            result.ReasonCode = "DeploymentBusy";
            result.WrapperExitCode = 1;

            return result;
        }

        result.RebootRequired = runtime.RebootPending(cancellation);

        if (result.RebootRequired)
        {
            result.Status = OfficeDeploymentStatus.Blocked;
            result.ReasonCode = "RebootRequired";
            result.WrapperExitCode = 3010;

            return result;
        }

        var media = runtime.Media(plan, cancellation);

        if (media == null || !media.Valid)
            throw new OfficeException(
                OfficeFailureReason.StaleMedia,
                "Recovery media no longer matches the recorded deployment."
            );

        OfficeDeploymentRuntime.RequireMatchingMedia(plan, media, OfficeFailureReason.StaleMedia);
        var before = plan.Data.Object("Before");
        var checkpoint = new OfficeRecoveryCheckpoint(
            record.SchemaVersion,
            plan.Request,
            record.Phase,
            record.PhaseCompleted,
            OfficeDeploymentVerification
                .Rows(before, "Products")
                .Select(value => value.Text("ProductId")!),
            OfficeDeploymentVerification
                .Rows(before, "Msi")
                .Select(value => value.Text("ProductCode")!)
        );
        var decision = new OfficeRecoveryPolicy().Evaluate(
            checkpoint,
            action,
            current,
            result.Verification.Compliant,
            false,
            false,
            true
        );

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

        if (!continuation.Eligible)
        {
            result.Plan = continuation;
            result.Status = OfficeDeploymentStatus.Blocked;
            result.ReasonCode = "StaleRecoveryState";
            result.Error = DivergenceError(divergence, continuation.Blockers);
            result.WrapperExitCode = 1;
            result.RecoveryRequired = false;

            return result;
        }

        var outcome = preview
            ? Preflight(continuation, action, options, key, cancellation)
            : Execute(continuation, action, options, key, cancellation);
        outcome.RecoveryDivergence = divergence;

        return outcome;
    }

    private static OfficeDocument? RecordedAfter(OfficeRecoveryRecord record)
    {
        var result = record.Data["Result"];

        return result is OfficeDocument document && document["After"] is OfficeDocument after
            ? after
            : null;
    }

    private static string DivergenceError(
        OfficeRecoveryDivergence divergence,
        IEnumerable<string> blockers
    )
    {
        var changed =
            divergence.ChangedFieldsSinceBefore.Count == 0
                ? "none detected in the recorded Before fields"
                : string.Join(", ", divergence.ChangedFieldsSinceBefore);
        var blocked = string.Join(", ", blockers);

        return "The machine changed since the recovery journal was recorded. Changed inventory fields: "
            + changed
            + ". The refreshed plan is not eligible because of: "
            + (string.IsNullOrEmpty(blocked) ? "an unspecified blocker" : blocked)
            + ".";
    }
}
