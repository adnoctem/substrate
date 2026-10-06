using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Networking;

/// <summary>Downloads HTTPS content and publishes it only after checking a caller-supplied trusted SHA-256 digest.</summary>
public sealed class VerifiedDownloadService
{
    private readonly HttpClient client;

    /// <remarks>The caller owns the client, its handler, proxy and credential policy. Certificate validation must remain enabled.
    /// This service never disposes the supplied client and does not execute or extract downloaded content.</remarks>
    public VerifiedDownloadService(HttpClient client) =>
        this.client = client ?? throw new ArgumentNullException(nameof(client));

    /// <remarks>The expected hash must come from an independently trusted source. HTTPS availability alone does not establish trust.
    /// The parent directory must exist. Cancellation, size violations, transport failures and hash mismatches leave the destination unchanged.
    /// Timeout bounds headers and body together; publication is one filesystem operation and cannot be interrupted once started.</remarks>
    public async Task DownloadAsync(
        Uri source,
        FileSystemPath destination,
        string trustedSha256,
        long maximumBytes,
        TimeSpan timeout,
        bool overwrite = false,
        CancellationToken cancellationToken = default
    )
    {
        if (
            source == null
            || !source.IsAbsoluteUri
            || source.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(source.UserInfo)
        )
            throw new ArgumentException(
                "An absolute HTTPS URI without embedded credentials is required.",
                nameof(source)
            );

        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        if (
            trustedSha256 == null
            || trustedSha256.Length != 64
            || !trustedSha256.All(Uri.IsHexDigit)
        )
            throw new ArgumentException(
                "An independently trusted SHA-256 digest of 64 hexadecimal characters is required.",
                nameof(trustedSha256)
            );

        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        cancellationToken.ThrowIfCancellationRequested();

        if (!overwrite && File.Exists(destination.Value))
            throw new IOException("The download destination already exists.");

        var temporary = Path.Combine(
            Path.GetDirectoryName(destination.Value)!,
            ".psf-download-" + Guid.NewGuid().ToString("N") + ".tmp"
        );
        var ownsTemporary = false;

        using (var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            stopping.CancelAfter(timeout);
            var token = stopping.Token;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, source))
                using (
                    var response = await client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                        .ConfigureAwait(false)
                )
                {
                    response.EnsureSuccessStatusCode();
                    var finalUri = response.RequestMessage?.RequestUri;

                    if (
                        finalUri != null
                        && (
                            finalUri.Scheme != Uri.UriSchemeHttps
                            || !string.IsNullOrEmpty(finalUri.UserInfo)
                        )
                    )
                        throw new InvalidDataException(
                            "The download redirected to an insecure URI."
                        );

                    if (response.Content.Headers.ContentLength > maximumBytes)
                        throw new InvalidDataException("The download exceeds the size limit.");

                    using (
                        var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)
                    )
                    using (
                        var output = new FileStream(
                            temporary,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            81920,
                            true
                        )
                    )
                    {
                        ownsTemporary = true;

                        using var hash = SHA256.Create();

                        long length = 0;
                        var buffer = new byte[81920];
                        int count;

                        while (
                            (
                                count = await input
                                    .ReadAsync(buffer, 0, buffer.Length, token)
                                    .ConfigureAwait(false)
                            ) != 0
                        )
                        {
                            if (count > maximumBytes - length)
                                throw new InvalidDataException(
                                    "The download exceeds the size limit."
                                );

                            length += count;
                            hash.TransformBlock(buffer, 0, count, buffer, 0);
                            await output.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
                        }

                        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        var actual = BitConverter.ToString(hash.Hash!).Replace("-", "");

                        if (
                            !string.Equals(
                                actual,
                                trustedSha256,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                            throw new InvalidDataException(
                                "The download SHA-256 does not match the trusted digest."
                            );

                        await output.FlushAsync(token).ConfigureAwait(false);
                        output.Flush(true);
                    }
                }

                token.ThrowIfCancellationRequested();

                if (overwrite && File.Exists(destination.Value))
                    File.Replace(temporary, destination.Value, null);
                else
                    File.Move(temporary, destination.Value);
            }
            finally
            {
                if (ownsTemporary && File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
