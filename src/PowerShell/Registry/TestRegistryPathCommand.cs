using AdNoctem.Substrate.Registry.Compatibility;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(VerbsDiagnostic.Test, "RegistryPath")]
[OutputType(typeof(bool))]
public sealed class TestRegistryPathCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Path { get; set; } = "";

    protected override void ProcessRecord()
    {
        var path = Parse(Path);
        WriteObject(path != null && Expand(path).Any(Store.KeyExists));
    }
}
