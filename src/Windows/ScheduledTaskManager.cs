using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace AdNoctem.Substrate.Windows;

public enum ScheduledTaskState
{
    Unknown = 0,
    Disabled = 1,
    Queued = 2,
    Ready = 3,
    Running = 4,
}

/// <summary>Detached task action properties; arguments are data and must not be evaluated as shell code.</summary>
public sealed class ScheduledTaskAction
{
    public CimRecord Data { get; }
    public string? Id => Data.GetValue("Id") as string;
    public string? Executable => Data.GetValue("Execute") as string;
    public string? Arguments => Data.GetValue("Arguments") as string;
    public string? WorkingDirectory => Data.GetValue("WorkingDirectory") as string;
    public string? ComClassId => Data.GetValue("ClassId") as string;

    internal ScheduledTaskAction(CimRecord data) => Data = data;
}

/// <summary>Detached task identity, state, principal, and action observations.</summary>
public sealed class ScheduledTaskInfo
{
    public string Name { get; }
    public string FolderPath { get; }
    public ScheduledTaskState State { get; }
    public string? Description { get; }
    public string? Author { get; }
    public IReadOnlyList<ScheduledTaskAction> Actions { get; }

    internal ScheduledTaskInfo(CimInstance instance)
    {
        Name = instance.CimInstanceProperties["TaskName"]?.Value as string ?? "";
        FolderPath = instance.CimInstanceProperties["TaskPath"]?.Value as string ?? "";
        State = (ScheduledTaskState)
            Convert.ToInt32(
                instance.CimInstanceProperties["State"]?.Value,
                CultureInfo.InvariantCulture
            );
        Description = instance.CimInstanceProperties["Description"]?.Value as string;
        Author = instance.CimInstanceProperties["Author"]?.Value as string;
        var actions =
            instance.CimInstanceProperties["Actions"]?.Value as CimInstance[]
            ?? Array.Empty<CimInstance>();

        try
        {
            Actions = Array.AsReadOnly(
                actions
                    .Select(action => new ScheduledTaskAction(CimRecord.Capture(action)))
                    .ToArray()
            );
        }
        finally
        {
            foreach (var action in actions)
                action.Dispose();
        }
    }
}

/// <summary>Local Task Scheduler inventory and explicit state changes through its native CIM provider.</summary>
public sealed class ScheduledTaskManager
{
    private const string Namespace = @"root\Microsoft\Windows\TaskScheduler";

    /// <summary>Reads scheduled tasks and detached action data through the Windows task CIM provider.</summary>
    public IReadOnlyList<ScheduledTaskInfo> GetTasks(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        ServiceManager.ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var tasks = new List<ScheduledTaskInfo>();

        using (var session = CimSession.Create(null))
        using (
            var options = new CimOperationOptions
            {
                Timeout = timeout,
                CancellationToken = cancellationToken,
            }
        )
            foreach (
                var item in session.QueryInstances(
                    Namespace,
                    "WQL",
                    "SELECT TaskName, TaskPath, State, Description, Author, Actions FROM MSFT_ScheduledTask",
                    options
                )
            )
                using (item)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tasks.Add(new ScheduledTaskInfo(item));
                }

        return tasks.AsReadOnly();
    }

    /// <summary>Offloads scheduled-task inventory with provider timeout and cancellation.</summary>
    public Task<IReadOnlyList<ScheduledTaskInfo>> GetTasksAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => GetTasks(timeout, cancellationToken), cancellationToken);

    /// <summary>Finds one task by exact folder and name, or returns null when absent.</summary>
    public ScheduledTaskInfo? GetTask(
        string folderPath,
        string name,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        ValidateIdentity(folderPath, name);

        return GetTasks(timeout, cancellationToken)
            .SingleOrDefault(task =>
                string.Equals(task.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(task.Name, name, StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <summary>Changes one exact task's enabled state. Folder and name are required; no wildcard expansion or task execution occurs.</summary>
    /// <returns>The native provider return code. Zero means success; failures may also be raised as CIM exceptions.</returns>
    public uint SetEnabled(
        string folderPath,
        string name,
        bool enabled,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        ValidateIdentity(folderPath, name);
        ServiceManager.ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        using (var session = CimSession.Create(null))
        using (
            var options = new CimOperationOptions
            {
                Timeout = timeout,
                CancellationToken = cancellationToken,
            }
        )
        using (var reference = new CimInstance("MSFT_ScheduledTask", Namespace))
        {
            reference.CimInstanceProperties.Add(
                CimProperty.Create("TaskPath", folderPath, CimType.String, CimFlags.Key)
            );
            reference.CimInstanceProperties.Add(
                CimProperty.Create("TaskName", name, CimType.String, CimFlags.Key)
            );

            using (var task = session.GetInstance(Namespace, reference, options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parameters = new CimMethodParametersCollection
                {
                    CimMethodParameter.Create("InputObject", task, CimType.Instance, CimFlags.In),
                };

                using (
                    var result = session.InvokeMethod(
                        Namespace,
                        "PS_ScheduledTask",
                        enabled ? "EnableByObject" : "DisableByObject",
                        parameters,
                        options
                    )
                )
                {
                    // The provider returns an updated instance as cmdletOutput. It must not escape this boundary.
                    foreach (var output in result.OutParameters)
                        if (output.Value is CimInstance instance)
                            instance.Dispose();

                    return Convert.ToUInt32(result.ReturnValue.Value, CultureInfo.InvariantCulture);
                }
            }
        }
    }

    /// <summary>Changes task enablement on a worker thread without launching the task.</summary>
    /// <remarks>Cancellation is forwarded to CIM. A request already accepted by the provider may still finish changing the task.</remarks>
    public Task<uint> SetEnabledAsync(
        string folderPath,
        string name,
        bool enabled,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () => SetEnabled(folderPath, name, enabled, timeout, cancellationToken),
            cancellationToken
        );

    private static void ValidateIdentity(string folderPath, string name)
    {
        if (
            string.IsNullOrWhiteSpace(folderPath)
            || !folderPath.StartsWith("\\", StringComparison.Ordinal)
            || !folderPath.EndsWith("\\", StringComparison.Ordinal)
            || folderPath.IndexOf('\0') >= 0
        )
            throw new ArgumentException(
                "Use an absolute task folder with leading and trailing backslashes.",
                nameof(folderPath)
            );

        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '\\', '/', '\0' }) >= 0)
            throw new ArgumentException(
                "Use an exact task name without a folder path.",
                nameof(name)
            );
    }
}
