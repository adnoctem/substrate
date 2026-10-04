using System;
using System.Management.Automation;
using System.Threading;
using PSFoundation.Diagnostics;
using PSFoundation.IO;
using PSFoundation.Policies;

namespace PSFoundation.PowerShell.Policies;

[Cmdlet(VerbsDiagnostic.Resolve, "LGPOSource")]
[OutputType(typeof(PSObject))]
public sealed class ResolveLgpoSourceCommand : PSCmdlet
{
    [Parameter(Position = 0), ValidateSet("SCT-LGPO-Standalone")] public string Source { get; set; } = "SCT-LGPO-Standalone";
    protected override void ProcessRecord()
    {
        var source = LgpoSource.Standalone;
        var value = new PSObject();
        value.Properties.Add(new PSNoteProperty("Name", source.Name));
        value.Properties.Add(new PSNoteProperty("Url", source.DownloadUri.AbsoluteUri));
        value.Properties.Add(new PSNoteProperty("Sha256", source.TrustedSha256));
        value.Properties.Add(new PSNoteProperty("ExpectedBinaryPath", source.ExpectedBinaryPath));
        value.Properties.Add(new PSNoteProperty("LastVerified", source.VerifiedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        WriteObject(value);
    }
}

/// <summary>Result mapping for the wrapper that retains v1 ValidateScript behavior.</summary>
public static class LgpoCompatibility
{
    public static ErrorRecord LaunchError(Exception exception)
    {
        while (!(exception is System.ComponentModel.Win32Exception) && exception.InnerException != null)
            exception = exception.InnerException;
        var native = (System.ComponentModel.Win32Exception)exception;
        var detail = new System.ComponentModel.Win32Exception(native.NativeErrorCode).Message.TrimEnd('.');
        var error = new InvalidOperationException("This command cannot be run due to the error: " + detail + ".", native);
        return new ErrorRecord(error, "InvalidOperationException", ErrorCategory.InvalidOperation, null);
    }

    public static PSObject Apply(string executable, string resolvedPolicyPath, string originalPolicyPath, bool desktop)
    {
        var tool = new LgpoTool();
        var policy = FileSystemPath.Parse(resolvedPolicyPath);
        var mode = tool.GetApplyMode(policy);
        var result = tool.Apply(FileSystemPath.Parse(executable), policy, Timeout.InfiniteTimeSpan, ProcessStopBehavior.WaitForExit);
        var value = new PSObject();
        value.Properties.Add(new PSNoteProperty("PolicyPath", originalPolicyPath));
        value.Properties.Add(new PSNoteProperty("Mode", mode.ToString()));
        value.Properties.Add(new PSNoteProperty("ExitCode", result.ExitCode));
        object? EmptyOutput() => desktop ? System.Management.Automation.Internal.AutomationNull.Value : null;
        value.Properties.Add(new PSNoteProperty("StdOut", result.StandardOutput.Length == 0 ? EmptyOutput() : result.StandardOutput));
        value.Properties.Add(new PSNoteProperty("StdErr", result.StandardError.Length == 0 ? EmptyOutput() : result.StandardError));
        value.Properties.Add(new PSNoteProperty("AppliedAt", DateTime.UtcNow));
        value.Properties.Add(new PSNoteProperty("Success", result.ExitCode == 0));
        return value;
    }
}
