using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSFoundation.Diagnostics;
using PSFoundation.IO;
using PSFoundation.Packages;
using PSFoundation.PowerShell.Infrastructure;
using PSFoundation.PowerShell.Windows;

namespace PSFoundation.PowerShell.Packages;

public static class ProgramCompatibility
{
    internal static PSObject Result(string target, string action, string status, string skipped = "", string error = "",
        IDictionary<string, object?>? fields = null, Hashtable? properties = null)
    {
        var values = fields ?? new Dictionary<string, object?>();
        values["SkippedReason"] = skipped;
        values["ErrorMessage"] = error;
        return OperationOutput.Create(target, "Win32Program", action, status, values, properties);
    }
    public static PSObject SkipInstall(string path, string? runId, bool includeRunId)
    {
        var fields = new Dictionary<string, object?> { ["Changed"] = false };
        if (includeRunId)
            fields["RunId"] = runId ?? "";
        return Result(path, "Install", "Skipped", "WhatIf", fields: fields);
    }
    public static PSObject Install(string path, string[]? arguments, bool noWait, bool passThru, int[] successCodes, int[] rebootCodes,
        string? runId, bool includeRunId)
    {
        var clock = Stopwatch.StartNew();
        var fields = new Dictionary<string, object?>();
        if (includeRunId)
            fields["RunId"] = runId ?? "";
        try
        {
            var manager = new Win32ProgramManager();
            var request = manager.CreateInstallRequest(FileSystemPath.Parse(path), arguments, successExitCodes: successCodes, rebootExitCodes: rebootCodes);
            if (noWait)
            {
                var id = manager.Start(request);
                fields["Duration"] = clock.Elapsed;
                return Result(path, "Install", "Started", fields: fields, properties: new Hashtable { ["ProcessId"] = id });
            }
            var result = manager.Execute(request);
            fields["Duration"] = clock.Elapsed;
            fields["ExitCode"] = result.Process.ExitCode;
            fields["RebootRequired"] = result.RebootRequired;
            var properties = new Hashtable { ["Succeeded"] = result.Succeeded };
            if (string.Equals(Path.GetExtension(path), ".msi", StringComparison.OrdinalIgnoreCase))
            {
                var translation = new ErrorTranslator().Translate(unchecked((uint)result.Process.ExitCode), ErrorDomain.Msi);
                if (translation != null)
                    properties["ErrorTranslation"] = SystemOutput.Object("Code", translation.DisplayCode,
                    "Domain", "Msi", "Benign", translation.Benign, "Detail", translation.Detail, "Matched", true);
            }
            return Result(path, "Install", passThru ? "ExitCode:" + result.Process.ExitCode : result.Succeeded ? "Installed" : "Failed",
                error: result.Succeeded ? "" : "Installer exited with code " + result.Process.ExitCode + ".", fields: fields, properties: properties);
        }
        catch (Exception error)
        {
            fields["Duration"] = clock.Elapsed;
            return Result(path, "Install", "Failed", error: error.Message, fields: fields,
                properties: new Hashtable { ["ErrorRecord"] = new ErrorRecord(error, "ProgramExecutionFailed", ErrorCategory.OperationStopped, path) });
        }
    }
}

[Cmdlet(VerbsLifecycle.Uninstall, "Win32Program", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High), OutputType(typeof(PSObject))]
public sealed class UninstallWin32ProgramCommand : ProgramInventoryCommand
{
    [Parameter(Mandatory = true, ParameterSetName = "Name")] public string Name { get; set; } = "";
    [Parameter(Mandatory = true, ParameterSetName = "InputObject", ValueFromPipeline = true)] public PSObject InputObject { get; set; } = null!;
    [Parameter] public SwitchParameter Quiet { get; set; }
    [Parameter] public SwitchParameter Force { get; set; }
    [Parameter] public SwitchParameter DryRun { get; set; }
    private readonly List<PSObject> targets = new List<PSObject>();
    protected override void ProcessRecord() { if (ParameterSetName == "InputObject") targets.Add(InputObject); }
    protected override void EndProcessing()
    {
        try
        {
            if (ParameterSetName == "Name")
                targets.AddRange(ProgramOutput.List(Name, false, Cancellation));
            if (targets.Count == 0)
            { WriteObject(ProgramCompatibility.Result(Name, "Uninstall", "Skipped", "NoMatch")); return; }
            var manager = new Win32ProgramManager();
            foreach (var program in targets)
            {
                Cancellation.ThrowIfCancellationRequested();
                string Read(string name) => LanguagePrimitives.ConvertTo<string>(program.Properties[name]?.Value) ?? "";
                var target = Read("DisplayName");
                var selection = manager.SelectUninstallCommand(Read("UninstallString"), Read("QuietUninstallString"), Quiet, Force);
                if (selection.Status != UninstallCommandStatus.Ready)
                { WriteObject(ProgramCompatibility.Result(target, "Uninstall", "Skipped", selection.Status == UninstallCommandStatus.MissingCommand ? "NoUninstallString" : "ForceRequired")); continue; }
                if (DryRun || !ShouldProcess(target, "Run uninstall command: " + selection.Command))
                { WriteObject(ProgramCompatibility.Result(target, "Uninstall", "Skipped", "WhatIf")); continue; }
                try
                {
                    var result = manager.Execute(manager.CreateUninstallRequest(selection.Command!), Cancellation);
                    WriteObject(ProgramCompatibility.Result(target, "Uninstall", "ExitCode:" + result.Process.ExitCode));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error)
                {
                    WriteObject(ProgramCompatibility.Result(target, "Uninstall", "Failed", error: error.Message,
                    properties: new Hashtable { ["ErrorRecord"] = new ErrorRecord(error, "ProgramExecutionFailed", ErrorCategory.OperationStopped, target) }));
                }
            }
        }
        finally { Dispose(); }
    }
}
