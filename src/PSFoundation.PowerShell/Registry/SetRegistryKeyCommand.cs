using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsCommon.Set, "RegistryKey", SupportsShouldProcess = true)]
[OutputType(typeof(PSObject))]
public sealed class SetRegistryKeyCommand : RegistryCommand
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
        if (path.SubKey.Length == 0)
        {
            Infrastructure.LegacyError.Write(this, $"Cannot create a root hive key: '{Path}'");
            WriteObject(null);
            return;
        }
        try
        {
            var resolved = Expand(path).FirstOrDefault(Store.KeyExists) ?? path;
            Status(Path, null, new RegistryOperations(Store).Create(Path, resolved, (target, action) => ShouldProcess(target, action), WriteVerbose));
        }
        catch (Exception error) { Failure(error, $"Access denied creating registry key: '{Path}'", $"Failed to create registry key '{Path}': "); WriteObject(null); }
    }
}
