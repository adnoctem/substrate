using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

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
