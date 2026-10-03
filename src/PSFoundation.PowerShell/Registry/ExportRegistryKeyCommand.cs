using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Management.Automation;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsData.Export, "RegistryKey")]
public sealed class ExportRegistryKeyCommand : RegistryProcessCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Key { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1)] public string OutputPath { get; set; } = "";
    protected override void ProcessRecord()
    {
        try
        {
            new LegacyRegistryFileService(new RegistryCommandRunner()).Export(Key, SessionState.Path.GetUnresolvedProviderPathFromPSPath(OutputPath), Cancellation.Token);
            WriteObject(true);
        }
        catch (OperationCanceledException) { throw new PipelineStoppedException(); }
        catch (Exception error) { Failure(error, $"Failed to export registry key '{Key}': " + error.Message, $"Failed to export registry key '{Key}': "); WriteObject(false); }
    }
}
