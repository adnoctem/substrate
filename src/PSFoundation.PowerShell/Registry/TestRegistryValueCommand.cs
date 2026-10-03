using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsDiagnostic.Test, "RegistryValue")]
[OutputType(typeof(bool))]
public sealed class TestRegistryValueCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Path { get; set; } = "";
    [Parameter(Position = 1)]
    public string Name
    {
        get => name; set => name = value ?? "";
    }
    private string name = "";

    protected override void ProcessRecord()
    {
        var path = Parse(Path);
        if (path == null)
        {
            WriteObject(false);
            return;
        }
        try
        {
            var paths = Expand(path);
            WriteObject(paths.Length > 0 && paths.All(p => Store.KeyExists(p) && ValueExists(p, Name == "(default)" ? "" : Name)));
        }
        catch (Exception error) { if (error is PipelineStoppedException || error is ActionPreferenceStopException) throw; WriteObject(false); }
    }
}
