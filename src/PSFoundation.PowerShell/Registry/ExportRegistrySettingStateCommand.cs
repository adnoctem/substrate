using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System.Collections;
using System.Management.Automation;
using Microsoft.Win32;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsData.Export, "RegistrySettingState")]
[OutputType(typeof(PSObject[]))]
public sealed class ExportRegistrySettingStateCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)] public object[] Settings { get; set; } = null!;
    [Parameter]
    public SwitchParameter Detailed { get; set; }
    [Parameter(Position = 1)] public RegistryView View { get; set; } = RegistryView.Default;

    protected override void ProcessRecord()
    {
        foreach (var setting in Settings)
        {
            if (setting == null)
                continue;
            var snapshot = new PSObject();
            if (Unwrap(setting) is Hashtable table)
                foreach (DictionaryEntry entry in table)
                    snapshot.Properties.Add(new PSNoteProperty(Text(entry.Key), entry.Value));
            else
                foreach (var property in PSObject.AsPSObject(setting).Properties)
                    snapshot.Properties.Add(new PSNoteProperty(property.Name, property.Value));
            if (snapshot.Properties["Path"] == null || snapshot.Properties["Name"] == null)
            {
                Infrastructure.LegacyError.Write(this, "Registry setting snapshots require Path and Name properties.");
                continue;
            }
            var path = Detailed ? ParseRequired(Text(snapshot.Properties["Path"].Value)) : Parse(Text(snapshot.Properties["Path"].Value));
            if (path == null)
                continue;
            var name = Text(snapshot.Properties["Name"].Value);
            if (Detailed)
            {
                var view = snapshot.Properties["View"] == null ? View : LanguagePrimitives.ConvertTo<RegistryView>(snapshot.Properties["View"].Value);
                foreach (var property in State(ReadState(path, name, view)).Properties)
                    snapshot.Properties.Add(new PSNoteProperty(property.Name, property.Value));
            }
            else
            {
                var state = Store.Read(path, name == "(default)" ? "" : name, RegistryView.Default);
                object? value = state.Exists ? Store.GetValue(path, name == "(default)" ? "" : name) : null;
                snapshot.Properties.Add(new PSNoteProperty("Preferred", value));
                if (state.Exists && snapshot.Properties["Type"] != null)
                    snapshot.Properties.Add(new PSNoteProperty("Type", state.Kind.ToString()));
            }
            WriteObject(snapshot);
        }
    }

}
