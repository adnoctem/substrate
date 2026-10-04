using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using PSFoundation.IO;
using PSFoundation.Registry.Policies;
using Xunit;

namespace PSFoundation.Registry.Tests;

public sealed class RegistryPolicyTests
{
    [Fact]
    public void MatchesTheIndependentlyRecordedPRegFixtureAndPreservesOrder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PSFoundation.slnx")))
            directory = directory.Parent;
        var hex = File.ReadAllText(Path.Combine(directory!.FullName, "tests", "fixtures", "policies", "ordered-dword.hex"));
        var digits = new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var bytes = Enumerable.Range(0, digits.Length / 2).Select(i => Convert.ToByte(digits.Substring(i * 2, 2), 16)).ToArray();
        using (var stream = new MemoryStream(bytes))
        {
            var records = new RegistryPolicyCodec().Read(stream);
            Assert.Equal(bytes, new RegistryPolicyCodec().Encode(records));
            Assert.True(stream.CanRead);
            Assert.NotEmpty(records);
        }
    }

    public static IEnumerable<object[]> PayloadCases => new[]
    {
        new object[] { 1u, "" }, new object[] { 1u, "Grüße 日本語" }, new object[] { 2u, "%TEMP%\\literal" },
        new object[] { 3u, new byte[] { 0, 255, 59, 93 } }, new object[] { 4u, uint.MaxValue }, new object[] { 5u, 0x12345678u },
        new object[] { 7u, Array.Empty<string>() }, new object[] { 7u, new[] { "one", "two" } }, new object[] { 11u, ulong.MaxValue },
        new object[] { 42u, new byte[] { 1, 2, 3 } }
    };

    [Theory]
    [MemberData(nameof(PayloadCases))]
    public void TypedPayloadRoundTrips(uint type, object data)
    {
        var record = RegistryPolicyRecord.FromDecoded("Software\\Synthetic", "Value", type, data);
        using (var stream = new MemoryStream(new RegistryPolicyCodec().Encode(new[] { record, record })))
        {
            var records = new RegistryPolicyCodec().Read(stream);
            Assert.Equal(2, records.Count);
            Assert.Equal(data, records[0].Decode());
            Assert.Equal(data, records[1].Decode());
        }
    }

    [Fact]
    public void RawRecordsAreImmutableAndMalformedTypedPayloadCanRoundTripExplicitly()
    {
        var source = new byte[] { 65 };
        var record = new RegistryPolicyRecord("K", "", 1, source);
        source[0] = 0;
        record.Payload[0] = 0;
        Assert.Equal(new byte[] { 65 }, record.Payload);
        var bytes = new RegistryPolicyCodec().Encode(new[] { record });
        using (var stream = new MemoryStream(bytes))
        {
            var error = Assert.Throws<RegistryPolicyFormatException>(() => new RegistryPolicyCodec().Read(stream));
            Assert.Contains("odd byte count", error.Message);
            Assert.True(error.Offset > 8);
            stream.Position = 0;
            Assert.Equal(bytes, new RegistryPolicyCodec().Encode(new RegistryPolicyCodec().Read(stream, false)));
        }
    }

    [Fact]
    public void EveryTruncatedRecordFailsWithoutReturningPartialRecords()
    {
        var bytes = new RegistryPolicyCodec().Encode(new[] { RegistryPolicyRecord.FromDecoded("K", "V", 4, 1u) });
        for (var length = 0; length < bytes.Length; length++)
        {
            if (length == 8)
                continue; // A header-only file is a valid empty policy.
            using (var stream = new MemoryStream(bytes.Take(length).ToArray()))
                Assert.Throws<RegistryPolicyFormatException>(() => new RegistryPolicyCodec().Read(stream));
        }
    }

    [Fact]
    public void UsesSharedRegistryModelsWithoutLosingUnsignedIntegerBits()
    {
        var record = RegistryPolicyRecord.FromRegistryValue("Software\\Synthetic", new RegistryValueEntry("Value", RegistryValue.DWord(-1)));
        Assert.Equal(uint.MaxValue, record.Decode());
        Assert.True(record.TryGetRegistryValue(out var value));
        Assert.Equal(-1, value!.GetData<int>());
        Assert.Equal(RegistryPath.Parse("HKCU\\Software\\Synthetic"), record.GetRegistryPath(RegistryHive.CurrentUser));
        Assert.False(new RegistryPolicyRecord("K", "V", 42, new byte[] { 1 }).TryGetRegistryValue(out _));
    }

    [Fact]
    public void RejectsUnterminatedStringsRegardlessOfHostGlobalization()
    {
        var record = new RegistryPolicyRecord("K", "V", 1, new byte[] { 65, 0 });
        Assert.Throws<InvalidDataException>(() => record.Decode());
        Assert.False(record.TryGetRegistryValue(out _));
    }

    [Fact]
    public void CancellationAndOffsetsRespectCallerOwnedStreams()
    {
        var codec = new RegistryPolicyCodec();
        using (var stream = new MemoryStream(new byte[] { 99, 99, 80 }))
        {
            stream.Position = 2;
            Assert.ThrowsAny<OperationCanceledException>(() => codec.Read(stream, cancellationToken: new CancellationToken(true)));
            Assert.Equal(2, stream.Position);
            var error = Assert.Throws<RegistryPolicyFormatException>(() => codec.Read(stream));
            Assert.Equal(1, error.Offset);
            Assert.True(stream.CanRead);
        }
    }

    [Fact]
    public void EnforcesPayloadLimitsBeforeGrowingAnUnboundedMultiString()
    {
        IEnumerable<string> Repeated()
        { while (true) yield return "x"; }
        Assert.Throws<ArgumentException>(() => RegistryPolicyRecord.FromDecoded("K", "V", 7, Repeated()));
        Assert.Throws<ArgumentException>(() => RegistryPolicyRecord.FromDecoded("K", "V", 1, new string('x', 32767)));
        Assert.Equal(65534, RegistryPolicyRecord.FromDecoded("K", "V", 1, new string('x', 32766)).Payload.Length);
        Assert.Equal(65534, RegistryPolicyRecord.FromDecoded("K", "V", 7, new string('x', 32765)).Payload.Length);
    }

    [Fact]
    public async Task FilePublicationPreservesTheExistingDestinationOnFailureOrCancellation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "psf-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = FileSystemPath.Parse(Path.Combine(directory, "registry.pol"));
        try
        {
            var service = new RegistryPolicyFileService();
            await service.WriteAsync(path, Array.Empty<RegistryPolicyRecord>());
            var before = File.ReadAllBytes(path.Value);
            await Assert.ThrowsAsync<IOException>(() => service.WriteAsync(path, Array.Empty<RegistryPolicyRecord>()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.WriteAsync(path, Array.Empty<RegistryPolicyRecord>(), true, new CancellationToken(true)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.WriteAsync(path, new RegistryPolicyRecord[] { null! }, true));
            Assert.Equal(before, File.ReadAllBytes(path.Value));
            await service.WriteAsync(path, new[] { RegistryPolicyRecord.FromDecoded("K", "**Del.Flag", 1, "") }, true);
            Assert.Single(await service.ReadAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }
}
