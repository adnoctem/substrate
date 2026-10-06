using System;
using System.Management.Automation;
using AdNoctem.Substrate.Registry;
using AdNoctem.Substrate.Registry.Compatibility;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(VerbsCommon.Search, "RegistryKey")]
public sealed class SearchRegistryKeyCommand : RegistryProcessCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Root { get; set; } = "";

    [Parameter(Mandatory = true, Position = 1)]
    public string Pattern { get; set; } = "";

    [Parameter(Mandatory = true, Position = 2)]
    public string OutputPath { get; set; } = "";

    protected override void ProcessRecord()
    {
        try
        {
            new LegacyRegistryFileService(
                new RegistryCommandRunner(),
                Text(SessionState.PSVariable.GetValue("PSEdition")) == "Desktop"
            ).Search(
                Root,
                Pattern,
                SessionState.Path.GetUnresolvedProviderPathFromPSPath(OutputPath),
                Cancellation.Token
            );
            WriteObject(true);
        }
        catch (OperationCanceledException)
        {
            throw new PipelineStoppedException();
        }
        catch (Exception error)
        {
            Failure(
                error,
                $"Failed to search registry '{Root}' for pattern '{Pattern}': " + error.Message,
                $"Failed to search registry '{Root}' for pattern '{Pattern}': "
            );
            WriteObject(false);
        }
    }
}
