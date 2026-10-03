using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsCommon.Get, "RegistryValueKind")]
[OutputType(typeof(RegistryValueKind))]
public sealed class GetRegistryValueKindCommand : RegistryCommand
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
            WriteObject(null);
            return;
        }
        try
        {
            if (!Store.KeyExists(path))
            {
                WriteVerbose($"Registry key not found: '{Path}'");
                WriteObject(null);
                return;
            }
            var state = Store.Read(path, Name, RegistryView.Default);
            if (!state.Exists)
            {
                WriteVerbose($"Value '{Name}' not found in registry key '{Path}'");
                WriteObject(null);
                return;
            }
            WriteObject(state.Kind);
        }
        catch (Exception error) { Failure(error, $"Access denied opening registry key: '{Path}'", $"Failed to get registry value kind for '{Name}' in '{Path}': "); WriteObject(null); }
    }
}
