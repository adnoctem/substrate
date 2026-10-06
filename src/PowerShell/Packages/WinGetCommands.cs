using System;
using System.Collections.Generic;
using System.Management.Automation;
using AdNoctem.Substrate.Packages;
using AdNoctem.Substrate.PowerShell.Windows;

namespace AdNoctem.Substrate.PowerShell.Packages;

public abstract class WinGetCommand : SystemCommand
{
    protected void Execute(
        WinGetAction action,
        WinGetSelection selection,
        string target,
        string? version,
        string? source,
        bool unknown,
        bool dryRun
    )
    {
        var verb = action.ToString();

        if (dryRun || !ShouldProcess(target, verb + " WinGet package"))
        {
            WriteObject(PackageOutput.Result(target, "WinGet", verb, "Skipped", "WhatIf"));

            return;
        }

        var executable = WinGetTool.FindInstalled();

        if (executable == null)
        {
            WriteObject(
                PackageOutput.Result(target, "WinGet", verb, "Skipped", "WinGetUnavailable")
            );

            return;
        }

        try
        {
            var result = new WinGetTool(executable).Run(
                new WinGetRequest(
                    action,
                    selection,
                    target,
                    string.IsNullOrEmpty(version) ? null : version,
                    string.IsNullOrEmpty(source) ? null : source,
                    unknown,
                    true
                ),
                Cancellation
            );
            WriteObject(
                PackageOutput.Result(
                    target,
                    "WinGet",
                    verb,
                    result.ExitCode == 0 ? "Completed" : "Failed",
                    error: result.ExitCode == 0
                        ? ""
                        : "WinGet exited with code " + result.ExitCode + ".",
                    fields: new Dictionary<string, object?>
                    {
                        ["ExitCode"] = result.ExitCode,
                        ["Duration"] = result.Duration,
                    }
                )
            );
        }
        catch (Exception error) when (!(error is OperationCanceledException))
        {
            WriteObject(PackageOutput.Failure(target, "WinGet", verb, error));
        }
    }
}

[
    Cmdlet(VerbsLifecycle.Install, "Win32ProgramFromWinGet", SupportsShouldProcess = true),
    OutputType(typeof(PSObject))
]
public sealed class InstallWin32ProgramFromWinGetCommand : WinGetCommand
{
    [Parameter(Mandatory = true, ParameterSetName = "Name")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = "Id")]
    public string Id { get; set; } = "";

    [Parameter]
    public string Version { get; set; } = "";

    [Parameter]
    public string Source { get; set; } = "";

    [Parameter]
    public SwitchParameter DryRun { get; set; }

    protected override void ProcessRecord() =>
        Execute(
            WinGetAction.Install,
            ParameterSetName == "Id" ? WinGetSelection.Id : WinGetSelection.Name,
            ParameterSetName == "Id" ? Id : Name,
            Version,
            Source,
            false,
            DryRun
        );
}

[
    Cmdlet(VerbsData.Update, "Win32ProgramFromWinGet", SupportsShouldProcess = true),
    OutputType(typeof(PSObject))
]
public sealed class UpdateWin32ProgramFromWinGetCommand : WinGetCommand
{
    [Parameter(ParameterSetName = "Id")]
    public string Id { get; set; } = "";

    [Parameter(ParameterSetName = "Name")]
    public string Name { get; set; } = "";

    [Parameter(ParameterSetName = "All")]
    public SwitchParameter All { get; set; }

    [Parameter]
    public SwitchParameter IncludeUnknown { get; set; }

    [Parameter]
    public string Source { get; set; } = "";

    [Parameter]
    public SwitchParameter DryRun { get; set; }

    protected override void ProcessRecord() =>
        Execute(
            WinGetAction.Update,
            Id.Length != 0 ? WinGetSelection.Id
                : Name.Length != 0 ? WinGetSelection.Name
                : WinGetSelection.All,
            Id.Length != 0 ? Id
                : Name.Length != 0 ? Name
                : "All",
            null,
            Source,
            IncludeUnknown,
            DryRun
        );
}

[
    Cmdlet(
        VerbsLifecycle.Uninstall,
        "Win32ProgramFromWinGet",
        SupportsShouldProcess = true,
        ConfirmImpact = ConfirmImpact.High
    ),
    OutputType(typeof(PSObject))
]
public sealed class UninstallWin32ProgramFromWinGetCommand : WinGetCommand
{
    [Parameter(Mandatory = true, ParameterSetName = "Name")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = "Id")]
    public string Id { get; set; } = "";

    [Parameter]
    public SwitchParameter DryRun { get; set; }

    protected override void ProcessRecord() =>
        Execute(
            WinGetAction.Uninstall,
            ParameterSetName == "Id" ? WinGetSelection.Id : WinGetSelection.Name,
            ParameterSetName == "Id" ? Id : Name,
            null,
            null,
            false,
            DryRun
        );
}
