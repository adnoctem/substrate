using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;

namespace AdNoctem.Substrate.Windows;

/// <summary>A service's configured account identity and observed host.</summary>
public sealed class ServiceAccountUsage
{
    public string ComputerName { get; }
    public string DisplayName { get; }
    public string StartName { get; }
    public string State { get; }
    public uint ProcessId { get; }
    internal ServiceAccountUsage(string computer, CimRecord data) { ComputerName = computer; DisplayName = data.GetValue("DisplayName") as string ?? ""; StartName = data.GetValue("StartName") as string ?? ""; State = data.GetValue("State") as string ?? ""; ProcessId = Convert.ToUInt32(data.GetValue("ProcessId"), CultureInfo.InvariantCulture); }
}
/// <summary>A task's configured principal and run level, rather than proof that the task is currently running.</summary>
public sealed class ScheduledTaskAccountUsage
{
    public string ComputerName { get; }
    public string TaskName { get; }
    public string Status { get; }
    public string RunAsUser { get; }
    internal ScheduledTaskAccountUsage(string computer, CimRecord data)
    {
        ComputerName = computer;
        TaskName = (data.GetValue("TaskPath") as string ?? "") + (data.GetValue("TaskName") as string ?? "");
        Status = ((ScheduledTaskState)Convert.ToInt32(data.GetValue("State"), CultureInfo.InvariantCulture)).ToString();
        RunAsUser = (data.GetValue("Principal") as CimRecord)?.GetValue("UserId") as string ?? "";
    }
}
/// <summary>Service and task account discovery over a borrowed CIM session, or an owned local session. No processes or tasks are started.</summary>
public sealed class AccountUsageManager
{
    private readonly CimManager manager;
    private readonly string computer;
    public AccountUsageManager(CimSession? session = null) { manager = new CimManager(session); computer = session?.ComputerName ?? "localhost"; }
    public IReadOnlyList<ServiceAccountUsage> GetServiceAccounts(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Array.AsReadOnly(manager.Query(@"root\cimv2", "SELECT DisplayName, StartName, State, ProcessId FROM Win32_Service", timeout, cancellationToken).Select(item => new ServiceAccountUsage(computer, item)).ToArray());
    public IReadOnlyList<ScheduledTaskAccountUsage> GetTaskAccounts(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Array.AsReadOnly(manager.Query(@"root\Microsoft\Windows\TaskScheduler", "SELECT TaskName, TaskPath, State, Principal FROM MSFT_ScheduledTask", timeout, cancellationToken).Select(item => new ScheduledTaskAccountUsage(computer, item)).ToArray());
    public Task<IReadOnlyList<ServiceAccountUsage>> GetServiceAccountsAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => Task.Run(() => GetServiceAccounts(timeout, cancellationToken), cancellationToken);
    public Task<IReadOnlyList<ScheduledTaskAccountUsage>> GetTaskAccountsAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => Task.Run(() => GetTaskAccounts(timeout, cancellationToken), cancellationToken);
}
