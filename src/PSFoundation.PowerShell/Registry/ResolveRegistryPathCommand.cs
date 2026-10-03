using System;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsDiagnostic.Resolve, "RegistryPath")]
[OutputType(typeof(RegistryKey))]
public sealed class ResolveRegistryPathCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)] public string Path { get; set; } = "";
    [Parameter]
    public SwitchParameter Writable { get; set; }
    [Parameter(Position = 1)] public RegistryView View { get; set; } = RegistryView.Default;

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
        RegistryKey? key;
        try
        {
            key = new RegistryReader().Open(parsed, Writable, View);
        }
        catch (UnauthorizedAccessException)
        {
            Infrastructure.LegacyError.Write(this, $"Access denied opening registry key: '{Path}'");
            WriteObject(null);
            return;
        }
        catch (Exception error)
        {
            Infrastructure.LegacyError.Write(this, $"Failed to resolve registry path '{Path}': {error.Message}");
            WriteObject(null);
            return;
        }
        WriteObject(key);
    }
}
