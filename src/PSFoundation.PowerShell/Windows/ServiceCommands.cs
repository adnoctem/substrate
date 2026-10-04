using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PSFoundation.PowerShell.Infrastructure;
using PSFoundation.Windows;

namespace PSFoundation.PowerShell.Windows;

public sealed class ServiceStartupSelection
{
    public string Name { get; }
    public PSObject? SkippedResult { get; }
    internal ServiceStartupSelection(string name, PSObject? skipped = null) { Name = name; SkippedResult = skipped; }
}
public static class ServiceCompatibility
{
    public static ServiceStartupSelection[] Select(string[] names, string startupType, string[]? excluded, string[]? protectedNames)
    {
        var services = new ServiceManager().GetServices(TimeSpan.FromSeconds(60));
        var result = new List<ServiceStartupSelection>();
        foreach (var pattern in names)
        {
            var matches = services.Where(item => new WildcardPattern(pattern, WildcardOptions.IgnoreCase).IsMatch(item.Name)).ToArray();
            if (matches.Length == 0)
            { result.Add(new ServiceStartupSelection(pattern, Result(pattern, "Skipped", "Service not found."))); continue; }
            foreach (var service in matches)
            {
                var filtered = (excluded ?? Array.Empty<string>()).Contains(pattern, StringComparer.OrdinalIgnoreCase)
                    || (excluded ?? Array.Empty<string>()).Contains(service.Name, StringComparer.OrdinalIgnoreCase);
                var guarded = (protectedNames ?? Array.Empty<string>()).Contains(service.Name, StringComparer.OrdinalIgnoreCase)
                    && !string.Equals(startupType, "Disabled", StringComparison.OrdinalIgnoreCase);
                result.Add(new ServiceStartupSelection(service.Name, filtered ? Result(service.Name, "Skipped", "Excluded via -Filter.")
                    : guarded ? Result(service.Name, "Refused", "Protected service - only Disabled is permitted without an explicit -Filter exclusion.") : null));
            }
        }
        return result.ToArray();
    }
    public static PSObject Result(string name, string status, string detail) => OperationOutput.Create(name, "Service", "SetStartup", status,
        new Dictionary<string, object?> { ["Detail"] = detail });
    public static PSObject Apply(string name, string startupType)
    {
        try
        {
            var mode = (ServiceStartupType)Enum.Parse(typeof(ServiceStartupType), startupType, true);
            var code = new ServiceManager().SetStartupType(name, mode, TimeSpan.FromSeconds(60));
            if (code != 0)
                throw new InvalidOperationException("ChangeStartMode returned " + code + ".");
            return Result(name, "Completed", "Startup type set to " + startupType + ".");
        }
        catch (Exception error) { return OperationOutput.Create(name, "Service", "SetStartup", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message }); }
    }
}

[Cmdlet(VerbsCommon.Set, "ScheduledTaskState", SupportsShouldProcess = true)]
public sealed class SetScheduledTaskStateCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string[] TaskName { get; set; } = Array.Empty<string>();
    [Parameter(Mandatory = true, Position = 1), ValidateSet("Enabled", "Disabled")] public string State { get; set; } = "";
    [Parameter(Position = 2)] public string[] Filter { get; set; } = Array.Empty<string>();
    protected override void ProcessRecord()
    {
        var manager = new ScheduledTaskManager();
        var tasks = manager.GetTasks(InventoryTimeout, Cancellation);
        foreach (var pattern in TaskName)
        {
            Cancellation.ThrowIfCancellationRequested();
            PSObject Result(string status, string detail) => OperationOutput.Create(pattern, "ScheduledTask", "SetState", status,
                new Dictionary<string, object?> { ["Detail"] = detail });
            var wildcard = new WildcardPattern(pattern, WildcardOptions.IgnoreCase);
            var matches = tasks.Where(task => wildcard.IsMatch(task.Name)).ToArray();
            if (matches.Length == 0)
            { WriteObject(Result("Skipped", "Task not found.")); continue; }
            if (Filter.Contains(pattern, StringComparer.OrdinalIgnoreCase))
            { WriteObject(Result("Skipped", "Excluded via -Filter.")); continue; }
            var selected = matches.Where(task => !Filter.Contains(task.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (selected.Length == 0)
            { WriteObject(Result("Skipped", "Excluded via -Filter.")); continue; }
            if (!ShouldProcess(pattern, "Set state to " + State))
            { WriteObject(Result("Skipped", "WhatIf")); continue; }
            try
            {
                foreach (var task in selected)
                {
                    Cancellation.ThrowIfCancellationRequested();
                    var code = manager.SetEnabled(task.FolderPath, task.Name, string.Equals(State, "Enabled", StringComparison.OrdinalIgnoreCase), InventoryTimeout, Cancellation);
                    if (code != 0)
                        throw new InvalidOperationException("Task state change returned " + code + " for " + task.FolderPath + task.Name + ".");
                }
                WriteObject(Result("Completed", "State set to " + State + "."));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { WriteObject(OperationOutput.Create(pattern, "ScheduledTask", "SetState", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message })); }
        }
    }
}
