using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;
using PSFoundation.IO;

namespace PSFoundation.Policies;

public enum LgpoApplyMode { TextSource, GpoBackup }

/// <summary>Explicit local policy execution through a caller-selected executable. No downloads, elevation or prompts occur.</summary>
public sealed class LgpoTool
{
    public bool IsInstalled(FileSystemPath executable)
        => File.Exists((executable ?? throw new ArgumentNullException(nameof(executable))).Value);

    public LgpoApplyMode GetApplyMode(FileSystemPath policyPath)
    {
        if (policyPath == null)
            throw new ArgumentNullException(nameof(policyPath));
        var attributes = File.GetAttributes(policyPath.Value);
        return (attributes & FileAttributes.Directory) != 0 ? LgpoApplyMode.GpoBackup : LgpoApplyMode.TextSource;
    }

    /// <remarks>Preparing a request validates paths but makes no policy changes. Paths can change before execution; preparation is not authorization.
    /// The executable must already be trusted by the caller. Timeout and interruption policy must be chosen explicitly: stopping LGPO may leave partial policy changes.</remarks>
    public ProcessRequest CreateApplyRequest(FileSystemPath executable, FileSystemPath policyPath, TimeSpan timeout, ProcessStopBehavior stopBehavior)
    {
        if (!IsInstalled(executable))
            throw new FileNotFoundException("LGPO executable was not found.", executable.Value);
        var mode = GetApplyMode(policyPath);
        return new ProcessRequest(executable.Value, new[] { mode == LgpoApplyMode.GpoBackup ? "/g" : "/t", policyPath.Value },
            workingDirectory: Path.GetDirectoryName(executable.Value), timeout: timeout, stopBehavior: stopBehavior, maximumCapturedCharacters: int.MaxValue);
    }

    /// <remarks>Only an explicit apply call changes policy. Nonzero exit codes, cancellation and timeouts are returned as process evidence.
    /// With WaitForExit, cancellation after launch does not terminate the policy operation; it may wait indefinitely.</remarks>
    public Task<ProcessResult> ApplyAsync(FileSystemPath executable, FileSystemPath policyPath, TimeSpan timeout, ProcessStopBehavior stopBehavior,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessManager().RunAsync(CreateApplyRequest(executable, policyPath, timeout, stopBehavior), cancellationToken);
    }

    public ProcessResult Apply(FileSystemPath executable, FileSystemPath policyPath, TimeSpan timeout, ProcessStopBehavior stopBehavior,
        CancellationToken cancellationToken = default)
        => ApplyAsync(executable, policyPath, timeout, stopBehavior, cancellationToken).GetAwaiter().GetResult();
}
