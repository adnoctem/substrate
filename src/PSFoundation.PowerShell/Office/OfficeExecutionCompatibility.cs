using System;
using System.Linq;
using System.Management.Automation;
using PSFoundation.Office;

namespace PSFoundation.PowerShell.Office;

public static partial class OfficeCompatibility
{
    public static PSObject? WriteJournal(string path, string json) => PathFailure(() => new OfficeJournalStore().Write(new OfficePathGuard().ValidatePath(path), json, true), "OperationFailed");
    public static PSObject? CheckHost() => PathFailure(() => new OfficeDeploymentEnvironment().RequireReady(TimeSpan.FromSeconds(60)), "PreflightFailed");
    public static bool SupportedHost() => new OfficeDeploymentEnvironment().ReadPlatform(TimeSpan.FromSeconds(60)).Supported;
    public static OfficeRecoveryDecision RecoveryDecision(object record, object current, string expectedAction)
    {
        var plan = Get(record, "Plan")!;
        var before = Get(plan, "Before")!;
        var action = Text(Get(record, "Action"))!;
        var request = Request(action, Get(plan, "Configuration"), Text(Get(plan, "SourcePath")), Strings(Get(plan, "RemoveProductId")) ?? System.Array.Empty<string>(),
            LanguagePrimitives.IsTrue(Get(plan, "RemoveMsi")), Strings(Get(plan, "Language")) ?? System.Array.Empty<string>(), Get(plan, "Settings")!);
        var checkpoint = new OfficeRecoveryCheckpoint(LanguagePrimitives.ConvertTo<int>(Get(record, "SchemaVersion")), request, Text(Get(record, "Phase"))!,
            LanguagePrimitives.IsTrue(Get(record, "PhaseCompleted")), Items(Get(before, "Products")).Select(product => Text(Get(product, "ProductId"))!),
            Items(Get(before, "Msi")).Select(product => Text(Get(product, "ProductCode"))!));
        return new OfficeRecoveryPolicy().Evaluate(checkpoint, (OfficeDeploymentAction)Enum.Parse(typeof(OfficeDeploymentAction), expectedAction, true),
            Items(Get(current, "Products")).Select(product => Text(Get(product, "ProductId"))!), Items(Get(current, "Msi")).Select(product => Text(Get(product, "ProductCode"))!),
            Items(Get(current, "Unknowns")).Any(), false, false, false, true);
    }
}
