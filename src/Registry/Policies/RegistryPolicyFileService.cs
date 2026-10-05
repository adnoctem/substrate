using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Registry.Policies;

/// <summary>Reads and atomically publishes registry.pol files. Machine/User scope is supplied by the caller, never inferred or applied.</summary>
public sealed class RegistryPolicyFileService
{
    public IReadOnlyList<RegistryPolicyRecord> Read(FileSystemPath path, bool validatePayloads = true, CancellationToken cancellationToken = default)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        cancellationToken.ThrowIfCancellationRequested();
        using (var stream = File.OpenRead(path.Value))
            return new RegistryPolicyCodec().Read(stream, validatePayloads, cancellationToken);
    }

    public async Task<IReadOnlyList<RegistryPolicyRecord>> ReadAsync(FileSystemPath path, bool validatePayloads = true, CancellationToken cancellationToken = default)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        cancellationToken.ThrowIfCancellationRequested();
        using (var stream = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
        using (var memory = new MemoryStream())
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (memory.Length + count > RegistryPolicyCodec.MaximumFileSize)
                    throw new RegistryPolicyFormatException(0, new InvalidDataException("Policy file exceeds the 64 MiB limit."), null);
                memory.Write(buffer, 0, count);
            }
            memory.Position = 0;
            return new RegistryPolicyCodec().Read(memory, validatePayloads, cancellationToken);
        }
    }

    /// <remarks>All records are validated before touching the destination. Cancellation before publication preserves it; the parent directory must exist.</remarks>
    public async Task WriteAsync(FileSystemPath path, IEnumerable<RegistryPolicyRecord> records, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        var bytes = new RegistryPolicyCodec().Encode(records, cancellationToken);
        using (var content = new MemoryStream(bytes, false))
            await new FileManager().WriteAtomicallyAsync(path, content, overwrite, cancellationToken).ConfigureAwait(false);
    }

    public void Write(FileSystemPath path, IEnumerable<RegistryPolicyRecord> records, bool overwrite = false, CancellationToken cancellationToken = default)
        => WriteAsync(path, records, overwrite, cancellationToken).GetAwaiter().GetResult();
}
