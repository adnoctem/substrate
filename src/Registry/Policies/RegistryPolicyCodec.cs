using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace AdNoctem.Substrate.Registry.Policies;

/// <summary>A malformed registry.pol stream, with its failure location retained for diagnosis.</summary>
public sealed class RegistryPolicyFormatException : IOException
{
    public long Offset { get; }
    internal string? ReadOperation { get; }
    internal RegistryPolicyFormatException(long offset, Exception error, string? readOperation)
        : base(error.Message, error) { Offset = offset; ReadOperation = readOperation; }
}

/// <summary>PReg version 1 binary codec. Preserves record order and duplicates; never applies policy or invokes LGPO.</summary>
public sealed class RegistryPolicyCodec
{
    public const int MaximumFileSize = 64 * 1024 * 1024;
    public const int MaximumPayloadSize = 65535;

    /// <remarks>The caller owns the readable, seekable stream. Reads from its current position to its end and returns only after complete validation.
    /// Cancellation is checked between records; synchronous stream reads cannot be interrupted. Raw reads can disable typed payload validation.</remarks>
    public IReadOnlyList<RegistryPolicyRecord> Read(Stream stream, bool validatePayloads = true, CancellationToken cancellationToken = default)
        => ReadCore(stream, validatePayloads, false, cancellationToken);

    internal IReadOnlyList<RegistryPolicyRecord> ReadLegacy(Stream stream, bool validatePayloads, CancellationToken cancellationToken)
        => ReadCore(stream, validatePayloads, true, cancellationToken);

    private static IReadOnlyList<RegistryPolicyRecord> ReadCore(Stream stream, bool validatePayloads, bool legacyStringComparison, CancellationToken cancellationToken)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("A readable, seekable stream is required.", nameof(stream));
        cancellationToken.ThrowIfCancellationRequested();
        var start = stream.Position;
        var records = new List<RegistryPolicyRecord>();
        string? operation = null;
        using (var reader = new BinaryReader(stream, Encoding.Unicode, true))
        {
            uint Read32()
            { operation = "ReadUInt32"; var value = reader.ReadUInt32(); operation = null; return value; }
            ushort Read16()
            { operation = "ReadUInt16"; var value = reader.ReadUInt16(); operation = null; return value; }
            void Delimiter(char expected)
            { if (Read16() != expected) throw new InvalidDataException("Expected policy delimiter '" + expected + "'."); }
            string Identifier()
            {
                var text = new StringBuilder();
                while (true)
                {
                    var character = Read16();
                    if (character == 0)
                        return text.ToString();
                    if (text.Length >= 32767)
                        throw new InvalidDataException("Policy identifier exceeds 32767 characters.");
                    text.Append((char)character);
                }
            }
            try
            {
                if (stream.Length - start > MaximumFileSize)
                    throw new InvalidDataException("Policy file exceeds the 64 MiB limit.");
                if (Read32() != 0x67655250)
                    throw new InvalidDataException("Invalid PReg signature.");
                if (Read32() != 1)
                    throw new InvalidDataException("Unsupported PReg version (expected 1).");
                while (stream.Position < stream.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Delimiter('[');
                    var key = Identifier();
                    if (key.Length == 0)
                        throw new InvalidDataException("Policy key must not be empty.");
                    Delimiter(';');
                    var name = Identifier();
                    Delimiter(';');
                    var type = Read32();
                    Delimiter(';');
                    var size = Read32();
                    Delimiter(';');
                    if (size > MaximumPayloadSize || size > stream.Length - stream.Position - 2)
                        throw new InvalidDataException("Invalid or truncated policy payload size.");
                    operation = "ReadBytes";
                    var payload = reader.ReadBytes((int)size);
                    operation = null;
                    Delimiter(']');
                    var record = new RegistryPolicyRecord(key, name, type, payload, false);
                    if (validatePayloads)
                    {
                        operation = "GetString";
                        _ = record.Decode(legacyStringComparison);
                        operation = null;
                    }
                    records.Add(record);
                }
                return records.AsReadOnly();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { throw new RegistryPolicyFormatException(stream.Position - start, error, operation); }
        }
    }

    /// <summary>Validates and encodes the entire sequence before returning bytes. The caller owns the input sequence; payloads are copied into the result.</summary>
    public byte[] Encode(IEnumerable<RegistryPolicyRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));
        cancellationToken.ThrowIfCancellationRequested();
        using (var buffer = new MemoryStream())
        using (var writer = new BinaryWriter(buffer, Encoding.Unicode, true))
        {
            writer.Write(0x67655250u);
            writer.Write(1u);
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (record == null)
                    throw new ArgumentException("Policy records must not be null.", nameof(records));
                RegistryPolicyRecord.ValidateIdentifiers(record.Key, record.ValueName);
                var size = 24L + 2L * (record.Key.Length + record.ValueName.Length) + record.PayloadBytes.Length;
                if (buffer.Length + size > MaximumFileSize)
                    throw new ArgumentException("Policy file exceeds the 64 MiB limit.", nameof(records));
                writer.Write((ushort)'[');
                writer.Write(Encoding.Unicode.GetBytes(record.Key + '\0'));
                writer.Write((ushort)';');
                writer.Write(Encoding.Unicode.GetBytes(record.ValueName + '\0'));
                writer.Write((ushort)';');
                writer.Write(record.Type);
                writer.Write((ushort)';');
                writer.Write((uint)record.PayloadBytes.Length);
                writer.Write((ushort)';');
                writer.Write(record.PayloadBytes);
                writer.Write((ushort)']');
            }
            writer.Flush();
            return buffer.ToArray();
        }
    }
}
