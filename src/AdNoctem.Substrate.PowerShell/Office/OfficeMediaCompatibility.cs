using System;
using System.Linq;
using System.Management.Automation;
using System.Security;
using System.Xml;
using AdNoctem.Substrate.Office;
using AdNoctem.Substrate.PowerShell.Windows;

namespace AdNoctem.Substrate.PowerShell.Office;

public static partial class OfficeCompatibility
{
    public static PSObject InvokeConfiguration(string executable, XmlDocument document, string directory, string mode, SecureString? productKey)
    {
        PSObject? result = null;
        var failure = PathFailure(() =>
        {
            var guard = new OfficePathGuard();
            result = ToolResult(new OdtTool().InvokeConfiguration(guard.ValidatePath(executable), document, guard.ValidatePath(directory),
                (OdtMode)Enum.Parse(typeof(OdtMode), mode.TrimStart('/'), true), productKey));
        });
        return SystemOutput.Object("Result", result, "Failure", failure);
    }
    public static PSObject PrepareMedia(object configuration, string destination, string executable)
    {
        PSObject? result = null;
        var failure = PathFailure(() =>
        {
            var version = Text(Get(configuration, "Version"));
            var target = Target(Text(Get(configuration, "TargetProductId"))!, Text(Get(configuration, "Architecture"))!, Text(Get(configuration, "Channel")),
                Strings(Get(configuration, "Language"))!, version, Strings(Get(configuration, "ExcludeApp"))!);
            var guard = new OfficePathGuard();
            result = MediaAssessment(new OfficeMediaManager().Prepare(target, guard.ValidatePath(destination), guard.ValidatePath(executable),
                exactVersionText: string.IsNullOrEmpty(version) ? null : version), destination);
        });
        return SystemOutput.Object("Result", result, "Failure", failure);
    }
    public static PSObject MediaFiles(string path)
    {
        object[]? files = null;
        var failure = PathFailure(() => files = new OfficeMediaManager().ReadFiles(new OfficePathGuard().ValidatePath(path)).Select(file =>
            (object)SystemOutput.Object("Path", file.Path, "Length", file.Length, "Hash", file.Hash)).ToArray());
        return SystemOutput.Object("Files", files, "Failure", failure);
    }
    public static PSObject InspectMedia(string path, object? configuration)
    {
        var version = configuration == null ? null : Text(Get(configuration, "Version"));
        var target = configuration == null ? null : Target(Text(Get(configuration, "TargetProductId"))!, Text(Get(configuration, "Architecture"))!, Text(Get(configuration, "Channel")),
            Strings(Get(configuration, "Language"))!, version, Strings(Get(configuration, "ExcludeApp"))!);
        var assessment = new OfficeMediaManager().Inspect(new OfficePathGuard().ValidatePath(path), target, exactVersionText: string.IsNullOrEmpty(version) ? null : version);
        return MediaAssessment(assessment, path);
    }
    private static PSObject MediaAssessment(OfficeMediaAssessment assessment, string path)
    {
        var error = assessment.Error;
        PSObject? diagnostic = error == null ? null : PathDiagnostic(error) ?? SystemOutput.Object("Stage", assessment.Stage, "ObjectKind", "MediaPackage", "Path", path,
            "ExceptionType", error.GetType().FullName, "Function", "Test-OfficeDeploymentMedia", "ScriptPath", null, "Line", null);
        var message = error == null ? null : error is OfficeException ? error.Message : "Office media validation failed at " + assessment.Stage + " for '" + path + "'.";
        if (assessment.Reason == OfficeFailureReason.InvalidContract)
            message = "Office media contract is invalid at '" + path + "'.";
        return SystemOutput.Object("Valid", assessment.Valid, "Path", path, "ManifestJson", assessment.ManifestJson, "ReasonCode", assessment.Reason?.ToString(), "Error", message, "Diagnostic", diagnostic);
    }
}
