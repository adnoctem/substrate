using System;
using System.Management.Automation;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsData.Dismount, "DefaultUserHive", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class DismountDefaultUserHiveCommand : RegistryToolCommand
{
    [Parameter(Position = 0), ValidatePattern("^[A-Za-z0-9_-]+$")] public string MountName { get; set; } = "DefaultUser";
    protected override void ProcessRecord()
    {
        try
        { HiveService.Dismount(MountName, (target, action) => ShouldProcess(target, action), Log, Cancellation.Token); }
        catch (OperationCanceledException) { throw new PipelineStoppedException(); }
    }
}
