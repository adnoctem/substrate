using AdNoctem.Substrate.Registry.Compatibility;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(VerbsCommon.Remove, "RegistryValue", SupportsShouldProcess = true)]
[OutputType(typeof(PSObject))]
public sealed class RemoveRegistryValueCommand : RegistryCommand
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
            var paths = Expand(path);
            var pattern = WildcardPattern.ContainsWildcardCharacters(Name) ? new WildcardPattern(Name, WildcardOptions.IgnoreCase) : null;
            var status = new RegistryOperations(Store).RemoveValue(Path, paths, Name, (target, action) => ShouldProcess(target, action), WriteVerbose,
                pattern == null ? null : (Func<string, bool>)pattern.IsMatch);
            Status(Path, Name, status);
        }
        catch (Exception error) { Failure(error, $"Access denied removing registry value '{Name}' from '{Path}'", $"Failed to remove registry value '{Name}' from '{Path}': "); WriteObject(null); }
    }
}
