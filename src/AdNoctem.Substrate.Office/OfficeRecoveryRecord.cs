using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Office;

/// <summary>A recovery-record validation issue, distinct from authorization to replay a deployment.</summary>
[Serializable]
public sealed class OfficeRecoveryDiagnostic
{
    public string Stage { get; }
    public string Category { get; }
    public string ObjectKind => "RecoveryJournal";
    public string? Path { get; }
    public string ExceptionType { get; }
    internal OfficeRecoveryDiagnostic(string stage, string category, string? path, string type)
    { Stage = stage; Category = category; Path = path; ExceptionType = type; }
}

/// <summary>A validated checkpoint, not permission to replay an operation. Recovery must rediscover current machine state.</summary>
public sealed class OfficeRecoveryRecord
{
    internal OfficeDocument Data { get; }
    public int SchemaVersion => (int)Data.Integer("SchemaVersion");
    public string RunId => Data.Text("RunId")!;
    public string MachineId => Data.Text("MachineId")!;
    public OfficeDeploymentAction Action { get; }
    public string Phase => Data.Text("Phase")!;
    public bool PhaseCompleted => Data.Boolean("PhaseCompleted");
    public bool RebootRequired => Data.Boolean("RebootRequired");
    public OfficeDeploymentPlanDocument? Plan { get; }
    public string ToJson() => Data.ToJson();
    private OfficeRecoveryRecord(OfficeDocument data, OfficeDeploymentAction action, OfficeDeploymentPlanDocument? plan)
    { Data = data; Action = action; Plan = plan; }

    /// <summary>Validates a recovery document's structure, run identity, and expected machine identity before accepting its evidence.</summary>
    public static OfficeRecoveryRecord Parse(string json, string expectedRunId, string expectedMachineId)
        => Parse(json, expectedRunId, expectedMachineId, null);

    internal static OfficeRecoveryRecord Parse(string json, string expectedRunId, string expectedMachineId, string? path)
    {
        if (!OfficeVersionResolver.IsMatch(expectedRunId, "^[a-fA-F0-9]{32}$"))
            throw new ArgumentException("A 32-digit hexadecimal run identifier is required.", nameof(expectedRunId));
        if (string.IsNullOrWhiteSpace(expectedMachineId))
            throw new ArgumentException("The current machine identity is required.", nameof(expectedMachineId));
        var stage = "Json";
        try
        {
            var record = OfficeDocument.Parse(json);
            stage = "Schema";
            record.Require(new[] { "SchemaVersion", "RunId", "MachineId", "Action", "Plan", "ConfigurationFingerprint", "MediaFingerprint", "Phase", "PhaseCompleted", "NativeResults", "RebootRequired", "CreatedAt", "UpdatedAt", "Result" });
            var schema = record.Integer("SchemaVersion");
            if (schema != 1 && schema != 2)
                throw Invalid("Recovery schema is unsupported.");
            stage = "Identity";
            if (!OfficeDocument.Equal(record.Text("RunId"), expectedRunId) || !OfficeDocument.Equal(record.Text("MachineId"), expectedMachineId))
                throw Invalid("Recovery run or machine does not match.");
            stage = "Schema";
            var plan = record.Object("Plan");
            var required = OfficeDeploymentPlanDocument.RequiredFields;
            if (schema == 2)
            {
                required = required.Concat(new[] { "PilotMigration", "PilotResources" }).ToArray();
                if (!OfficeDocument.Equal(record.Text("Action"), "Migrate") || !plan.Boolean("PilotMigration"))
                    throw Invalid("Schema-2 journals are restricted to migration pilots.");
                plan.Object("PilotResources").Require(new[] { "UiLanguages", "PrimaryLanguage", "ProofingLanguages", "Provisioning", "Verification" });
            }
            plan.Require(required, "Media");
            stage = "Context";
            if (plan.Integer("SchemaVersion") != schema || !OfficeDocument.Equal(plan.Text("Action"), record.Text("Action"))
                || !OfficeDocument.Equal(plan.Text("MachineId"), record.Text("MachineId")) || !OfficeDocument.Equal(plan.Text("MediaFingerprint"), record.Text("MediaFingerprint")))
                throw Invalid("Recovery operation context is inconsistent.");
            _ = plan.Boolean("RemoveMsi");
            _ = record.Boolean("PhaseCompleted");
            _ = record.Boolean("RebootRequired");
            _ = record.Text("Phase");
            _ = record.Array("NativeResults");
            _ = record.Text("CreatedAt");
            _ = record.Text("UpdatedAt");
            stage = "InventoryFingerprint";
            if (!OfficeDocument.MatchesFingerprint(plan["Before"], plan.Text("InventoryFingerprint")))
                throw Invalid("Recorded inventory fingerprint differs.");
            stage = "Authority";
            var action = OfficeDeploymentPlanDocument.ParseEnum<OfficeDeploymentAction>(record.Text("Action"));
            if (action == OfficeDeploymentAction.Install && (plan.Boolean("RemoveMsi") || plan.Array("RemoveProductId").Length != 0))
                throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Install recovery cannot contain removal authority.");
            stage = "Settings";
            // Validate legacy pilot request data without making it resumable in the ordinary backend.
            var ordinary = OfficeDocument.Create(OfficeDeploymentPlanDocument.RequiredFields.Concat(plan.Has("Media") ? new[] { "Media" } : Array.Empty<string>()).SelectMany(name => new object?[] { name, plan[name] }).ToArray());
            ordinary["SchemaVersion"] = 1;
            var typedPlan = new OfficeDeploymentPlanDocument(ordinary.Copy());
            stage = "Configuration";
            if (plan["Configuration"] != null)
            {
                var normalized = OfficeDeploymentPlanDocument.NormalizeConfiguration(plan.Object("Configuration"), out _);
                stage = "ConfigurationFingerprint";
                if (!OfficeDocument.MatchesFingerprint(normalized, record.Text("ConfigurationFingerprint")))
                    throw Invalid("Recorded target fingerprint differs.");
            }
            return new OfficeRecoveryRecord(record, action, schema == 1 ? typedPlan : null);
        }
        catch (Exception error) when (!(error is OperationCanceledException)) { throw Failure(stage, path, error); }
    }

