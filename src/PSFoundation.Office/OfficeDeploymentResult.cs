using System;
using System.Collections.Generic;
using System.Linq;
using PSFoundation.IO;

namespace PSFoundation.Office;

public enum OfficeDeploymentStatus { Planned, Preview, Blocked, Completed, Failed, AppliedUnverified }

/// <summary>Explicit execution inputs. Paths identify caller-prepared tools and a protected local journal root; nothing is downloaded implicitly.</summary>
public sealed class OfficeDeploymentOptions
{
    public FileSystemPath OdtPath { get; }
    public FileSystemPath LogRoot { get; }
    public bool ForceCloseApplications { get; }
    internal OfficeDocument ExecutionMetadata { get; }
    /// <param name="executionMetadataJson">Optional data-only JSON object identifying the caller/runtime in durable reports. Never include credentials or secrets.</param>
    public OfficeDeploymentOptions(FileSystemPath odtPath, FileSystemPath logRoot, bool forceCloseApplications = false, string? executionMetadataJson = null)
    {
        OdtPath = odtPath ?? throw new ArgumentNullException(nameof(odtPath));
        LogRoot = logRoot ?? throw new ArgumentNullException(nameof(logRoot));
        ForceCloseApplications = forceCloseApplications;
        ExecutionMetadata = executionMetadataJson == null ? OfficeDocument.Create("ComponentVersion", typeof(OfficeDeploymentManager).Assembly.GetName().Version!.ToString(),
            "RuntimeVersion", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, "ProcessBitness", IntPtr.Size * 8) : OfficeDocument.Parse(executionMetadataJson);
    }
}
public sealed class OfficeNativePhaseResult
{
    public string Phase { get; }
    public int ExitCode { get; }
    internal OfficeNativePhaseResult(string phase, int exitCode) { Phase = phase; ExitCode = exitCode; }
}

public sealed class OfficeDeploymentResult
{
    internal readonly List<OfficeNativePhaseResult> Native = new List<OfficeNativePhaseResult>();
    internal readonly List<string> Cleanup = new List<string>();
    internal readonly List<string> ResidualPaths = new List<string>();
    internal readonly List<string> Logs = new List<string>();
    public string RunId { get; } = Guid.NewGuid().ToString("N");
    public OfficeDeploymentPlanDocument Plan { get; internal set; }
    public string Action => RecoveryObservation ? "Recover" : Plan.Request.Action.ToString();
    internal bool RecoveryObservation { get; set; }
    internal OfficeDocument? ExecutionMetadata { get; set; }
    public OfficeDeploymentStatus Status { get; internal set; } = OfficeDeploymentStatus.Planned;
    public string Phase { get; internal set; } = "Preflight";
    public string? ReasonCode { get; internal set; }
    public bool? Changed { get; internal set; } = false;
    public bool ChangeKnown { get; internal set; } = true;
    public bool AlreadyCompliant { get; internal set; }
    public bool RecoveryRequired { get; internal set; }
    public bool RebootRequired { get; internal set; }
    public int? ExitCode { get; internal set; }
    public int WrapperExitCode { get; internal set; }
    public string? RecoveryPath { get; internal set; }
    public string? Error { get; internal set; }
    public object? Diagnostic { get; internal set; }
    public OfficeInventory? After { get; internal set; }
    public OfficeDeploymentAssessment? Verification { get; internal set; }
    public OfficeActivationState? Activation { get; internal set; }
    public IReadOnlyList<OfficeNativePhaseResult> NativeResults => Native.AsReadOnly();
    public IReadOnlyList<string> CleanupErrors => Cleanup.AsReadOnly();
    public IReadOnlyList<string> Residue => ResidualPaths.AsReadOnly();
    public IReadOnlyList<string> LogPaths => Logs.AsReadOnly();
    internal OfficeDeploymentResult(OfficeDeploymentPlanDocument plan) { Plan = plan; }
    public string ToJson() => Document().ToJson();
    internal OfficeDocument Document()
    {
        var after = After == null ? null : OfficeInventorySerializer.Document(After);
        OfficeDocument? verification = null;
        if (Verification != null)
        {
            verification = OfficeDocument.Create("Compliant", Verification.Compliant, "Discrepancies", Verification.Discrepancies,
                "Unknowns", Verification.Unknowns);
            if (Plan.Request.Action != OfficeDeploymentAction.Remove && Plan.Request.Action != OfficeDeploymentAction.SetUpdateConfiguration && Plan.Request.Action != OfficeDeploymentAction.SetApplicationPreference)
            { verification["SchemaVersion"] = 1; verification["Inventory"] = after; }
        }
        return OfficeDocument.Create("Target", Plan.MachineId, "Source", "Office", "Action", Action, "Status", Status.ToString(),
            "Before", Plan.Data["Before"], "RunId", RunId, "SchemaVersion", 1, "MachineId", Plan.MachineId, "Phase", Phase, "ReasonCode", ReasonCode,
            "Changed", Changed, "ChangeKnown", ChangeKnown, "AlreadyCompliant", AlreadyCompliant, "After", after, "Verification", verification,
            "Activation", Activation.HasValue ? OfficeDocument.Create("TargetProductId", Plan.Data.Object("Configuration").Text("TargetProductId"), "Status", Activation.Value.ToString()) : null,
            "Configuration", Plan.Data["Configuration"], "LanguageTransition", Plan.Data["LanguageTransition"], "RebootRequired", RebootRequired, "ExitCode", ExitCode,
            "NativeResults", Native.Select(item => OfficeDocument.Create("Phase", item.Phase, "ExitCode", item.ExitCode)).ToArray(), "WrapperExitCode", WrapperExitCode,
            "RecoveryRequired", RecoveryRequired, "RecoveryPath", RecoveryPath, "LogPaths", Logs, "Residue", ResidualPaths, "CleanupErrors", Cleanup,
            "Error", Error, "Diagnostic", DiagnosticDocument(Diagnostic), "Execution", ExecutionMetadata, "Plan", Plan.Data);
    }
    internal static OfficeDocument? DiagnosticDocument(object? value)
    {
        if (value is OfficePathDiagnostic path)
            return OfficeDocument.Create("Stage", path.Stage, "ObjectKind", path.ObjectKind, "Path", path.Path, "Sid", path.Sid,
                "Rights", RightsName(path.AccessMask),
                "IsInherited", path.IsInherited, "InheritanceFlags", path.InheritanceFlags?.ToString(), "PropagationFlags", path.PropagationFlags?.ToString());
        if (value is OfficeRecoveryDiagnostic recovery)
            return OfficeDocument.Create("Stage", recovery.Stage, "Category", recovery.Category, "ObjectKind", recovery.ObjectKind, "Path", recovery.Path, "ExceptionType", recovery.ExceptionType);
        return null;
    }
    private static string? RightsName(int? mask)
    {
        if (!mask.HasValue)
            return null;
        var type = Type.GetType("System.Security.AccessControl.FileSystemRights, System.IO.FileSystem.AccessControl")
            ?? Type.GetType("System.Security.AccessControl.FileSystemRights, mscorlib");
        return type == null ? "0x" + unchecked((uint)mask.Value).ToString("X8", System.Globalization.CultureInfo.InvariantCulture) : Enum.ToObject(type, mask.Value).ToString();
    }
}
