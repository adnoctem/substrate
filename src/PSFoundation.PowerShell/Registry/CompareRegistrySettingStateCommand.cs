using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System.Management.Automation;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsData.Compare, "RegistrySettingState")]
[OutputType(typeof(PSObject))]
public sealed class CompareRegistrySettingStateCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)] public object[] Settings { get; set; } = null!;
    protected override void ProcessRecord()
    {
        foreach (var setting in Settings)
        {
            var diff = new RegistryStateService(SnapshotStore).Compare(Desired(setting));
            WriteObject(Shape("Path", diff.After.Path.ProviderPath, "Name", diff.After.Name, "View", diff.After.View.ToString(),
                "Before", State(diff.Before), "After", State(diff.After), "Changed", diff.Changed, "Action", diff.Action));
        }
    }
}
