using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using PSFoundation.Diagnostics;
using PSFoundation.Office;
using PSFoundation.PowerShell.Windows;

namespace PSFoundation.PowerShell.Office;

[Cmdlet(VerbsDiagnostic.Resolve, "OfficeDeploymentToolSource"), OutputType(typeof(PSObject))]
public sealed class ResolveOfficeDeploymentToolSourceCommand : PSCmdlet
{
    protected override void ProcessRecord() => WriteObject(OfficeCompatibility.ToolSource(OdtSource.Reviewed));
}
[Cmdlet(VerbsDiagnostic.Test, "OfficeDeploymentToolSourceAvailability"), OutputType(typeof(PSObject))]
public sealed class TestOfficeDeploymentToolSourceAvailabilityCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        using (var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
        {
            var result = new OdtTool(client).CheckSource(InventoryTimeout, cancellationToken: Cancellation);
            if (!result.Available)
                WriteObject(SystemOutput.Object("Uri", result.Source.DownloadUri.AbsoluteUri, "Version", result.Source.Version.ToString(), "Available", false, "Error", result.Error?.Message, "CheckedAt", result.CheckedAtUtc));
            else
            {
                object? length = result.ContentLength?.ToString(CultureInfo.InvariantCulture);
                if (length != null && string.Equals(SessionState.PSVariable.GetValue("PSEdition") as string, "Core", StringComparison.Ordinal))
                    length = new[] { (string)length };
                WriteObject(SystemOutput.Object("Uri", result.Source.DownloadUri.AbsoluteUri, "Version", result.Source.Version.ToString(), "Available", true,
                    "StatusCode", result.StatusCode, "ContentLength", length, "CheckedAt", result.CheckedAtUtc));
            }
        }
    }
}
[Cmdlet(VerbsDiagnostic.Test, "OfficeDeploymentTool"), OutputType(typeof(PSObject))]
public sealed class TestOfficeDeploymentToolCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string OdtPath { get; set; } = "";
    protected override void ProcessRecord()
    {
        OdtAssessment? result = null;
        string? error = null;
        try
        { result = new OdtTool().Check(new OfficePathGuard().ValidatePath(OdtPath, Cancellation), cancellationToken: Cancellation); }
        catch (OperationCanceledException) { throw; }
        catch (Exception failure) { error = failure.Message; }
        WriteObject(OfficeCompatibility.ToolAssessment(result, OdtPath, error));
    }
}
[Cmdlet(VerbsCommon.Get, "OfficeDeploymentToolHelp", SupportsShouldProcess = true), OutputType(typeof(PSObject))]
public sealed class GetOfficeDeploymentToolHelpCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string OdtPath { get; set; } = "";
    protected override void ProcessRecord()
    {
        if (!ShouldProcess(OdtPath, "Run verified ODT help"))
            return;
        try
        { WriteObject(OfficeCompatibility.ToolResult(new OdtTool().Help(new OfficePathGuard().ValidatePath(OdtPath, Cancellation), Cancellation))); }
        catch (OfficeException error) { ThrowTerminatingError(new ErrorRecord(error, error.Reason.ToString(), ErrorCategory.InvalidOperation, OdtPath)); }
    }
}
[Cmdlet(VerbsLifecycle.Install, "OfficeDeploymentTool", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High), OutputType(typeof(PSObject))]
public sealed class InstallOfficeDeploymentToolCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Destination { get; set; } = "";
    [Parameter] public SwitchParameter DryRun { get; set; }
    protected override void ProcessRecord()
    {
        try
        {
            var destination = new OfficePathGuard().ValidatePath(Destination, Cancellation);
            var setup = destination.Combine("setup.exe");
            var display = Path.Combine(Destination, "setup.exe");
            var exists = false;
            try
            { _ = File.GetAttributes(setup.Value); exists = true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            if (exists)
            {
                var existing = new OdtTool().Check(setup, cancellationToken: Cancellation);
                if (!existing.Valid)
                    throw new OfficeException(OfficeFailureReason.UntrustedTool, "Existing ODT is untrusted; it was not replaced.");
                WriteObject(OfficeCompatibility.ToolAssessment(existing, display));
                return;
            }
            var source = OdtSource.Reviewed;
            if (DryRun || !ShouldProcess(Destination, "Acquire verified ODT " + source.Version))
            { WriteObject(SystemOutput.Object("Status", "Preview", "Path", display, "Source", OfficeCompatibility.ToolSource(source))); return; }
            WriteObject(OfficeCompatibility.ToolAssessment(new OdtTool().Install(destination, TimeSpan.FromMinutes(5), source, Cancellation), display));
        }
        catch (OfficeException error) { ThrowTerminatingError(new ErrorRecord(error, error.Reason.ToString(), ErrorCategory.InvalidOperation, Destination)); }
    }
}
public static partial class OfficeCompatibility
{
    public static PSObject ToolAssessment(OdtAssessment? result, string path, string? error = null)
        => SystemOutput.Object("Valid", result?.Valid ?? false, "Path", path, "Version", result?.Version?.ToString(),
            "ReasonCode", result?.Valid == true ? null : "UntrustedTool", "Detail", result?.Detail ?? error, "SignatureStatus", result?.Signature?.Status.ToString(),
            "OriginalFilename", result?.OriginalFilename, "FileDescription", result?.FileDescription, "ProductName", result?.ProductName, "CompanyName", result?.CompanyName,
            "Modes", Enum.GetNames(typeof(OdtMode)).Cast<object>().ToArray());
    public static PSObject ToolResult(ProcessResult result) => SystemOutput.Object("ExitCode", result.ExitCode, "StdOut", result.StandardOutput,
        "StdErr", result.StandardError, "Duration", result.Duration, "TimedOut", result.TimedOut, "Cancelled", result.Cancelled);
    public static PSObject InvokeTool(string executable, string mode, string? configuration)
    {
        PSObject? result = null;
        var failure = PathFailure(() =>
        {
            var guard = new OfficePathGuard();
            var operation = (OdtMode)Enum.Parse(typeof(OdtMode), mode.TrimStart('/'), true);
            result = ToolResult(new OdtTool().Invoke(guard.ValidatePath(executable), operation,
                operation == OdtMode.Help ? null : guard.ValidatePath(configuration ?? "")));
        });
        return SystemOutput.Object("Result", result, "Failure", failure);
    }
    public static PSObject? RemoveDeploymentDirectory(string path, string parent) => PathFailure(() => new OfficePathGuard().RemoveWorkDirectory(path, parent));
    public static PSObject ToolSource(OdtSource source) => SystemOutput.Object("Uri", source.DownloadUri.AbsoluteUri, "Version", source.Version.ToString(),
        "Publisher", source.Publisher, "Reference", source.Reference.AbsoluteUri, "ReviewedDate", source.ReviewedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    public static PSObject? CheckDeploymentPath(string path, bool requireProtected, string objectKind)
        => PathFailure(() => { if (requireProtected) new OfficePathGuard().RequireProtected(path, objectKind); else new OfficePathGuard().ValidatePath(path); });
    public static PSObject? CreateDeploymentDirectory(string path) => PathFailure(() => new OfficePathGuard().EnsureProtectedDirectory(path));
    private static PSObject? PathFailure(Action operation)
    {
        try
        { operation(); return null; }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            var reason = error is OfficeException office ? office.Reason.ToString() : "UntrustedMedia";
            PSObject? diagnostic = null;
            if (error.Data["OfficeDiagnostic"] is OfficePathDiagnostic detail)
            {
                var type = new PSTypeName("System.Security.AccessControl.FileSystemRights").Type;
                var rights = detail.AccessMask.HasValue ? type == null ? "0x" + unchecked((uint)detail.AccessMask.Value).ToString("X8", CultureInfo.InvariantCulture)
                    : Enum.ToObject(type, detail.AccessMask.Value).ToString() : null;
                diagnostic = SystemOutput.Object("Stage", detail.Stage, "ObjectKind", detail.ObjectKind, "Path", detail.Path, "Sid", detail.Sid, "Rights", rights,
                    "IsInherited", detail.IsInherited, "InheritanceFlags", detail.InheritanceFlags?.ToString(), "PropagationFlags", detail.PropagationFlags?.ToString());
            }
            return SystemOutput.Object("ReasonCode", reason, "Detail", error.Message, "Diagnostic", diagnostic);
        }
    }
}
