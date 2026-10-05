using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AdNoctem.Substrate.Diagnostics.Tests;

public sealed class LoggingAndErrorTests
{
    [Theory]
    [InlineData("0x80073D06", 0x80073D06u)]
    [InlineData("-2147009274", 0x80073D06u)]
    [InlineData("4294967295", uint.MaxValue)]
    [InlineData("-2147483648", 0x80000000u)]
    public void ParsesSignedUnsignedAndHexCodes(string text, uint expected)
    { Assert.True(ErrorTranslator.TryParseCode(text, out var code)); Assert.Equal(expected, code); }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("-2147483649")]
    [InlineData("0x000000001")]
    [InlineData("+3010")]
    [InlineData("3010 unrelated")]
    public void RejectsInvalidCodes(string text) => Assert.False(ErrorTranslator.TryParseCode(text, out _));

    [Fact]
    public void StructuredHresultHasPriorityAndAmbiguousTextIsRejected()
    {
        var translator = new ErrorTranslator();
        var known = new COMException("0x80073CF0", unchecked((int)0x80073D06));
        Assert.Equal(0x80073D06u, translator.Translate(known)!.Code);
        Assert.Null(translator.Translate(new Exception("0x80073CF0 and 0x80073D06")));
        Assert.Null(translator.Translate(new Exception("3010 users")));
        Assert.Equal(0x80073D06u, translator.Translate(new Exception("0x80073D06 then 0x80073D06"))!.Code);
        Assert.True(translator.Translate(3010, ErrorDomain.Msi)!.Benign);
        Assert.False(translator.Translate(0x80073D06, ErrorDomain.Appx)!.Benign);
    }

    [Fact]
    public async Task ConcurrentLoggingKeepsCompleteEscapedRecordsAndCallerOwnership()
    {
        var writer = new StringWriter();
        var properties = new Dictionary<string, string?> { ["key"] = "old" };
        var entry = new LogEntry(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), LogLevel.Information, "quoted\"\nmessage", properties: properties);
        properties["key"] = "new";
        using (var sink = new JsonLineLogSink(writer))
        {
            await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => sink.WriteAsync(entry)));
            await sink.FlushAsync();
        }
        var lines = writer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(50, lines.Length);
        Assert.All(lines, line => { Assert.Contains("quoted\\\"\\u000amessage", line); Assert.Contains("\"key\":\"old\"", line); Assert.EndsWith("}}", line); });
        writer.Write("still open");
    }

    [Fact]
    public async Task DisposedAndCancelledLoggingDoesNotWrite()
    {
        var writer = new StringWriter();
        var entry = new LogEntry(DateTimeOffset.UtcNow, LogLevel.Error, "test");
        var sink = new JsonLineLogSink(writer);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sink.WriteAsync(entry, new CancellationToken(true)));
        sink.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sink.WriteAsync(entry));
        Assert.Equal("", writer.ToString());
    }
}
