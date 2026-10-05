using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;

namespace PSFoundation.Windows;

public enum SystemIntegrityStatus { Clean, Violations, CouldNotRun, Failed }
/// <summary>SFC verification output, native exit evidence, and a bounded supporting log excerpt.</summary>
public sealed class SystemIntegrityReport
{
    public SystemIntegrityStatus Status { get; }
    public int ExitCode { get; }
    public string Detail { get; }
    public IReadOnlyList<string> LogExcerpt { get; }
    internal SystemIntegrityReport(SystemIntegrityStatus status, int exitCode, string detail, string[] log) { Status = status; ExitCode = exitCode; Detail = detail; LogExcerpt = Array.AsReadOnly(log); }
}
/// <summary>Runs Windows system-file verification without requesting repair and classifies available diagnostics.</summary>
public sealed class SystemIntegrityManager
{
    /// <summary>Runs SFC verify-only. No repair or elevation occurs. Cancellation terminates only this verifier process.</summary>
    public SystemIntegrityReport Verify(CancellationToken cancellationToken = default) => VerifyAsync(cancellationToken).GetAwaiter().GetResult();
    public async Task<SystemIntegrityReport> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var result = await new ProcessManager().RunAsync(new ProcessRequest(Path.Combine(Environment.SystemDirectory, "sfc.exe"), new[] { "/verifyOnly" }, maximumCapturedCharacters: 1024 * 1024), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var tail = new Queue<string>();
        try
        {
            using (var reader = new StreamReader(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Logs\CBS\CBS.log")))
                while (!reader.EndOfStream)
                { cancellationToken.ThrowIfCancellationRequested(); tail.Enqueue(reader.ReadLine()!); if (tail.Count > 30) tail.Dequeue(); }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return Assess(result.StandardOutput + Environment.NewLine + result.StandardError, result.ExitCode, tail);
    }
    public SystemIntegrityReport Assess(string output, int exitCode, IEnumerable<string>? logExcerpt = null)
    {
        var log = (logExcerpt ?? Array.Empty<string>()).ToArray();
        var text = output + Environment.NewLine + string.Join(Environment.NewLine, log);
        var status = text.IndexOf("found corrupt files", StringComparison.OrdinalIgnoreCase) >= 0 ? SystemIntegrityStatus.Violations
            : text.IndexOf("did not find any integrity violations", StringComparison.OrdinalIgnoreCase) >= 0 ? SystemIntegrityStatus.Clean
            : text.IndexOf("could not perform the requested operation", StringComparison.OrdinalIgnoreCase) >= 0 ? SystemIntegrityStatus.CouldNotRun
            : exitCode == 0 ? SystemIntegrityStatus.Clean : exitCode == 1 ? SystemIntegrityStatus.Violations : exitCode == 2 ? SystemIntegrityStatus.CouldNotRun : SystemIntegrityStatus.Failed;
        return new SystemIntegrityReport(status, exitCode, status == SystemIntegrityStatus.Clean ? "no violations" : status == SystemIntegrityStatus.Violations ? "integrity violations found" : status == SystemIntegrityStatus.CouldNotRun ? "could not run" : "sfc exited with code " + exitCode, log);
    }
}
