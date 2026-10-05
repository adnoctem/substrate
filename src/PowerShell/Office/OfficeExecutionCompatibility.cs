using System;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Office;

namespace AdNoctem.Substrate.PowerShell.Office;

public static partial class OfficeCompatibility
{
    public static PSObject Deployment(string planJson, string expectedAction, string odtPath, string logRoot, bool forceCloseApplications,
        System.Security.SecureString? productKey, bool preview, string executionMetadata)
    {
        string? json = null;
        var failure = PathFailure(() =>
        {
            var plan = OfficeDeploymentPlanDocument.Parse(planJson);
            var action = (OfficeDeploymentAction)Enum.Parse(typeof(OfficeDeploymentAction), expectedAction, true);
            var options = new OfficeDeploymentOptions(IO.FileSystemPath.Parse(odtPath), IO.FileSystemPath.Parse(logRoot), forceCloseApplications, executionMetadata);
            var manager = new OfficeDeploymentManager();
            OfficeDeploymentResult result;
            if (preview)
                result = manager.Preview(plan, action, options, productKey);
            else
                switch (action)
                {
                    case OfficeDeploymentAction.Install:
                        result = manager.Install(plan, options, productKey);
                        break;
                    case OfficeDeploymentAction.Remove:
                        result = manager.Remove(plan, options);
                        break;
                    case OfficeDeploymentAction.Migrate:
                        result = manager.Migrate(plan, options, productKey);
                        break;
                    case OfficeDeploymentAction.Update:
                        result = manager.Update(plan, options);
                        break;
                    case OfficeDeploymentAction.AddLanguage:
                        result = manager.AddLanguage(plan, options);
                        break;
                    case OfficeDeploymentAction.RemoveLanguage:
                        result = manager.RemoveLanguage(plan, options);
                        break;
                    case OfficeDeploymentAction.SetApplicationSelection:
                        result = manager.SetApplicationSelection(plan, options);
                        break;
                    case OfficeDeploymentAction.SetUpdateConfiguration:
                        result = manager.SetUpdateConfiguration(plan, options);
                        break;
                    case OfficeDeploymentAction.SetApplicationPreference:
                        result = manager.SetApplicationPreferences(plan, options);
                        break;
                    default:
                        throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Unsupported deployment operation.");
                }
            json = result.ToJson();
        }, "OperationFailed");
        return Windows.SystemOutput.Object("Json", json, "Failure", failure);
    }
    public static PSObject Recover(string runId, string expectedAction, string odtPath, string logRoot, bool forceCloseApplications,
        System.Security.SecureString? productKey, bool preview, string executionMetadata)
    {
        string? json = null;
        var failure = PathFailure(() =>
        {
            var action = (OfficeDeploymentAction)Enum.Parse(typeof(OfficeDeploymentAction), expectedAction, true);
            var options = new OfficeDeploymentOptions(IO.FileSystemPath.Parse(odtPath), IO.FileSystemPath.Parse(logRoot), forceCloseApplications, executionMetadata);
            var manager = new OfficeDeploymentManager();
            if (action != OfficeDeploymentAction.Install && action != OfficeDeploymentAction.Migrate)
                throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Unsupported recovery operation.");
            json = (preview ? manager.PreviewRecovery(runId, action, options, productKey) : action == OfficeDeploymentAction.Install
                ? manager.ResumeInstallation(runId, options, productKey) : manager.ResumeMigration(runId, options, productKey)).ToJson();
        }, "OperationFailed");
        return Windows.SystemOutput.Object("Json", json, "Failure", failure);
    }
    public static PSObject ReadRecovery(string runId, string logRoot)
    {
        string? json = null;
        var failure = PathFailure(() => json = new OfficeRecoveryManager().Read(runId, new OfficePathGuard().ValidatePath(logRoot)).ToJson(), "InvalidRecoveryRecord");
        return Windows.SystemOutput.Object("Json", json, "Failure", failure);
    }
    public static PSObject? WriteJournal(string path, string json) => PathFailure(() => new OfficeJournalStore().Write(new OfficePathGuard().ValidatePath(path), json, true), "OperationFailed");
    public static PSObject? CheckHost() => PathFailure(() => new OfficeDeploymentEnvironment().RequireReady(TimeSpan.FromSeconds(60)), "PreflightFailed");
    public static bool SupportedHost() => new OfficeDeploymentEnvironment().ReadPlatform(TimeSpan.FromSeconds(60)).Supported;
    public static OfficeRecoveryDecision RecoveryDecision(object record, object current, string expectedAction, bool compliant = false)
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
            Items(Get(current, "Unknowns")).Any(), compliant, false, false, true);
    }
}