    internal static OfficeException Failure(string stage, string? path, Exception error)
    {
        var office = error as OfficeException;
        var category = stage == "Json" ? "Json" : office == null ? stage == "Read" ? stage : "InternalValidation" : "Validation";
        var failure = new OfficeException(stage == "Json" ? OfficeFailureReason.InvalidRecoveryRecord : office?.Reason ?? OfficeFailureReason.InvalidRecoveryRecord,
            "Recovery journal validation failed at " + stage + (path == null ? "" : " for '" + path + "'") + " (" + category + "); no operation was resumed.");
        failure.Data["OfficeDiagnostic"] = new OfficeRecoveryDiagnostic(stage, category, path, error.GetType().FullName!);
        return failure;
    }
    private static OfficeException Invalid(string message) => new OfficeException(OfficeFailureReason.InvalidRecoveryRecord, message);
}

/// <summary>Reads protected recovery journals and validates their machine and run identity.</summary>
public sealed class OfficeRecoveryManager
{
    /// <summary>Reads and validates a protected recovery record for the requested run.</summary>
    public OfficeRecoveryRecord Read(string runId, FileSystemPath logRoot, CancellationToken cancellationToken = default)
    {
        if (logRoot == null)
            throw new ArgumentNullException(nameof(logRoot));
        if (!OfficeVersionResolver.IsMatch(runId, "^[a-fA-F0-9]{32}$"))
            throw new ArgumentException("A 32-digit hexadecimal run identifier is required.", nameof(runId));
        var path = logRoot.Combine(runId + ".json");
        string json;
        try
        { json = new OfficeJournalStore().Read(path, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (OfficeException error) when (error.Data.Contains("OfficeDiagnostic")) { throw; }
        catch (Exception error) { throw OfficeRecoveryRecord.Failure("Read", path.Value, error); }
        cancellationToken.ThrowIfCancellationRequested();
        return OfficeRecoveryRecord.Parse(json, runId, new OfficeRegistryReader().ReadMachineId(), path.Value);
    }
    /// <summary>Reads protected recovery evidence on a worker thread without replaying deployment.</summary>
    public Task<OfficeRecoveryRecord> ReadAsync(string runId, FileSystemPath logRoot, CancellationToken cancellationToken = default)
        => Task.Run(() => Read(runId, logRoot, cancellationToken), cancellationToken);
}
