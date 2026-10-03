using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsCommon.Get, "RegistryKey")]
[OutputType(typeof(PSObject))]
public sealed class GetRegistryKeyCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Path { get; set; } = "";

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
            var subKeys = (WildcardPattern.ContainsWildcardCharacters(path.ProviderPath)
                ? paths.Select(p => p.SubKey.Substring(p.SubKey.LastIndexOf('\\') + 1))
                : paths.SelectMany(Store.SubKeys)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray();
            var values = paths.SelectMany(Store.ValueNames).Where(x => !new[] { "PSPath", "PSParentPath", "PSChildName", "PSDrive", "PSProvider" }.Contains(x, StringComparer.OrdinalIgnoreCase)).Select(x => x == "(default)" ? "" : x).Cast<object>().ToArray();
            WriteObject(Shape("Path", Path, "SubKeys", subKeys, "Values", values));
        }
        catch (Exception error) { Failure(error, $"Access denied reading registry key: '{Path}'", $"Failed to read registry key '{Path}': "); WriteObject(null); }
    }
}
