using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;
using PSFoundation.Security;

namespace PSFoundation.Office;

public enum OdtMode { Download, Configure, Customize, Help }
/// <summary>Reviewed Microsoft ODT distribution metadata. Downloaded content must still pass signature and identity validation.</summary>
public sealed class OdtSource
{
    public Uri DownloadUri { get; }
    public Version Version { get; }
    public string Publisher => "Microsoft Corporation";
    public Uri Reference { get; }
    public DateTime ReviewedAtUtc { get; }
    public static OdtSource Reviewed { get; } = new OdtSource(
        new Uri("https://download.microsoft.com/download/6c1eeb25-cf8b-41d9-8d0d-cc1dbc032140/officedeploymenttool_20326-20112.exe"),
        new Version("16.0.20326.20112"), new Uri("https://www.microsoft.com/en-us/download/details.aspx?id=49117"), new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc));
    public OdtSource(Uri downloadUri, Version version, Uri reference, DateTime reviewedAtUtc)
    {
        if (downloadUri == null)
            throw new ArgumentNullException(nameof(downloadUri));
        if (!downloadUri.IsAbsoluteUri || downloadUri.Scheme != Uri.UriSchemeHttps || !downloadUri.IsDefaultPort || downloadUri.UserInfo.Length != 0
            || !downloadUri.Host.Equals("download.microsoft.com", StringComparison.OrdinalIgnoreCase) || downloadUri.Fragment.Length != 0)
            throw new ArgumentException("Use an HTTPS Microsoft download URI without credentials or a fragment.", nameof(downloadUri));
        DownloadUri = downloadUri;
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        if (reviewedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The review timestamp must be UTC.", nameof(reviewedAtUtc));
        ReviewedAtUtc = reviewedAtUtc;
    }
}
/// <summary>Source reachability evidence; successful HTTP status does not authenticate the distribution.</summary>
public sealed class OdtSourceAvailability
{
    public OdtSource Source { get; }
    public bool Available => Error == null && StatusCode >= 200 && StatusCode < 300;
    public int? StatusCode { get; }
    public long? ContentLength { get; }
    public DateTime CheckedAtUtc { get; }
    public Exception? Error { get; }
    internal OdtSourceAvailability(OdtSource source, int? status, long? length, Exception? error)
    { Source = source; StatusCode = status; ContentLength = length; Error = error; CheckedAtUtc = DateTime.UtcNow; }
}
/// <summary>Signature, identity, and version evidence for a candidate Office Deployment Tool executable.</summary>
public sealed class OdtAssessment
{
    public FileSystemPath Path { get; }
    public bool Valid { get; }
    public Version? Version { get; }
    public OfficeFailureReason? Reason => Valid ? (OfficeFailureReason?)null : OfficeFailureReason.UntrustedTool;
    public string? Detail { get; }
    public AuthenticodeVerification? Signature { get; }
    public string? OriginalFilename { get; }
    public string? FileDescription { get; }
    public string? ProductName { get; }
    public string? CompanyName { get; }
    internal OdtAssessment(FileSystemPath path, bool valid, string? detail, AuthenticodeVerification? signature, FileVersionInfo? info)
    {
        Path = path;
        Valid = valid;
        Detail = detail;
        Signature = signature;
        Version = info == null ? null : new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        OriginalFilename = info?.OriginalFilename;
        FileDescription = info?.FileDescription;
        ProductName = info?.ProductName;
        CompanyName = info?.CompanyName;
    }
}

