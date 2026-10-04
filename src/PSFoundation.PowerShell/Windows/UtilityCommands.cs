using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using PSFoundation.IO;
using PSFoundation.PowerShell.Infrastructure;
using PSFoundation.Windows;

namespace PSFoundation.PowerShell.Windows;

[Cmdlet(VerbsDiagnostic.Test, "SystemFileIntegrity"), OutputType(typeof(PSObject))]
public sealed class TestSystemFileIntegrityCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var result = new SystemIntegrityManager().Verify(Cancellation);
        WriteObject(SystemOutput.Object("Status", result.Status == SystemIntegrityStatus.Clean ? "Completed" : "Failed", "Detail", result.Detail, "ExitCode", result.ExitCode, "LogExcerpt", result.LogExcerpt));
    }
}
[Cmdlet(VerbsLifecycle.Install, "Font", SupportsShouldProcess = true)]
public sealed class InstallFontCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string FontSourceFolder { get; set; } = "";
    [Parameter] public SwitchParameter RemoveSource { get; set; }
    protected override void ProcessRecord()
    {
        var path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(FontSourceFolder);
        var manager = new FontManager();
        var files = Directory.Exists(path) ? manager.FindFiles(FileSystemPath.Parse(path)) : Array.Empty<FileSystemPath>();
        if (files.Count == 0)
        { WriteObject(OperationOutput.Create(FontSourceFolder, "Font", "Install", "Skipped", new Dictionary<string, object?> { ["Detail"] = "No font files (.ttf/.ttc/.otf) found in the source folder." })); return; }
        foreach (var file in files)
        {
            Cancellation.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file.Value);
            var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", name);
            if (!ShouldProcess(name, "Install font to " + target))
            { WriteObject(OperationOutput.Create(name, "Font", "Install", "Skipped", new Dictionary<string, object?> { ["Detail"] = "WhatIf" })); continue; }
            try
            {
                var display = manager.Install(file, RemoveSource, Cancellation);
                WriteObject(OperationOutput.Create(name, "Font", "Install", "Completed", new Dictionary<string, object?> { ["Detail"] = "Registered as '" + display + "' in " + target + "." }));
            }
            catch (Exception error) when (!(error is OperationCanceledException)) { WriteObject(OperationOutput.Create(name, "Font", "Install", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message })); }
        }
    }
}
