using AdNoctem.Substrate.Registry.Compatibility;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.IO;
using System.Management.Automation;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(VerbsData.Mount, "DefaultUserHive", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(string))]
public sealed class MountDefaultUserHiveCommand : RegistryProcessCommand
{
    [Parameter(Position = 0), ValidatePattern("^[A-Za-z0-9_-]+$")] public string MountName { get; set; } = "DefaultUser";
    [Parameter(Position = 1), ValidateNotNullOrEmpty] public string HivePath { get; set; } = null!;
    protected override void BeginProcessing()
    {
        if (!MyInvocation.BoundParameters.ContainsKey("HivePath"))
            HivePath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive")!, @"Users\Default\NTUSER.DAT");
    }
    protected override void ProcessRecord()
    {
        try
        { WriteObject(HiveService.Mount(MountName, HivePath, (target, action) => ShouldProcess(target, action), Log, Cancellation.Token)); }
        catch (OperationCanceledException) { throw new PipelineStoppedException(); }
    }
}
