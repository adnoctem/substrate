using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Windows;

internal static class UpdateOutput
{
    internal static PSObject Info(WindowsUpdateInfo update) => SystemOutput.Object("Identity", SystemOutput.Object("UpdateID", update.UpdateId.ToString(), "RevisionNumber", update.RevisionNumber),
        "UpdateID", update.UpdateId.ToString(), "KB", update.KBArticleIds.Select(id => "KB" + id).ToArray(), "KBArticleIDs", update.KBArticleIds.ToArray(), "Title", update.Title,
        "Categories", update.Categories.Select(name => SystemOutput.Object("Name", name)).ToArray(), "IsDownloaded", update.IsDownloaded, "IsInstalled", update.IsInstalled);
    internal static PSObject Result(WindowsUpdateResult result) => SystemOutput.Object("KB", result.Update.KBArticleIds.Select(id => "KB" + id).ToArray(), "Title", result.Update.Title,
        "HResult", result.HResult, "Result", ResultName(result.ResultCode), "RebootRequired", result.RebootRequired);
    internal static string ResultName(int code) => code == 2 ? "Succeeded" : code == 3 ? "SucceededWithErrors" : code == 4 ? "Failed" : code == 5 ? "Aborted" : code == 1 ? "InProgress" : "NotStarted";
    internal static bool MatchesKB(WindowsUpdateInfo update, string kb) => update.KBArticleIds.Any(id => string.Equals(id, kb.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ? kb.Substring(2) : kb, StringComparison.OrdinalIgnoreCase));
    internal static Guid Id(PSObject value)
    {
        var identity = value.Properties["Identity"]?.Value;
        var text = identity == null ? value.Properties["UpdateID"]?.Value : PSObject.AsPSObject(identity).Properties["UpdateID"]?.Value;
        if (!Guid.TryParse(Convert.ToString(text), out var id) || id == Guid.Empty)
            throw new ArgumentException("Selected update is missing its UpdateID; refusing an unfiltered update operation.");
        return id;
    }
}
[Cmdlet(VerbsCommon.Get, "WindowsUpdate"), OutputType(typeof(PSObject[]))]
public sealed class GetWindowsUpdateCommand : SystemCommand
{
    [Parameter(Position = 0)] public string[] Category { get; set; } = Array.Empty<string>();
    [Parameter(Position = 1)] public string KBArticleID { get; set; } = "";
    [Parameter(Position = 2)] public string Title { get; set; } = "";
    protected override void ProcessRecord()
    {
        var title = string.IsNullOrEmpty(Title) ? null : new WildcardPattern(Title, WildcardOptions.IgnoreCase);
        foreach (var update in new WindowsUpdateManager().FindAvailable(Cancellation))
        {
            if (Category.Length != 0 && !update.Categories.Intersect(Category, StringComparer.OrdinalIgnoreCase).Any())
                continue;
            if (KBArticleID.Length != 0 && !UpdateOutput.MatchesKB(update, KBArticleID))
                continue;
            if (title != null && !title.IsMatch(update.Title))
                continue;
            WriteObject(UpdateOutput.Info(update));
        }
    }
}
[Cmdlet(VerbsLifecycle.Install, "WindowsUpdate", DefaultParameterSetName = "Pipeline", SupportsShouldProcess = true), OutputType(typeof(PSObject[]))]
public sealed class InstallWindowsUpdateCommand : SystemCommand
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Pipeline")] public PSObject[] InputObject { get; set; } = Array.Empty<PSObject>();
    [Parameter(Mandatory = true, ParameterSetName = "KB")] public string KBArticleID { get; set; } = "";
    [Parameter(Mandatory = true, ParameterSetName = "All")] public SwitchParameter AcceptAll { get; set; }
    [Parameter] public SwitchParameter AutoReboot { get; set; }
    [Parameter] public SwitchParameter IgnoreReboot { get; set; }
    private readonly HashSet<Guid> ids = new HashSet<Guid>();
    protected override void ProcessRecord()
    { if (ParameterSetName == "Pipeline") foreach (var item in InputObject) ids.Add(UpdateOutput.Id(item)); }
    protected override void EndProcessing()
    {
        var manager = new WindowsUpdateManager();
        if (ParameterSetName != "Pipeline")
            foreach (var item in manager.FindAvailable(Cancellation))
                if (ParameterSetName == "All" || UpdateOutput.MatchesKB(item, KBArticleID))
                    ids.Add(item.UpdateId);
        if (ids.Count == 0)
        { WriteError(new ErrorRecord(new InvalidOperationException("No updates to install."), "NoUpdates", ErrorCategory.ObjectNotFound, null)); return; }
        if (!ShouldProcess(string.Join(", ", ids), "Install Windows updates"))
            return;
        var results = manager.Install(ids, true, Cancellation);
        foreach (var result in results)
            WriteObject(UpdateOutput.Result(result));
        if (AutoReboot && !IgnoreReboot && results.Any(result => result.RebootRequired))
            SessionState.InvokeCommand.InvokeScript("Microsoft.PowerShell.Management\\Restart-Computer -Force");
    }
}
[Cmdlet(VerbsCommon.Hide, "WindowsUpdate", SupportsShouldProcess = true)]
public sealed class HideWindowsUpdateCommand : SystemCommand
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Pipeline")] public PSObject[] InputObject { get; set; } = Array.Empty<PSObject>();
    [Parameter(Mandatory = true, ParameterSetName = "KB")] public string KBArticleID { get; set; } = "";
    private readonly HashSet<Guid> ids = new HashSet<Guid>();
    protected override void ProcessRecord() { if (ParameterSetName == "Pipeline") foreach (var item in InputObject) ids.Add(UpdateOutput.Id(item)); }
    protected override void EndProcessing()
    {
        var manager = new WindowsUpdateManager();
        if (ParameterSetName == "KB")
            foreach (var item in manager.FindAvailable(Cancellation))
                if (UpdateOutput.MatchesKB(item, KBArticleID))
                    ids.Add(item.UpdateId);
        foreach (var id in ids)
            if (ShouldProcess(id.ToString(), "Hide update"))
                manager.SetHidden(new[] { id }, true, Cancellation);
    }
}
[Cmdlet(VerbsCommon.Get, "WindowsUpdateHistory"), OutputType(typeof(PSObject[]))]
public sealed class GetWindowsUpdateHistoryCommand : SystemCommand
{
    [Parameter(Position = 0)] public int Last { get; set; }
    [Parameter(Position = 1)] public string KBArticleID { get; set; } = "";
    protected override void ProcessRecord()
    {
        var filter = KBArticleID.Length == 0 ? null : new Regex(@"\b" + Regex.Escape(KBArticleID) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        foreach (var entry in new WindowsUpdateManager().GetHistory(MyInvocation.BoundParameters.ContainsKey(nameof(Last)) ? Last : (int?)null, Cancellation))
        {
            if (filter != null && !filter.IsMatch(entry.Title))
                continue;
            WriteObject(SystemOutput.Object("Title", entry.Title, "Date", entry.Date, "Operation", entry.Operation == 1 ? "Installation" : "Uninstallation", "Result", UpdateOutput.ResultName(entry.ResultCode), "HResult", entry.HResult, "UpdateID", entry.UpdateId.ToString()));
        }
    }
}
[Cmdlet(VerbsLifecycle.Uninstall, "WindowsUpdate", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class UninstallWindowsUpdateCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string KBArticleID { get; set; } = "";
    protected override void ProcessRecord()
    {
        var manager = new WindowsUpdateManager();
        var ids = manager.FindInstalled(Cancellation).Where(update => UpdateOutput.MatchesKB(update, KBArticleID)).Select(update => update.UpdateId).ToArray();
        if (ids.Length == 0)
        { WriteError(new ErrorRecord(new InvalidOperationException("No installed update found for KB: " + KBArticleID), "UpdateNotFound", ErrorCategory.ObjectNotFound, KBArticleID)); return; }
        if (!ShouldProcess(KBArticleID, "Uninstall Windows update"))
            return;
        foreach (var result in manager.Uninstall(ids, Cancellation))
            WriteObject(UpdateOutput.Result(result));
    }
}
[Cmdlet(VerbsDiagnostic.Test, "WindowsUpdateRebootRequired"), OutputType(typeof(bool))]
public sealed class TestWindowsUpdateRebootRequiredCommand : SystemCommand
{ protected override void ProcessRecord() => WriteObject(new WindowsUpdateManager().IsRebootRequired()); }
[Cmdlet(VerbsCommon.Get, "WindowsUpdateConfiguration"), OutputType(typeof(PSObject))]
public sealed class GetWindowsUpdateConfigurationCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var output = new PSObject();
        foreach (var entry in new WindowsUpdateManager().GetConfiguration())
            output.Properties.Add(new PSNoteProperty(entry.Key, entry.Value));
        WriteObject(output);
    }
}
