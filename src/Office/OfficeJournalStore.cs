using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Office;

/// <summary>Bounded protected JSON journal I/O. Parsing a document does not validate recovery authority or fingerprints.</summary>
public sealed class OfficeJournalStore
{
    /// <remarks>The caller chooses the serialization contract. Writes are atomic and never create the parent or repair its security.</remarks>
    public async Task WriteAsync(
        FileSystemPath path,
        string json,
        bool overwrite = false,
        CancellationToken cancellationToken = default
    )
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        if (json == null)
            throw new ArgumentNullException(nameof(json));

        cancellationToken.ThrowIfCancellationRequested();
        var guard = new OfficePathGuard();
        guard.ValidatePath(path.Value, cancellationToken);
        guard.RequireProtected(
            Path.GetDirectoryName(path.Value)!,
            "RecoveryDirectory",
            cancellationToken
        );

        if (Exists(path.Value))
            guard.RequireProtected(path.Value, "RecoveryJournal", cancellationToken);

        try
        {
            if (OfficeJsonContract.Read(json).GetAttribute("type") != "object")
                throw new OfficeException(
                    OfficeFailureReason.InvalidRecoveryRecord,
                    "A journal must be a JSON object."
                );
        }
        catch (XmlException)
        {
            throw new OfficeException(
                OfficeFailureReason.InvalidRecoveryRecord,
                "Journal JSON could not be parsed."
            );
        }

        using (var input = new MemoryStream(new UTF8Encoding(false, true).GetBytes(json)))
            await new FileManager()
                .WriteAtomicallyAsync(path, input, overwrite, cancellationToken)
                .ConfigureAwait(false);
    }

    public void Write(
        FileSystemPath path,
        string json,
        bool overwrite = false,
        CancellationToken cancellationToken = default
    ) => WriteAsync(path, json, overwrite, cancellationToken).GetAwaiter().GetResult();

    /// <summary>Reads text only after checking both the parent and journal. The caller must validate its schema and identity before use.</summary>
    public async Task<string> ReadAsync(
        FileSystemPath path,
        CancellationToken cancellationToken = default
    )
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        var guard = new OfficePathGuard();
        guard.RequireProtected(
            Path.GetDirectoryName(path.Value)!,
            "RecoveryDirectory",
            cancellationToken
        );
        guard.RequireProtected(path.Value, "RecoveryJournal", cancellationToken);

        using (
            var input = new FileStream(
                path.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                true
            )
        )
        {
            if (input.Length > OfficeMediaManifest.MaximumJsonBytes)
                throw new OfficeException(
                    OfficeFailureReason.InvalidRecoveryRecord,
                    "Recovery journal exceeds the 16 MiB limit."
                );

            using (var content = new MemoryStream())
            {
                await input.CopyToAsync(content, 81920, cancellationToken).ConfigureAwait(false);

                return new UTF8Encoding(false, true)
                    .GetString(content.ToArray())
                    .TrimStart('\uFEFF');
            }
        }
    }

    public string Read(FileSystemPath path, CancellationToken cancellationToken = default) =>
        ReadAsync(path, cancellationToken).GetAwaiter().GetResult();

    private static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);

            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
