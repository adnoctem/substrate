using System;
using System.Management.Automation;
using AdNoctem.Substrate.Registry.Compatibility;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(
    VerbsData.Dismount,
    "DefaultUserHive",
    SupportsShouldProcess = true,
    ConfirmImpact = ConfirmImpact.Medium
)]
public sealed class DismountDefaultUserHiveCommand : RegistryProcessCommand
{
    [Parameter(Position = 0), ValidatePattern("^[A-Za-z0-9_-]+$")]
    public string MountName { get; set; } = "DefaultUser";

    protected override void ProcessRecord()
    {
        try
        {
            HiveService.Dismount(
                MountName,
                (target, action) => ShouldProcess(target, action),
                Log,
                Cancellation.Token
            );
        }
        catch (OperationCanceledException)
        {
            throw new PipelineStoppedException();
        }
    }
}
