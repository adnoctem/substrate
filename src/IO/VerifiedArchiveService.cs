using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.IO;

/// <summary>Publishes one exact ZIP entry after independently verifying the archive and extracted file. Never executes content.</summary>
public sealed class VerifiedArchiveService
{
    /// <summary>Extracts one exact entry after enforcing archive and file digests, path rules, and size limits.</summary>
    /// <remarks>Both digests must be independently trusted. No archive paths become filesystem paths. The destination's parent must exist.
    /// Verification uses the same open archive handle as extraction. Failure leaves an existing destination unchanged.</remarks>
    public void ExtractFile(
        FileSystemPath archive,
        FileSystemPath destination,
        string entryPath,
        string trustedArchiveSha256,
        string trustedFileSha256,
        long maximumArchiveBytes,
        long maximumFileBytes,
        bool overwrite = false,
        CancellationToken cancellationToken = default
    )
    {
        if (archive == null)
            throw new ArgumentNullException(nameof(archive));

        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        ValidateEntryPath(entryPath);
        ValidateHash(trustedArchiveSha256);
        ValidateHash(trustedFileSha256);

        if (maximumArchiveBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumArchiveBytes));

        if (maximumFileBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));

        cancellationToken.ThrowIfCancellationRequested();

        if (!overwrite && File.Exists(destination.Value))
            throw new IOException("The extraction destination already exists.");

        var temporary = Path.Combine(
            Path.GetDirectoryName(destination.Value)!,
            ".psf-extract-" + Guid.NewGuid().ToString("N") + ".tmp"
        );
        var ownsTemporary = false;

        try
        {
            using (
                var input = new FileStream(
                    archive.Value,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                )
            )
            {
                CopyVerified(
                    input,
                    Stream.Null,
                    trustedArchiveSha256,
                    maximumArchiveBytes,
                    cancellationToken
                );
                input.Position = 0;

                using (var zip = new ZipArchive(input, ZipArchiveMode.Read, true))
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var candidate in zip.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ValidateEntryPath(candidate.FullName.TrimEnd('/'));

                        if (!names.Add(candidate.FullName))
                            throw new InvalidDataException(
                                "The archive contains duplicate entry paths."
                            );
                    }

                    var entries = zip
                        .Entries.Where(entry =>
                            string.Equals(entry.FullName, entryPath, StringComparison.Ordinal)
                        )
                        .ToArray();

                    if (entries.Length != 1)
                        throw new InvalidDataException(
                            "The expected archive entry was not found exactly once."
                        );

                    if (entries[0].Length > maximumFileBytes)
                        throw new InvalidDataException("The archive entry exceeds the size limit.");

                    using (var source = entries[0].Open())
                    using (
                        var output = new FileStream(
                            temporary,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None
                        )
                    )
                    {
                        ownsTemporary = true;
                        CopyVerified(
                            source,
                            output,
                            trustedFileSha256,
                            maximumFileBytes,
                            cancellationToken
                        );
                        output.Flush(true);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

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

    /// <summary>Performs verified extraction on a worker thread.</summary>
    /// <remarks>Offloads synchronous filesystem and ZIP operations. Cancellation is checked between reads and before atomic publication.</remarks>
    public Task ExtractFileAsync(
        FileSystemPath archive,
        FileSystemPath destination,
        string entryPath,
        string trustedArchiveSha256,
        string trustedFileSha256,
        long maximumArchiveBytes,
        long maximumFileBytes,
        bool overwrite = false,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
                ExtractFile(
                    archive,
                    destination,
                    entryPath,
                    trustedArchiveSha256,
                    trustedFileSha256,
                    maximumArchiveBytes,
                    maximumFileBytes,
                    overwrite,
                    cancellationToken
                ),
            cancellationToken
        );

    private static void CopyVerified(
        Stream input,
        Stream output,
        string expected,
        long limit,
        CancellationToken token
    )
    {
        using (var hash = SHA256.Create())
        {
            var buffer = new byte[81920];
            long length = 0;

            while (true)
            {
                token.ThrowIfCancellationRequested();
                var count = input.Read(buffer, 0, buffer.Length);

                if (count == 0)
                    break;

                if (count > limit - length)
                    throw new InvalidDataException("The content exceeds the size limit.");

                length += count;
                hash.TransformBlock(buffer, 0, count, buffer, 0);
                output.Write(buffer, 0, count);
            }

            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            if (
                !string.Equals(
                    BitConverter.ToString(hash.Hash!).Replace("-", ""),
                    expected,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new InvalidDataException(
                    "The content SHA-256 does not match the trusted digest."
                );
        }
    }

    private static void ValidateHash(string hash)
    {
        if (hash == null || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new ArgumentException("A trusted SHA-256 digest is required.", nameof(hash));
    }

    private static void ValidateEntryPath(string path)
    {
        if (
            string.IsNullOrWhiteSpace(path)
            || path.IndexOfAny(new[] { '\\', ':', '\0' }) >= 0
            || path.Any(char.IsControl)
            || path.Split('/')
                .Any(segment => segment.Length == 0 || segment == "." || segment == "..")
        )
            throw new InvalidDataException(
                "Archive entries must use canonical relative ZIP paths."
            );
    }
}
