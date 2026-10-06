using System;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Registry;
using AdNoctem.Substrate.Registry.Compatibility;
using Microsoft.Win32;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(VerbsCommon.Remove, "RegistryKey", SupportsShouldProcess = true)]
[OutputType(typeof(PSObject))]
public sealed class RemoveRegistryKeyCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = "";

    [Parameter]
    public SwitchParameter Recurse { get; set; }

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
            Infrastructure.LegacyError.Write(this, $"Cannot remove a root hive key: '{Path}'");
            WriteObject(null);

            return;
        }

        try
        {
            var paths = Expand(path);
            var status = new RegistryOperations(Store).Remove(
                Path,
                paths,
                Recurse,
                (target, action) => ShouldProcess(target, action),
                () =>
                    ShouldContinue(
                        $"The item at {Path} has children. Remove all children?",
                        "Confirm"
                    ),
                WriteVerbose
            );
            Status(Path, null, status);
        }
        catch (Exception error)
        {
            Failure(
                error,
                $"Access denied removing registry key: '{Path}'",
                $"Failed to remove registry key '{Path}': "
            );
            WriteObject(null);
        }
    }
}
