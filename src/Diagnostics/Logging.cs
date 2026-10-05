using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Diagnostics;

public enum LogLevel { Trace, Debug, Information, Warning, Error, Critical }

/// <summary>An immutable event; properties are strings to avoid retaining arbitrary mutable application objects or formatting them implicitly.</summary>
public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; }
    public LogLevel Level { get; }
    public string Message { get; }
    public string? EventId { get; }
    public IReadOnlyDictionary<string, string?> Properties { get; }
    public LogEntry(DateTimeOffset timestamp, LogLevel level, string message, string? eventId = null, IReadOnlyDictionary<string, string?>? properties = null)
    {
        if (!Enum.IsDefined(typeof(LogLevel), level))
            throw new ArgumentOutOfRangeException(nameof(level));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Timestamp = timestamp;
        Level = level;
        EventId = eventId;
        var copy = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (properties != null)
            foreach (var pair in properties)
                copy.Add(pair.Key, pair.Value);
        Properties = new ReadOnlyDictionary<string, string?>(copy);
    }
}

/// <summary>A caller-provided destination for structured log events.</summary>
/// <remarks>Implementations define their concurrency and durability guarantees. Sink failures propagate to the caller.</remarks>
public interface ILogSink
{
    /// <summary>Writes an event using the destination's cancellation and persistence policy.</summary>
    Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>Explicit ordered fan-out. Sinks belong to the caller; sink errors propagate and already-written events cannot be rolled back.</summary>
public sealed class LogManager
{
    private readonly ILogSink[] sinks;
    public LogManager(IEnumerable<ILogSink> sinks)
    {
        this.sinks = (sinks ?? throw new ArgumentNullException(nameof(sinks))).ToArray();
        if (this.sinks.Any(s => s == null))
            throw new ArgumentException("Sinks cannot contain null.", nameof(sinks));
    }
    /// <summary>Writes to each configured sink in order, stopping at the first exception or cancellation.</summary>
    /// <remarks>Earlier sinks may already have persisted the event. The manager does not dispose sinks or retry writes.</remarks>
    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var sink in sinks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await sink.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }
    /// <summary>Blocks until the ordered sink writes complete; errors propagate without an aggregate wrapper.</summary>
    public void Write(LogEntry entry, CancellationToken cancellationToken = default) => WriteAsync(entry, cancellationToken).GetAwaiter().GetResult();
}

/// <summary>Serializes writes as JSON lines to a caller-selected writer. No console, path, timestamp, or redaction policy is inferred.</summary>
public sealed class JsonLineLogSink : ILogSink, IDisposable
{
    private readonly TextWriter writer;
    private readonly bool leaveOpen;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private bool disposed;
    /// <param name="writer">Destination writer. This sink serializes its own writes, not writes made through other references.</param>
    /// <param name="leaveOpen">Keep the supplied writer open when disposing the sink; defaults to true.</param>
    public JsonLineLogSink(TextWriter writer, bool leaveOpen = true)
    { this.writer = writer ?? throw new ArgumentNullException(nameof(writer)); this.leaveOpen = leaveOpen; }

    /// <summary>Appends one structured event as a JSON line, without automatic flushing.</summary>
    /// <remarks>Cancellation is observed before acquiring the write lock and before writing; an in-flight TextWriter write cannot be cancelled.
    /// A writer failure may leave a partial line. Disposal waits for an active write, rejects queued writes and honors leaveOpen.</remarks>
    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(JsonLineLogSink));
            cancellationToken.ThrowIfCancellationRequested();
            var text = new StringBuilder("{\"Timestamp\":");
            AppendString(text, entry.Timestamp.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            text.Append(",\"Level\":");
            AppendString(text, entry.Level.ToString());
            text.Append(",\"Message\":");
            AppendString(text, entry.Message);
            text.Append(",\"EventId\":");
            AppendString(text, entry.EventId);
            text.Append(",\"Properties\":{");
            var first = true;
            foreach (var pair in entry.Properties)
            {
                if (!first)
                    text.Append(',');
                first = false;
                AppendString(text, pair.Key);
                text.Append(':');
                AppendString(text, pair.Value);
            }
            text.Append("}}");
            await writer.WriteLineAsync(text.ToString()).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    /// <summary>Flushes the writer after active writes complete. Cancellation does not interrupt an in-flight writer flush.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(JsonLineLogSink));
            cancellationToken.ThrowIfCancellationRequested();
            await writer.FlushAsync().ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    /// <summary>Waits for an active write and closes the writer only when ownership was explicitly transferred.</summary>
    public void Dispose()
    {
        gate.Wait();
        try
        { if (!disposed) { disposed = true; if (!leaveOpen) writer.Dispose(); } }
        finally { gate.Release(); }
        // SemaphoreSlim has no allocated wait handle here. Keep it available for deterministic rejection of racing calls.
    }

    private static void AppendString(StringBuilder output, string? value)
    {
        if (value == null)
        { output.Append("null"); return; }
        output.Append('"');
        foreach (var c in value)
            if (c == '"' || c == '\\')
                output.Append('\\').Append(c);
            else if (c < ' ' || char.IsSurrogate(c))
                output.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            else
                output.Append(c);
        output.Append('"');
    }
}
