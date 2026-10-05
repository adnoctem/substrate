using AdNoctem.Substrate.Registry.Compatibility;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Management.Automation;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(VerbsData.ConvertTo, "RegistryProviderPath")]
[OutputType(typeof(string))]
public sealed class ConvertToRegistryProviderPathCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = "";

    protected override void ProcessRecord()
    {
        RegistryPath parsed;
        try
        {
            parsed = RegistryPath.Parse(Path);
        }
        catch (ArgumentException)
        {
            Infrastructure.LegacyError.Write(this, $"Unable to resolve registry hive from path: '{Path}'");
            WriteObject(null);
            return;
        }
        WriteObject(parsed.ProviderPath);
    }
}
