using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsData.ConvertTo, "RegistrySettingResult")]
[OutputType(typeof(PSObject[]))]
public sealed class ConvertToRegistrySettingResultCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)] public object[] Settings { get; set; } = null!;
    [Parameter]
    public SwitchParameter Undo { get; set; }
    [Parameter]
    public SwitchParameter DryRun { get; set; }
    [Parameter(Position = 1)] public string Source { get => source; set => source = value ?? ""; }
    private string source = "Registry";

    protected override void ProcessRecord()
    {
        foreach (var setting in Settings)
        {
            if (setting == null)
                continue;
            var fields = Fields(setting);
            var pathValue = Required(fields, "Path");
            var nameValue = Required(fields, "Name");
            var target = Required(fields, Undo ? "Default" : "Preferred");
            // v1 tests PSObject.Properties for Description: hashtable entries are not properties.
            var description = PSObject.AsPSObject(setting).Properties["Description"]?.Value;
            var pathText = Text(pathValue);
            var name = Text(nameValue);
            var audit = RegistrySettingAudit.Inspect(pathText, target, Undo, DryRun, Text(description),
                () => Store.KeyExists(RegistryPath.Parse(@"HKU\DefaultUser")),
                () => { var parsed = Parse(pathText); return parsed != null && Store.Read(parsed, name, RegistryView.Default).Exists; },
                () => Store.GetValue(RegistryPath.Parse(pathText), name), Text);
            WriteObject(Shape("Target", pathText + "\\" + name, "Source", Source, "Action", audit.Action, "Status", audit.Status, "Detail", audit.Detail));
        }
    }
}
