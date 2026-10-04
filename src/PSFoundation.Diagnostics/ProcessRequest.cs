using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace PSFoundation.Diagnostics;

public enum ProcessStopBehavior { TerminateProcess, TerminateTree, WaitForExit }

/// <summary>A shell-free Windows process request. Arguments and environment are copied; credentials must not be placed in arguments.</summary>
public sealed class ProcessRequest
{
    public string FileName { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string? WorkingDirectory { get; }
    public IReadOnlyDictionary<string, string?> Environment { get; }
    public TimeSpan Timeout { get; }
    public TimeSpan CleanupTimeout { get; }
    public ProcessStopBehavior StopBehavior { get; }
    public int MaximumCapturedCharacters { get; }

    /// <remarks>WaitForExit checks cancellation before launch, then records stop requests without interrupting the child. It can wait indefinitely.
    /// TerminateTree is best effort for descendants still attached to the process tree; detached processes are outside this ownership boundary.
    /// Output beyond the per-stream limit is drained and discarded. A zero limit captures nothing.</remarks>
    public ProcessRequest(string fileName, IEnumerable<string>? arguments = null, string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null, TimeSpan? timeout = null,
        ProcessStopBehavior stopBehavior = ProcessStopBehavior.TerminateProcess,
        int maximumCapturedCharacters = 16 * 1024 * 1024, TimeSpan? cleanupTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOf('\0') >= 0)
            throw new ArgumentException("An executable path is required.", nameof(fileName));
        var args = (arguments ?? Array.Empty<string>()).ToArray();
        if (args.Any(a => a == null || a.IndexOf('\0') >= 0))
            throw new ArgumentException("Arguments cannot be null or contain NUL.", nameof(arguments));
        if (!Enum.IsDefined(typeof(ProcessStopBehavior), stopBehavior))
            throw new ArgumentOutOfRangeException(nameof(stopBehavior));
        if (maximumCapturedCharacters < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCapturedCharacters));
        Timeout = timeout ?? System.Threading.Timeout.InfiniteTimeSpan;
        CleanupTimeout = cleanupTimeout ?? TimeSpan.FromSeconds(10);
        if (Timeout != System.Threading.Timeout.InfiniteTimeSpan && Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (CleanupTimeout <= TimeSpan.Zero || CleanupTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(cleanupTimeout));
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (environment != null)
            foreach (var pair in environment)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Key.IndexOfAny(new[] { '=', '\0' }) >= 0 || pair.Value?.IndexOf('\0') >= 0)
                    throw new ArgumentException("Invalid environment entry.", nameof(environment));
                env.Add(pair.Key, pair.Value);
            }
        FileName = fileName;
        Arguments = Array.AsReadOnly(args);
        WorkingDirectory = workingDirectory;
        Environment = new ReadOnlyDictionary<string, string?>(env);
        StopBehavior = stopBehavior;
        MaximumCapturedCharacters = maximumCapturedCharacters;
    }
}
