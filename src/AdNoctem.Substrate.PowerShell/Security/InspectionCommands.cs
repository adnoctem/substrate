using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Microsoft.Management.Infrastructure;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.PowerShell.Infrastructure;
using AdNoctem.Substrate.PowerShell.Windows;
using AdNoctem.Substrate.Security;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Security;

public static class InspectionCompatibility
{
    public static PSObject Object(CimRecord record)
    {
        var result = new PSObject();
        object? ConvertValue(object? value)
        {
            if (value is CimRecord child)
                return Object(child);
            if (!(value is string) && value is IEnumerable items)
                return items.Cast<object?>().Select(ConvertValue).ToArray();
            return value;
        }
        foreach (var pair in record.Properties)
            result.Properties.Add(new PSNoteProperty(pair.Key, ConvertValue(pair.Value)));
        result.Properties.Add(new PSNoteProperty("PSComputerName", record.ServerName));
        return result;
    }
    public static PSObject[] Threats(bool includeUrls)
    {
        return new DefenderManager().GetThreats(TimeSpan.FromSeconds(60)).Select(threat =>
        {
            var result = Object(threat.Data);
            if (includeUrls)
                result.Properties.Add(new PSNoteProperty("ThreatDescriptionURL", threat.DescriptionUrl));
            return result;
        }).ToArray();
    }
    public static PSObject[] Detections(DateTime cutoff, bool includeUrls)
    {
        var manager = new DefenderManager();
        var detections = manager.GetDetections(TimeSpan.FromSeconds(60), cutoff);
        var threats = includeUrls && detections.Count != 0 ? manager.GetThreats(TimeSpan.FromSeconds(60)) : Array.Empty<DefenderThreat>();
        var names = threats.GroupBy(threat => threat.Id).ToDictionary(group => group.Key, group => group.First().DescriptionUrl);
        return detections.Select(detection =>
        {
            var result = Object(detection.Data);
            if (includeUrls)
                result.Properties.Add(new PSNoteProperty("ThreatDescriptionURL", names.TryGetValue(detection.ThreatId, out var url) ? url : null));
            return result;
        }).ToArray();
    }
    public static PSObject[] Files(string path, DateTime start, DateTime end, bool desktop, Action<string> verbose)
    {
        var inventory = new FileInventoryManager().Find(FileSystemPath.Parse(path), new DateTimeOffset(start), new DateTimeOffset(end), continueOnError: true);
        foreach (var failure in inventory.Errors)
            verbose(failure.Path + ": " + failure.Error.Message);
        foreach (var skipped in inventory.SkippedReparsePoints)
            verbose("Skipped directory reparse point: " + skipped);
        return inventory.Files.Select(file => SystemOutput.Object("LastWriteTime", file.LastWriteTimeUtc.ToLocalTime(), "LastWriteTimeUtc", file.LastWriteTimeUtc,
            "LastAccessTime", file.LastAccessTimeUtc.ToLocalTime(), "LastAccessTimeUtc", file.LastAccessTimeUtc,
            "CreationTime", file.CreationTimeUtc.ToLocalTime(), "CreationTimeUtc", file.CreationTimeUtc, "Mode", Mode(file.Attributes, desktop),
            "IsReadOnly", file.IsReadOnly, "Length", file.Length, "Extension", Path.GetExtension(file.Path.Value), "FullName", file.Path.Value)).ToArray();
    }
    private static string Mode(FileAttributes attributes, bool desktop)
    {
        bool Has(FileAttributes value) => (attributes & value) != 0;
        return (!desktop && Has(FileAttributes.ReparsePoint) ? "l" : "-") + (Has(FileAttributes.Archive) ? "a" : "-")
            + (Has(FileAttributes.ReadOnly) ? "r" : "-") + (Has(FileAttributes.Hidden) ? "h" : "-") + (Has(FileAttributes.System) ? "s" : "-")
            + (desktop ? Has(FileAttributes.ReparsePoint) ? "l" : "-" : "");
    }
}

[Cmdlet(VerbsCommon.Get, "DefenderThreatDescriptionURL"), OutputType(typeof(string))]
public sealed class GetDefenderThreatDescriptionUrlCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)] public string ThreatName { get; set; } = "";
    protected override void ProcessRecord() => WriteObject(DefenderManager.GetThreatDescriptionUrl(ThreatName));
}
[Cmdlet(VerbsCommon.Add, "DefenderExclusion", SupportsShouldProcess = true), OutputType(typeof(PSObject))]
public sealed class AddDefenderExclusionCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0), ValidateSet("Path", "Extension", "Process")] public string Type { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1), ValidateNotNullOrEmpty] public string Value { get; set; } = "";
    protected override void ProcessRecord()
    {
        var target = Type + ": " + Value;
        if (!ShouldProcess(target, "Add Microsoft Defender exclusion"))
        { Result(target, "Skipped", "WhatIf"); return; }
        try
        {
            var code = new DefenderManager().AddExclusions((DefenderExclusionKind)Enum.Parse(typeof(DefenderExclusionKind), Type, true), new[] { Value }, InventoryTimeout, Cancellation);
            Result(target, code == 0 ? "Completed" : "Failed", code == 0 ? Value : "Defender provider returned 0x" + code.ToString("X8"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { Result(target, "Failed", error.Message); }
    }
    private void Result(string target, string status, string detail) => WriteObject(OperationOutput.Create(target, "Defender", "AddExclusion", status,
        new Dictionary<string, object?> { ["Detail"] = detail }));
}
[Cmdlet(VerbsCommon.Get, "ScheduledTaskAction"), OutputType(typeof(PSObject[]))]
public sealed class GetScheduledTaskActionCommand : SystemCommand
{
    [Parameter] public SwitchParameter SuspiciousOnly { get; set; }
    protected override void ProcessRecord()
    {
        IReadOnlyList<ScheduledTaskInfo> tasks;
        try
        { tasks = new ScheduledTaskManager().GetTasks(InventoryTimeout, Cancellation); }
        catch (CimException error) { WriteVerbose(error.Message); return; }
        var hosts = new Regex("powershell|pwsh|cmd|wscript|cscript|mshta|rundll32|regsvr32|InstallUtil", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        foreach (var task in tasks.OrderBy(task => task.FolderPath, StringComparer.CurrentCultureIgnoreCase).ThenBy(task => task.Name, StringComparer.CurrentCultureIgnoreCase))
            foreach (var action in task.Actions)
            {
                if (SuspiciousOnly && (action.Executable == null || !hosts.IsMatch(action.Executable)))
                    continue;
                WriteObject(SystemOutput.Object("TaskName", task.Name, "TaskPath", task.FolderPath, "State", task.State, "Execute", action.Executable, "Arguments", action.Arguments));
            }
    }
}
[Cmdlet(VerbsCommon.Get, "WMIPersistence"), OutputType(typeof(PSObject))]
public sealed class GetWmiPersistenceCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var inventory = new WmiPersistenceManager().Read(InventoryTimeout, continueOnError: true, cancellationToken: Cancellation);
        foreach (var failure in inventory.Errors)
            WriteVerbose(failure.ClassName + ": " + failure.Error.Message);
        WriteObject(SystemOutput.Object("EventFilters", inventory.EventFilters.Select(InspectionCompatibility.Object).ToArray(),
            "CommandLineConsumers", inventory.Consumers.Where(item => string.Equals(item.ClassName, "CommandLineEventConsumer", StringComparison.OrdinalIgnoreCase)).Select(InspectionCompatibility.Object).ToArray(),
            "Bindings", inventory.Bindings.Select(InspectionCompatibility.Object).ToArray()));
    }
}
