using System;
using System.Management.Automation;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsCommon.Search, "RegistryKey")]
public sealed class SearchRegistryKeyCommand : RegistryToolCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Root { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1)] public string Pattern { get; set; } = "";
    [Parameter(Mandatory = true, Position = 2)] public string OutputPath { get; set; } = "";
    protected override void ProcessRecord()
    {
        try
        {
            new RegistryFileService(new RegistryTool(), Text(SessionState.PSVariable.GetValue("PSEdition")) == "Desktop").Search(Root, Pattern, SessionState.Path.GetUnresolvedProviderPathFromPSPath(OutputPath), Cancellation.Token);
            WriteObject(true);
        }
        catch (OperationCanceledException) { throw new PipelineStoppedException(); }
        catch (Exception error) { Failure(error, $"Failed to search registry '{Root}' for pattern '{Pattern}': " + error.Message, $"Failed to search registry '{Root}' for pattern '{Pattern}': "); WriteObject(false); }
    }
}
