using PSFoundation.Registry.Compatibility;
using RegistryPath = PSFoundation.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = PSFoundation.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

[Cmdlet(VerbsData.Restore, "RegistrySettingState", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(PSObject))]
public sealed class RestoreRegistrySettingStateCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)] public object[] Settings { get; set; } = null!;
    [Parameter(Position = 1), AllowEmptyCollection]
    public object[]? ExpectedState { get; set; }
    private readonly Dictionary<string, RegistryState> expected = new Dictionary<string, RegistryState>(StringComparer.OrdinalIgnoreCase);

    protected override void BeginProcessing()
    {
        foreach (var entry in ExpectedState ?? Array.Empty<object>())
        {
            var state = Desired(entry, true, true);
            if (expected.ContainsKey(state.Identity))
                Invalid("ExpectedState contains duplicate registry values.");
            expected.Add(state.Identity, state);
        }
    }

    protected override void ProcessRecord()
    {
        foreach (var setting in Settings)
        {
            var desired = Desired(setting, true);
            expected.TryGetValue(desired.Identity, out var expectedValue);
            var result = new RegistryStateService(SnapshotStore, error => !(error is PipelineStoppedException) && !(error is ActionPreferenceStopException)).Restore(desired, MyInvocation.BoundParameters.ContainsKey("ExpectedState"), expectedValue,
                (target, action) => ShouldProcess(target, action));
            var output = Shape("Target", desired.Path.ProviderPath + "\\" + desired.Name, "Source", "Registry",
                "Action", result.Difference.Action, "Status", result.Status);
            if (result.Status == "Skipped")
                output.Properties.Add(new PSNoteProperty("SkippedReason", "WhatIf"));
            if (result.Error != null)
                output.Properties.Add(new PSNoteProperty("Error", result.Error));
            output.Properties.Add(new PSNoteProperty("Changed", result.Changed));
            output.Properties.Add(new PSNoteProperty("AlreadyCompliant", result.AlreadyCompliant));
            output.Properties.Add(new PSNoteProperty("Before", State(result.Difference.Before)));
            output.Properties.Add(new PSNoteProperty("After", State(result.After)));
            WriteObject(output);
        }
    }
}
