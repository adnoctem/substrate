using System;

namespace AdNoctem.Substrate.Diagnostics;

/// <summary>Completed process evidence. Nonzero exit codes and requested stops are data, not automatic retry or success policy.</summary>
public sealed class ProcessResult
{
    public int ProcessId { get; }
    public int ExitCode { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }
    public bool OutputTruncated { get; }
    public bool ErrorTruncated { get; }
    public TimeSpan Duration { get; }
    public bool TimedOut { get; }
    public bool Cancelled { get; }

    internal ProcessResult(
        int processId,
        int exitCode,
        string output,
        string error,
        bool outputTruncated,
        bool errorTruncated,
        TimeSpan duration,
        bool timedOut,
        bool cancelled
    )
    {
        ProcessId = processId;
        ExitCode = exitCode;
        StandardOutput = output;
        StandardError = error;
        OutputTruncated = outputTruncated;
        ErrorTruncated = errorTruncated;
        Duration = duration;
        TimedOut = timedOut;
        Cancelled = cancelled;
    }
}
