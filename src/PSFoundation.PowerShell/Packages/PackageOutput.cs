using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using PSFoundation.Diagnostics;
using PSFoundation.PowerShell.Infrastructure;
using PSFoundation.PowerShell.Windows;

namespace PSFoundation.PowerShell.Packages;

internal static class PackageOutput
{
    internal static PSObject Result(string target, string source, string action, string status, string skipped = "", string error = "", ErrorRecord? record = null,
        string? domain = null, IDictionary<string, object?>? fields = null, Hashtable? properties = null, bool suppliedError = false)
    {
        var values = fields == null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(fields);
        values["SkippedReason"] = skipped;
        values["ErrorMessage"] = record != null && !suppliedError ? record.Exception.Message : error;
        var metadata = properties == null ? new Hashtable() : new Hashtable(properties);
        if (record != null)
            metadata["ErrorRecord"] = record;
        if (string.IsNullOrEmpty(domain))
            domain = source == "WinGet" ? "Winget" : source == "UPFAppxPackage" || source == "Installed" || source == "Provisioned" ? "Appx" : null;
        if (domain != null)
        {
            var errorDomain = (ErrorDomain)Enum.Parse(typeof(ErrorDomain), domain, true);
            var translator = new ErrorTranslator();
            var translation = record != null ? translator.Translate(record.Exception, errorDomain, record.ErrorDetails?.Message)
                : values.TryGetValue("ExitCode", out var exitCode) ? translator.Translate(unchecked((uint)Convert.ToInt32(exitCode)), errorDomain) : null;
            if (translation != null)
                metadata["ErrorTranslation"] = SystemOutput.Object("Code", translation.DisplayCode, "Domain", translation.Domain.ToString(), "Benign", translation.Benign, "Detail", translation.Detail, "Matched", true);
        }
        return OperationOutput.Create(target, source, action, status, values, metadata);
    }
    internal static PSObject Failure(string target, string source, string action, Exception error)
        => Result(target, source, action, "Failed", record: new ErrorRecord(error, "PackageOperationFailed", ErrorCategory.OperationStopped, target));
}
[Cmdlet(VerbsCommon.New, "PackageLifecycleResult"), OutputType(typeof(PSObject))]
public sealed class NewPackageLifecycleResultCommand : PSCmdlet
{
    [Parameter(Position = 0)] public string Target { get; set; } = "";
    [Parameter(Position = 1)] public string Source { get; set; } = "";
    [Parameter(Position = 2)] public string Action { get; set; } = "";
    [Parameter(Position = 3)] public string Status { get; set; } = "";
    [Parameter(Position = 4)] public string SkippedReason { get; set; } = "";
    [Parameter(Position = 5)] public string ErrorMessage { get; set; } = "";
    [Parameter(Position = 6)] public ErrorRecord? ErrorRecord { get; set; }
    [Parameter(Position = 7), ValidateSet("Appx", "Winget", "Dism", "Msi")] public string? ErrorDomain { get; set; }
    [Parameter(Position = 8)] public bool Changed { get; set; }
    [Parameter(Position = 9)] public bool AlreadyCompliant { get; set; }
    [Parameter(Position = 10), AllowNull] public object? Before { get; set; }
    [Parameter(Position = 11), AllowNull] public object? After { get; set; }
    [Parameter(Position = 12)] public int ExitCode { get; set; }
    [Parameter(Position = 13)] public bool RebootRequired { get; set; }
    [Parameter(Position = 14)] public TimeSpan Duration { get; set; }
    [Parameter(Position = 15)] public string RunId { get; set; } = "";
    [Parameter(Position = 16)] public Hashtable? Property { get; set; }
    protected override void ProcessRecord()
    {
        var fields = new Dictionary<string, object?>();
        foreach (var name in new[] { "Changed", "AlreadyCompliant", "Before", "After", "ExitCode", "RebootRequired", "Duration", "RunId" })
            if (MyInvocation.BoundParameters.TryGetValue(name, out var value))
                fields[name] = value;
        WriteObject(PackageOutput.Result(Target, Source, Action, Status, SkippedReason, ErrorMessage, ErrorRecord, ErrorDomain, fields, Property, MyInvocation.BoundParameters.ContainsKey(nameof(ErrorMessage))));
    }
}
