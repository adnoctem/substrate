using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Office;

/// <summary>An observed Office process identity, including start time to detect process identifier reuse.</summary>
public sealed class OfficeApplicationProcess
{
    public int ProcessId { get; }
    public string Name { get; }
    public DateTime StartedAtUtc { get; }

    internal OfficeApplicationProcess(int id, string name, DateTime started)
    {
        ProcessId = id;
        Name = name;
        StartedAtUtc = started;
    }
}

/// <summary>Explicit Office application discovery and termination. Never targets installer processes or quits an application implicitly.</summary>
public sealed class OfficeApplicationManager
{
    private static readonly string[] Names =
    {
        "WINWORD",
        "EXCEL",
        "POWERPNT",
        "OUTLOOK",
        "ONENOTE",
        "ONENOTEM",
        "MSPUB",
        "MSACCESS",
        "lync",
        "VISIO",
        "WINPROJ",
        "INFOPATH",
        "GROOVE",
        "MSOHTMED",
        "communicator",
    };

    public Task<IReadOnlyList<OfficeApplicationProcess>> ReadAsync(
        CancellationToken cancellationToken = default
    ) => Task.Run(() => Read(cancellationToken), cancellationToken);

    public Task<bool> ForceCloseAsync(
        OfficeApplicationProcess observed,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => ForceClose(observed, cancellationToken), cancellationToken);

    public IReadOnlyList<OfficeApplicationProcess> Read(
        CancellationToken cancellationToken = default
    )
    {
        var result = new List<OfficeApplicationProcess>();

        foreach (var name in Names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processes = Process.GetProcessesByName(name);

            try
            {
                foreach (var process in processes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!process.HasExited)
                            result.Add(
                                new OfficeApplicationProcess(
                                    process.Id,
                                    process.ProcessName,
                                    process.StartTime.ToUniversalTime()
                                )
                            );
                    }
                    catch (InvalidOperationException)
                    { /* Exited during observation. */
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>Force-terminates exactly one freshly matched process. Caller must authorize possible loss of unsaved work.</summary>
    public bool ForceClose(
        OfficeApplicationProcess observed,
        CancellationToken cancellationToken = default
    )
    {
        if (observed == null)
            throw new ArgumentNullException(nameof(observed));

        cancellationToken.ThrowIfCancellationRequested();
        Process process;

        try
        {
            process = Process.GetProcessById(observed.ProcessId);
        }
        catch (ArgumentException)
        {
            return false;
        }

        using (process)
        {
            if (process.HasExited)
                return false;

            if (
                !Names.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)
                || !OfficeDocument.Equal(process.ProcessName, observed.Name)
                || process.StartTime.ToUniversalTime() != observed.StartedAtUtc
            )
                throw new OfficeException(
                    OfficeFailureReason.ApplicationsRunning,
                    "Office process identity changed before closure."
                );

            cancellationToken.ThrowIfCancellationRequested();
            process.Kill();

            // A requested stop does not suppress the bounded wait for an already-issued termination.
            if (!process.WaitForExit(10000))
                throw new OfficeException(
                    OfficeFailureReason.ApplicationsRunning,
                    "Office application did not exit after termination was requested."
                );

            return true;
        }
    }
}