/// <summary>Explicit Office Deployment Tool operations. Discovery never downloads or executes a tool.</summary>
public sealed partial class OdtTool
{
    private readonly HttpClient? client;
    /// <remarks>The optional HTTP client is borrowed; its transport configuration and lifetime belong to the caller.</remarks>
    public OdtTool(HttpClient? client = null) => this.client = client;
    /// <summary>Assesses a setup executable's signature, publisher, version, and expected Office identity.</summary>
    public OdtAssessment Check(FileSystemPath executable, SignatureNetworkAccess networkAccess = SignatureNetworkAccess.Online, CancellationToken cancellationToken = default)
    {
        if (executable == null)
            throw new ArgumentNullException(nameof(executable));
        if (!Enum.IsDefined(typeof(SignatureNetworkAccess), networkAccess))
            throw new ArgumentOutOfRangeException(nameof(networkAccess));
        cancellationToken.ThrowIfCancellationRequested();
        AuthenticodeVerification? signature = null;
        FileVersionInfo? info = null;
        try
        {
            new OfficePathGuard().ValidatePath(executable.Value, cancellationToken);
            if ((File.GetAttributes(executable.Value) & FileAttributes.Directory) != 0 || !string.Equals(System.IO.Path.GetExtension(executable.Value), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new IOException("OdtPath must identify an existing executable file.");
            using (var locked = new FileStream(executable.Value, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                signature = new AuthenticodeVerifier().Verify(executable, networkAccess, cancellationToken);
                info = FileVersionInfo.GetVersionInfo(executable.Value);
                var version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
                var legacy = Same(info.OriginalFilename, "setup.exe") && OfficeVersionResolver.IsMatch(info.FileDescription, "Office.*(Deployment|Click-to-Run)");
                var bootstrapper = Same(info.OriginalFilename, "Bootstrapper.exe") && Same(info.FileDescription, "Microsoft 365 and Office")
                    && Same(info.ProductName, "Microsoft Office") && Same(info.CompanyName, "Microsoft Corporation");
                string? detail = null;
                if (!signature.Valid)
                    detail = "ODT Authenticode signature is not valid (status: " + signature.Status + ").";
                else if (!OfficeVersionResolver.IsMatch(signature.Signer?.Subject, @"(?:^|,\s*)O=Microsoft Corporation(?:,|$)"))
                    detail = "ODT signer is not Microsoft Corporation.";
                else if (!legacy && !bootstrapper)
                    detail = "Unrecognized ODT identity: OriginalFilename='" + info.OriginalFilename + "', FileDescription='" + info.FileDescription
                    + "', ProductName='" + info.ProductName + "', CompanyName='" + info.CompanyName + "'. Use the extracted Office Deployment Tool setup.exe.";
                else if (version < new Version("16.0.12827.20258"))
                    detail = "ODT version " + version + " is below the supported minimum 16.0.12827.20258.";
                return new OdtAssessment(executable, detail == null, detail, signature, info);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return new OdtAssessment(executable, false, error.Message, signature, info); }
    }
    public Task<OdtAssessment> CheckAsync(FileSystemPath executable, SignatureNetworkAccess networkAccess = SignatureNetworkAccess.Online, CancellationToken cancellationToken = default)
        => Task.Run(() => Check(executable, networkAccess, cancellationToken), cancellationToken);
    /// <summary>Checks reachability only. Success is not content or publisher trust.</summary>
    public async Task<OdtSourceAvailability> CheckSourceAsync(TimeSpan timeout, OdtSource? source = null, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        source = source ?? OdtSource.Reviewed;
        cancellationToken.ThrowIfCancellationRequested();
        using (var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        using (var owned = client == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan } : null)
        {
            stopping.CancelAfter(timeout);
            try
            {
                using (var response = await GetMicrosoftResponseAsync(client ?? owned!, HttpMethod.Head, source.DownloadUri, stopping.Token).ConfigureAwait(false))
                {
                    return new OdtSourceAvailability(source, (int)response.StatusCode, response.Content.Headers.ContentLength, null);
                }
            }
            catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException || error is InvalidDataException)
            { cancellationToken.ThrowIfCancellationRequested(); return new OdtSourceAvailability(source, null, null, error); }
        }
    }
    /// <summary>Checks the configured Microsoft download source for reachability without authenticating its content.</summary>
    public OdtSourceAvailability CheckSource(TimeSpan timeout, OdtSource? source = null, CancellationToken cancellationToken = default)
        => CheckSourceAsync(timeout, source, cancellationToken).GetAwaiter().GetResult();
    private static bool Same(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
