using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSFoundation.IO;

/// <summary>Explicit filesystem operations. No parent directories or overwrite policy are inferred.</summary>
public sealed class FileManager
{
    /// <remarks>Writes to a sibling temporary file and publishes by rename or replacement. Cancellation before publication leaves the destination
    /// intact. Replacement is a single filesystem operation, not a transaction across files; the filesystem must support File.Replace.</remarks>
    public Task WriteAtomicallyAsync(FileSystemPath destination, Stream content, bool overwrite = false, CancellationToken cancellationToken = default)
        => WriteAtomicallyAsync(destination, content, overwrite, cancellationToken, null);

    // Supplies failure-stage evidence to the compatibility adapter without exposing PowerShell error semantics in this library.
    internal async Task WriteAtomicallyAsync(FileSystemPath destination, Stream content, bool overwrite, CancellationToken cancellationToken, Action<string, int>? beforeOperation)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (!content.CanRead)
            throw new ArgumentException("Content must be readable.", nameof(content));
        cancellationToken.ThrowIfCancellationRequested();
        var temporary = Path.Combine(Path.GetDirectoryName(destination.Value)!, ".psf-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            beforeOperation?.Invoke("Open", 4);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                beforeOperation?.Invoke("CopyTo", 1);
                await content.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
                beforeOperation?.Invoke("Flush", 0);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (overwrite && File.Exists(destination.Value))
            {
                beforeOperation?.Invoke("Replace", 3);
                File.Replace(temporary, destination.Value, null);
            }
            else
            {
                beforeOperation?.Invoke("Move", 2);
                File.Move(temporary, destination.Value);
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <remarks>The caller owns content; its position advances. The destination's parent must already exist.</remarks>
    public void WriteAtomically(FileSystemPath destination, Stream content, bool overwrite = false, CancellationToken cancellationToken = default)
        => WriteAtomicallyAsync(destination, content, overwrite, cancellationToken).GetAwaiter().GetResult();

    public async Task<string> ComputeSha256Async(FileSystemPath path, CancellationToken cancellationToken = default)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        cancellationToken.ThrowIfCancellationRequested();
        using (var input = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
        using (var hash = SHA256.Create())
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                hash.TransformBlock(buffer, 0, count, buffer, 0);
            cancellationToken.ThrowIfCancellationRequested();
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var text = new StringBuilder(64);
            foreach (var value in hash.Hash!)
                text.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }

    public string ComputeSha256(FileSystemPath path, CancellationToken cancellationToken = default)
        => ComputeSha256Async(path, cancellationToken).GetAwaiter().GetResult();
}
