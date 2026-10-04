using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PSFoundation.IO;
using PSFoundation.PowerShell.Infrastructure;
using PSFoundation.Windows;

namespace PSFoundation.PowerShell.Windows;

[Cmdlet(VerbsDiagnostic.Test, "PendingReboot"), OutputType(typeof(PSObject))]
public sealed class TestPendingRebootCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var status = new RebootManager().GetStatus(InventoryTimeout, Cancellation);
        foreach (var probe in status.Probes.Where(probe => probe.Error != null))
            WriteVerbose(probe.Indicator + ": " + probe.Error!.Message);
        WriteObject(SystemOutput.Object("PendingReboot", status.IsPending,
            "Indicators", status.Probes.Where(probe => probe.Status == RebootProbeStatus.Pending).Select(probe => probe.Indicator.ToString()).ToArray()));
    }
}
[Cmdlet(VerbsCommon.Get, "FileLockProcess"), OutputType(typeof(PSObject))]
public sealed class GetFileLockProcessCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)] public string[] FilePath { get; set; } = Array.Empty<string>();
    protected override void ProcessRecord()
    {
        foreach (var path in FilePath)
        {
            string resolved;
            try
            {
                ProviderInfo provider;
                var paths = SessionState.Path.GetResolvedProviderPathFromPSPath(WildcardPattern.Escape(path), out provider);
                if (provider.Name != "FileSystem" || paths.Count != 1)
                    throw new ArgumentException("Use one filesystem path.");
                resolved = paths[0];
            }
            catch (Exception error) when (error is ItemNotFoundException || error is DriveNotFoundException || error is ProviderNotFoundException || error is ArgumentException)
            { WriteObject(OperationOutput.Create(path, "RestartManager", "FindLockers", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = "Path does not exist: " + path })); continue; }
            try
            {
                var lockers = new FileLockManager().GetLockingProcesses(FileSystemPath.Parse(resolved), Cancellation).Select(locker =>
                    SystemOutput.Object("ProcessId", locker.ProcessId, "ProcessName", locker.ProcessName, "AppName", locker.ApplicationName,
                        "SessionId", locker.SessionId, "ApplicationType", TypeName(locker.ApplicationType), "Restartable", locker.Restartable)).Cast<object>().ToArray();
                WriteObject(OperationOutput.Create(resolved, "RestartManager", "FindLockers", "Completed",
                    new Dictionary<string, object?> { ["Detail"] = lockers.Length + " process(es) hold this file open." }, new Hashtable { ["Lockers"] = lockers }));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { WriteObject(OperationOutput.Create(resolved, "RestartManager", "FindLockers", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message })); }
        }
    }
    private static string TypeName(LockingApplicationType type) => type == LockingApplicationType.Unknown ? "RmUnknownApp"
        : Enum.IsDefined(typeof(LockingApplicationType), type) ? "Rm" + type : "Unknown(" + (int)type + ")";
}
