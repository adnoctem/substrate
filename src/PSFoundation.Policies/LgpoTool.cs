using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;
using PSFoundation.IO;
using PSFoundation.Networking;

namespace PSFoundation.Policies;

public enum LgpoApplyMode { TextSource, GpoBackup }

/// <summary>Explicit download, installation and policy execution operations. Applying policy never installs a tool or elevates.</summary>
public sealed class LgpoTool
{
    private readonly HttpClient? client;
    /// <remarks>The optional HTTP client is borrowed and required only for network operations. The caller owns transport configuration and disposal.</remarks>
    public LgpoTool(HttpClient? client = null) { this.client = client; }

    /// <summary>Downloads the pinned archive and publishes only its verified executable. Never runs LGPO or changes policy.</summary>
    /// <remarks>The caller chooses the destination and overwrite policy. OS filesystem permissions decide access; no administrator check or elevation is implicit.
    /// Timeout applies to the complete download/extraction operation. An existing destination is replaced only after both hashes match.</remarks>
    public async Task<FileSystemPath> InstallAsync(FileSystemPath directory, TimeSpan timeout, bool overwrite = false,
        LgpoSource? source = null, CancellationToken cancellationToken = default)
    {
        if (directory == null)
            throw new ArgumentNullException(nameof(directory));
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var http = client ?? throw new InvalidOperationException("Supply an HTTP client for installation.");
        source = source ?? LgpoSource.Standalone;
        var executable = directory.Combine("LGPO.exe");
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(executable.Value) && !overwrite)
            throw new IOException("The installation destination already exists.");
        Directory.CreateDirectory(directory.Value);
        var archive = directory.Combine(".psf-lgpo-" + Guid.NewGuid().ToString("N") + ".zip");
        using (var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            stopping.CancelAfter(timeout);
            try
            {
                await new VerifiedDownloadService(http).DownloadAsync(source.DownloadUri, archive, source.TrustedSha256, 64 * 1024 * 1024,
                    timeout, cancellationToken: stopping.Token).ConfigureAwait(false);
                await new VerifiedArchiveService().ExtractFileAsync(archive, executable, source.ExpectedBinaryPath, source.TrustedSha256,
                    source.TrustedBinarySha256, 64 * 1024 * 1024, 16 * 1024 * 1024, overwrite, stopping.Token).ConfigureAwait(false);
                return executable;
            }
            finally { if (File.Exists(archive.Value)) File.Delete(archive.Value); }
        }
    }

    /// <summary>Downloads and installs a digest-verified LGPO executable without applying policy.</summary>
    public FileSystemPath Install(FileSystemPath directory, TimeSpan timeout, bool overwrite = false,
        LgpoSource? source = null, CancellationToken cancellationToken = default)
        => InstallAsync(directory, timeout, overwrite, source, cancellationToken).GetAwaiter().GetResult();

    /// <summary>Checks source reachability; success does not authenticate downloaded content.</summary>
    public LgpoSourceAvailability CheckSource(TimeSpan timeout, LgpoSource? source = null, CancellationToken cancellationToken = default)
        => CheckSourceAsync(timeout, source, cancellationToken).GetAwaiter().GetResult();

    /// <summary>Checks reachability only. A successful HEAD response makes no claim about content integrity.</summary>
    public async Task<LgpoSourceAvailability> CheckSourceAsync(TimeSpan timeout, LgpoSource? source = null, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var http = client ?? throw new InvalidOperationException("Supply an HTTP client for availability checks.");
        source = source ?? LgpoSource.Standalone;
        cancellationToken.ThrowIfCancellationRequested();
        using (var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            stopping.CancelAfter(timeout);
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, source.DownloadUri))
                using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stopping.Token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var finalUri = response.RequestMessage?.RequestUri;
                    if (finalUri != null && (finalUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(finalUri.UserInfo)))
                        throw new InvalidDataException("The source redirected to an insecure URI.");
                    return new LgpoSourceAvailability(source.DownloadUri, (int)response.StatusCode, response.Content.Headers.ContentLength, null);
                }
            }
            catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException || error is InvalidDataException)
            { cancellationToken.ThrowIfCancellationRequested(); return new LgpoSourceAvailability(source.DownloadUri, null, null, error); }
        }
    }

    /// <summary>Checks file existence only. The caller must establish executable trust before execution.</summary>
    public bool IsInstalled(FileSystemPath executable)
        => File.Exists((executable ?? throw new ArgumentNullException(nameof(executable))).Value);

    /// <summary>Selects GPO backup mode for a directory and text mode for a file.</summary>
    public LgpoApplyMode GetApplyMode(FileSystemPath policyPath)
    {
        if (policyPath == null)
            throw new ArgumentNullException(nameof(policyPath));
        var attributes = File.GetAttributes(policyPath.Value);
        return (attributes & FileAttributes.Directory) != 0 ? LgpoApplyMode.GpoBackup : LgpoApplyMode.TextSource;
    }

    /// <summary>Prepares the executable, arguments, timeout, and stop policy for an explicit policy application.</summary>
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

    /// <summary>Runs the prepared LGPO operation and returns native execution evidence without interpreting a nonzero exit as success.</summary>
    /// <remarks>Only an explicit apply call changes policy. Nonzero exit codes, cancellation and timeouts are returned as process evidence.
    /// With WaitForExit, cancellation after launch does not terminate the policy operation; it may wait indefinitely.</remarks>
    public Task<ProcessResult> ApplyAsync(FileSystemPath executable, FileSystemPath policyPath, TimeSpan timeout, ProcessStopBehavior stopBehavior,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessManager().RunAsync(CreateApplyRequest(executable, policyPath, timeout, stopBehavior), cancellationToken);
    }

    /// <summary>Synchronously applies local policy with the caller-selected interruption policy.</summary>
    public ProcessResult Apply(FileSystemPath executable, FileSystemPath policyPath, TimeSpan timeout, ProcessStopBehavior stopBehavior,
        CancellationToken cancellationToken = default)
        => ApplyAsync(executable, policyPath, timeout, stopBehavior, cancellationToken).GetAwaiter().GetResult();
}
