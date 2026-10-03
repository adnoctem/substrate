using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsCommon.Get, "RegistryValue")]
[OutputType(typeof(object))]
public sealed class GetRegistryValueCommand : RegistryCommand
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
        var paths = Expand(path).Where(Store.KeyExists).ToArray();
        if (paths.Length == 0)
        {
            Infrastructure.LegacyError.Write(this, $"Registry key not found: '{Path}'");
            WriteObject(null);
            return;
        }
        try
        {
            var valueName = string.Equals(Name, "(default)", StringComparison.OrdinalIgnoreCase) ? "" : Name;
            // A missing property on any resolved key terminates the provider read before output.
            if (paths.Any(p => !ValueExists(p, valueName)))
            {
                WriteVerbose($"Value '{Name}' not found in registry key '{Path}'");
                WriteObject(null);
                return;
            }
            if (WildcardPattern.ContainsWildcardCharacters(valueName))
                return;
            var found = false;
            foreach (var resolved in paths)
            {
                var name = string.Equals(Name, "(default)", StringComparison.OrdinalIgnoreCase) ? "" : Name;
                if (!Store.Read(resolved, name, RegistryView.Default).Exists)
                    continue;
                found = true;
                WriteObject(Store.GetValue(resolved, name));
            }
            if (!found)
            {
                WriteVerbose($"Value '{Name}' not found in registry key '{Path}'");
                WriteObject(null);
            }
        }
        catch (UnauthorizedAccessException error) { Failure(error, $"Access denied reading registry key: '{Path}'", ""); WriteObject(null); }
        catch (Exception error) { if (error is PipelineStoppedException || error is ActionPreferenceStopException) throw; WriteVerbose($"Value '{Name}' not found in registry key '{Path}'"); WriteObject(null); }
    }
}
