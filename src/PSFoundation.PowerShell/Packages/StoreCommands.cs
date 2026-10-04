using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PSFoundation.Packages;
using PSFoundation.PowerShell.Windows;

namespace PSFoundation.PowerShell.Packages;

[Cmdlet(VerbsCommon.Get, "MSStoreUpdate"), OutputType(typeof(PSObject[]))]
public sealed class GetMSStoreUpdateCommand : SystemCommand
{
    [Parameter(Position = 0)] public string PackageFamilyName { get; set; } = "";
    protected override void ProcessRecord()
    {
        var filter = string.IsNullOrEmpty(PackageFamilyName) ? null : new WildcardPattern(PackageFamilyName, WildcardOptions.IgnoreCase);
        foreach (var item in new StoreUpdateManager().FindUpdates(Cancellation))
            if (filter == null || filter.IsMatch(item.PackageFamilyName))
                WriteObject(SystemOutput.Object("PackageFamilyName", item.PackageFamilyName, "ProductId", item.ProductId,
                "ItemKind", null, "ErrorCode", item.ErrorCode, "InstallType", item.InstallType, "CompletedInstallCount", null, "TotalInstallCount", null));
    }
}
[Cmdlet(VerbsLifecycle.Install, "MSStoreUpdate", DefaultParameterSetName = "Pipeline", SupportsShouldProcess = true), OutputType(typeof(PSObject[]))]
public sealed class InstallMSStoreUpdateCommand : SystemCommand
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Pipeline")] public PSObject[] InputObject { get; set; } = Array.Empty<PSObject>();
    [Parameter(Mandatory = true, ParameterSetName = "Name")] public string PackageFamilyName { get; set; } = "";
    [Parameter(Mandatory = true, ParameterSetName = "All")] public SwitchParameter All { get; set; }
    private readonly List<string> targets = new List<string>();
    protected override void ProcessRecord()
    {
        if (ParameterSetName != "Pipeline")
            return;
        foreach (var item in InputObject)
        {
            var name = LanguagePrimitives.ConvertTo<string>(item.Properties["PackageFamilyName"]?.Value);
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Selected Store update is missing its package family name.");
            targets.Add(name);
        }
    }
    protected override void EndProcessing()
    {
        var manager = new StoreUpdateManager();
        if (ParameterSetName != "Pipeline")
        {
            var pattern = ParameterSetName == "Name" ? new WildcardPattern(PackageFamilyName, WildcardOptions.IgnoreCase) : null;
            targets.AddRange(manager.FindUpdates(Cancellation).Where(item => pattern == null || pattern.IsMatch(item.PackageFamilyName)).Select(item => item.PackageFamilyName));
        }
        if (targets.Count == 0)
        { WriteError(new ErrorRecord(new InvalidOperationException("No Store updates to install."), "NoStoreUpdates", ErrorCategory.ObjectNotFound, PackageFamilyName)); return; }
        foreach (var name in targets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!ShouldProcess(name, "Install Microsoft Store update"))
                continue;
            try
            {
                var result = manager.Install(name, Cancellation);
                WriteObject(result == StoreUpdateOutcome.NoUpdate ? SystemOutput.Object("PackageFamilyName", name, "Status", "NoUpdate") : SystemOutput.Object("PackageFamilyName", name, "Status", "Completed", "ErrorCode", 0));
            }
            catch (Exception error) when (!(error is OperationCanceledException))
            { WriteVerbose(error.Message); WriteObject(SystemOutput.Object("PackageFamilyName", name, "Status", "Failed", "ErrorCode", error.HResult)); }
        }
    }
}
