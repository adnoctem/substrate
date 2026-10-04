using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;
using PSFoundation.IO;
using PSFoundation.Security;

namespace PSFoundation.Office;

public sealed partial class OdtTool
{
    /// <summary>Verifies and invokes an explicit ODT mode without a shell, prompts or elevation.</summary>
    /// <remarks>Cancellation before launch prevents execution. Once launched, ODT is allowed to finish because it can delegate to shared
    /// services; a stop request is recorded in the result. Executable and configuration read leases remain held until completion.
    /// Output is drained with a one MiB capture limit per stream. There is no automatic retry or success-code interpretation.</remarks>
    public async Task<ProcessResult> InvokeAsync(FileSystemPath executable, OdtMode mode, FileSystemPath? configuration = null,
        CancellationToken cancellationToken = default)
    {
        if (executable == null)
            throw new ArgumentNullException(nameof(executable));
        if (!Enum.IsDefined(typeof(OdtMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == OdtMode.Help ? configuration != null : configuration == null)
            throw new ArgumentException("A configuration file is required only for download, configure and customize.", nameof(configuration));
        cancellationToken.ThrowIfCancellationRequested();
        var guard = new OfficePathGuard();
        using (var executableLease = OpenExecutable(executable, cancellationToken))
        {
            var assessment = await CheckAsync(executable, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!assessment.Valid)
                throw new OfficeException(OfficeFailureReason.UntrustedTool, "ODT signature or executable identity validation failed.");
            if (configuration != null)
                guard.RequireProtected(configuration.Value, cancellationToken: cancellationToken);
            using (var configurationLease = configuration == null ? null : new FileStream(configuration.Value, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var arguments = configuration == null ? new[] { "/help" } : new[] { "/" + mode.ToString().ToLowerInvariant(), configuration.Value };
                return await new ProcessManager().RunAsync(new ProcessRequest(executable.Value, arguments,
                    stopBehavior: ProcessStopBehavior.WaitForExit, maximumCapturedCharacters: 1024 * 1024), cancellationToken).ConfigureAwait(false);
            }
        }
    }
    public ProcessResult Invoke(FileSystemPath executable, OdtMode mode, FileSystemPath? configuration = null, CancellationToken cancellationToken = default)
        => InvokeAsync(executable, mode, configuration, cancellationToken).GetAwaiter().GetResult();
    public Task<ProcessResult> DownloadAsync(FileSystemPath executable, FileSystemPath configuration, CancellationToken cancellationToken = default)
        => InvokeAsync(executable, OdtMode.Download, configuration, cancellationToken);
    public Task<ProcessResult> ConfigureAsync(FileSystemPath executable, FileSystemPath configuration, CancellationToken cancellationToken = default)
        => InvokeAsync(executable, OdtMode.Configure, configuration, cancellationToken);
    public Task<ProcessResult> CustomizeAsync(FileSystemPath executable, FileSystemPath configuration, CancellationToken cancellationToken = default)
        => InvokeAsync(executable, OdtMode.Customize, configuration, cancellationToken);
    public Task<ProcessResult> HelpAsync(FileSystemPath executable, CancellationToken cancellationToken = default)
        => InvokeAsync(executable, OdtMode.Help, cancellationToken: cancellationToken);
    public ProcessResult Download(FileSystemPath executable, FileSystemPath configuration, CancellationToken cancellationToken = default)
        => DownloadAsync(executable, configuration, cancellationToken).GetAwaiter().GetResult();
    public ProcessResult Configure(FileSystemPath executable, FileSystemPath configuration, CancellationToken cancellationToken = default)
        => ConfigureAsync(executable, configuration, cancellationToken).GetAwaiter().GetResult();
    public ProcessResult Customize(FileSystemPath executable, FileSystemPath configuration, CancellationToken cancellationToken = default)
        => CustomizeAsync(executable, configuration, cancellationToken).GetAwaiter().GetResult();
    public ProcessResult Help(FileSystemPath executable, CancellationToken cancellationToken = default)
        => HelpAsync(executable, cancellationToken).GetAwaiter().GetResult();

    /// <summary>Downloads a reviewed Microsoft extractor, verifies it before extraction and publishes verified setup.exe without overwriting.</summary>
    /// <remarks>The destination parent must exist. Existing tools are verified and reused, never replaced. New working directories use
    /// protected ACLs from creation. Timeout bounds acquisition, not a running extractor. Cancellation after extraction prevents publication.
    /// A borrowed HTTP client's handler must disable automatic redirects to let this operation validate every hop; final URLs are always checked.</remarks>
    public async Task<OdtAssessment> InstallAsync(FileSystemPath destination, TimeSpan downloadTimeout, OdtSource? source = null,
        CancellationToken cancellationToken = default)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (downloadTimeout <= TimeSpan.Zero || downloadTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(downloadTimeout));
        cancellationToken.ThrowIfCancellationRequested();
        var guard = new OfficePathGuard();
        guard.ValidatePath(destination.Value, cancellationToken);
        var setup = destination.Combine("setup.exe");
        if (Exists(setup.Value))
            return RequireTrusted(await CheckAsync(setup, cancellationToken: cancellationToken).ConfigureAwait(false));
        var parent = Path.GetDirectoryName(destination.Value.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            throw new OfficeException(OfficeFailureReason.UnsafePath, "The destination parent must already exist.");
        // Validate an existing destination before doing network or process work. Never repair its ACL implicitly.
        if (Exists(destination.Value))
            guard.RequireProtected(destination.Value, cancellationToken: cancellationToken);
        var work = FileSystemPath.Parse(Path.Combine(parent, "PSFOfficeTool-" + Guid.NewGuid().ToString("N")));
        var created = false;
        Exception? operationFailure = null;
        try
        {
            guard.CreateProtectedDirectory(work.Value, cancellationToken);
            created = true;
            guard.RequireProtected(work.Value, cancellationToken: cancellationToken);
            var package = work.Combine("odt.exe");
            await DownloadExtractorAsync(package, source ?? OdtSource.Reviewed, downloadTimeout, cancellationToken).ConfigureAwait(false);
            var extracted = work.Combine("Extracted");
            guard.EnsureProtectedDirectory(extracted.Value, cancellationToken);
            using (var packageLease = new FileStream(package.Value, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var signature = await new AuthenticodeVerifier().VerifyAsync(package, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!signature.Valid || !OfficeVersionResolver.IsMatch(signature.Signer?.Subject, @"(?:^|,\s*)O=Microsoft Corporation(?:,|$)"))
                    throw new OfficeException(OfficeFailureReason.UntrustedTool, "ODT extractor did not pass Microsoft signature validation.");
                var result = await new ProcessManager().RunAsync(new ProcessRequest(package.Value, new[] { "/quiet", "/extract:" + extracted.Value },
                    stopBehavior: ProcessStopBehavior.WaitForExit, maximumCapturedCharacters: 65536), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (result.ExitCode != 0 || result.TimedOut || result.Cancelled)
                    throw new OfficeException(OfficeFailureReason.ToolExtractionFailed, "ODT extraction returned " + result.ExitCode + ".");
            }
            var candidate = extracted.Combine("setup.exe");
            guard.RequireProtected(candidate.Value, cancellationToken: cancellationToken);
            using (var content = new FileStream(candidate.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                RequireTrusted(await CheckAsync(candidate, cancellationToken: cancellationToken).ConfigureAwait(false));
                guard.EnsureProtectedDirectory(destination.Value, cancellationToken);
                if (Exists(setup.Value))
                    throw new OfficeException(OfficeFailureReason.Conflict, "Destination changed during extraction.");
                await new FileManager().WriteAtomicallyAsync(setup, content, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            // Publication has completed; finish its verification even if cancellation now arrives.
            return RequireTrusted(await CheckAsync(setup).ConfigureAwait(false));
        }
        catch (Exception error) { operationFailure = error; throw; }
        finally
        {
            if (created)
            {
                try
                { guard.RemoveWorkDirectory(work.Value, parent); }
                catch (Exception cleanup)
                {
                    if (operationFailure == null)
                        throw;
                    // Preserve the operation's reason code and attach bounded cleanup evidence, never replace the original failure.
                    operationFailure.Data["OfficeCleanupError"] = cleanup.Message;
                }
            }
        }
    }
    public OdtAssessment Install(FileSystemPath destination, TimeSpan downloadTimeout, OdtSource? source = null, CancellationToken cancellationToken = default)
        => InstallAsync(destination, downloadTimeout, source, cancellationToken).GetAwaiter().GetResult();

    private async Task DownloadExtractorAsync(FileSystemPath package, OdtSource source, TimeSpan timeout, CancellationToken cancellationToken)
    {
        const long maximumBytes = 64 * 1024 * 1024;
        using (var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        using (var owned = client == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan } : null)
        {
            stopping.CancelAfter(timeout);
            var http = client ?? owned!;
            using (var response = await GetMicrosoftResponseAsync(http, HttpMethod.Get, source.DownloadUri, stopping.Token).ConfigureAwait(false))
            {
                if (response.Content.Headers.ContentLength > maximumBytes)
                    throw new InvalidDataException("ODT download exceeds the 64 MiB limit.");
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(package.Value, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long length = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, 0, buffer.Length, stopping.Token).ConfigureAwait(false)) != 0)
                    {
                        length += count;
                        if (length > maximumBytes)
                            throw new InvalidDataException("ODT download exceeds the 64 MiB limit.");
                        await output.WriteAsync(buffer, 0, count, stopping.Token).ConfigureAwait(false);
                    }
                    if (length == 0 || response.Content.Headers.ContentLength.HasValue && length != response.Content.Headers.ContentLength.Value)
                        throw new InvalidDataException("ODT download is empty or incomplete.");
                    await output.FlushAsync(stopping.Token).ConfigureAwait(false);
                    output.Flush(true);
                }
            }
        }
    }
    private static async Task<HttpResponseMessage> GetMicrosoftResponseAsync(HttpClient http, HttpMethod method, Uri uri, CancellationToken cancellationToken)
    {
        for (var hop = 0; hop <= 5; hop++)
        {
            ValidateDownloadUri(uri);
            using (var request = new HttpRequestMessage(method, uri))
            {
                var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                try
                {
                    ValidateDownloadUri(response.RequestMessage?.RequestUri ?? uri);
                    var status = (int)response.StatusCode;
                    if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
                    {
                        var location = response.Headers.Location ?? throw new InvalidDataException("Microsoft download redirect has no destination.");
                        uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                        response.Dispose();
                        continue;
                    }
                    response.EnsureSuccessStatusCode();
                    return response;
                }
                catch { response.Dispose(); throw; }
            }
        }
        throw new InvalidDataException("Microsoft download exceeded five redirects.");
    }
    private static void ValidateDownloadUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || !uri.Host.Equals("download.microsoft.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The source redirected outside the trusted Microsoft HTTPS download host.");
    }
    private static OdtAssessment RequireTrusted(OdtAssessment result)
    { if (!result.Valid) throw new OfficeException(OfficeFailureReason.UntrustedTool, result.Detail ?? "ODT verification failed."); return result; }
    private static FileStream OpenExecutable(FileSystemPath executable, CancellationToken cancellationToken)
    {
        try
        {
            new OfficePathGuard().ValidatePath(executable.Value, cancellationToken);
            return new FileStream(executable.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { throw new OfficeException(OfficeFailureReason.UntrustedTool, "ODT signature or executable identity validation failed.", error); }
    }
    private static bool Exists(string path)
    {
        try
        { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
