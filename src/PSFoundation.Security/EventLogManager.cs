using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;

namespace PSFoundation.Security;

public sealed class WindowsEventSnapshot
{
    public int Id { get; }
    public long? RecordId { get; }
    public DateTime? TimeCreated { get; }
    public string? ProviderName { get; }
    public string? LogName { get; }
    public string? MachineName { get; }
    public byte? Level { get; }
    public int? ProcessId { get; }
    public int? ThreadId { get; }
    public string? UserId { get; }
    public string Xml { get; }
    public string? Message { get; }
    public Exception? MessageError { get; }
    public WindowsEventPayload Payload { get; }
    internal WindowsEventSnapshot(EventRecord record, bool includeMessage)
    {
        Id = record.Id;
        RecordId = record.RecordId;
        TimeCreated = record.TimeCreated;
        ProviderName = record.ProviderName;
        LogName = record.LogName;
        MachineName = record.MachineName;
        Level = record.Level;
        ProcessId = record.ProcessId;
        ThreadId = record.ThreadId;
        UserId = record.UserId?.Value;
        Xml = record.ToXml();
        Payload = WindowsEventPayload.Parse(Xml);
        if (includeMessage)
        {
            try
            { Message = record.FormatDescription(); }
            catch (EventLogException error) { MessageError = error; }
        }
    }
}

/// <summary>Owns one native event reader. Use sequentially; concurrent reads or disposal are not supported.</summary>
public sealed class EventLogReadSession : IDisposable
{
    private EventLogReader? reader;
    private readonly int maximumEvents;
    private int returned;
    internal EventLogReadSession(WindowsEventQuery query, EventLogSession session)
    {
        maximumEvents = query.MaximumEvents;
        reader = new EventLogReader(new EventLogQuery(query.Source, query.SourceKind == WindowsEventSourceKind.Channel ? PathType.LogName : PathType.FilePath, query.ToQueryXml())
        { Session = session, ReverseDirection = query.NewestFirst });
    }
    /// <summary>Returns a caller-owned native record, or null at the end/limit. Dispose each returned record; keep a borrowed remote session alive while using it.</summary>
    public EventRecord? ReadNextRecord(CancellationToken cancellationToken = default)
    {
        var active = reader ?? throw new ObjectDisposedException(nameof(EventLogReadSession));
        cancellationToken.ThrowIfCancellationRequested();
        if (returned >= maximumEvents)
            return null;
        using (cancellationToken.Register(() => { try { active.CancelReading(); } catch (ObjectDisposedException) { } catch (EventLogException) { } }))
        {
            EventRecord? record = null;
            try
            {
                record = active.ReadEvent();
                cancellationToken.ThrowIfCancellationRequested();
                if (record != null)
                    returned++;
                return record;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            { record?.Dispose(); throw new OperationCanceledException(cancellationToken); }
        }
    }
    public void Dispose() { reader?.Dispose(); reader = null; }
}

/// <summary>Native Windows Event Log operations. A supplied session is borrowed; otherwise the caller's local identity is used.</summary>
public sealed class EventLogManager
{
    private readonly EventLogSession session;
    public EventLogManager(EventLogSession? session = null) => this.session = session ?? EventLogSession.GlobalSession;
    public IReadOnlyList<string> GetChannels() => Array.AsReadOnly(session.GetLogNames().ToArray());
    /// <summary>Only a missing channel returns false. Access and transport failures remain failures.</summary>
    public bool ChannelExists(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("An exact channel name is required.", nameof(channel));
        try
        { using (var configuration = new EventLogConfiguration(channel, session)) return configuration.LogName != null; }
        catch (EventLogNotFoundException) { return false; }
    }
    public EventLogReadSession OpenReader(WindowsEventQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        cancellationToken.ThrowIfCancellationRequested();
        return new EventLogReadSession(query, session);
    }
    /// <summary>Reads at most the query's limit into detached snapshots and closes all native records/readers before returning.</summary>
    public IReadOnlyList<WindowsEventSnapshot> Read(WindowsEventQuery query, bool includeMessages = false, CancellationToken cancellationToken = default)
    {
        var records = new List<WindowsEventSnapshot>();
        using (var reader = OpenReader(query, cancellationToken))
            while (true)
            {
                using (var record = reader.ReadNextRecord(cancellationToken))
                {
                    if (record == null)
                        break;
                    records.Add(new WindowsEventSnapshot(record, includeMessages));
                }
            }
        return records.AsReadOnly();
    }
    /// <remarks>Offloads native reads and forwards cancellation to CancelReading. The caller must retain a borrowed session until completion.</remarks>
    public Task<IReadOnlyList<WindowsEventSnapshot>> ReadAsync(WindowsEventQuery query, bool includeMessages = false, CancellationToken cancellationToken = default)
        => Task.Run(() => Read(query, includeMessages, cancellationToken), cancellationToken);
    /// <summary>Exports matching events to a sibling temporary EVTX, then publishes it. Does not clear or modify the source log.</summary>
    /// <remarks>Export includes all matching events; MaximumEvents and NewestFirst apply to reading, not export.
    /// Cancellation is checked before and after the synchronous export, not during it. The destination's parent must exist.</remarks>
    public void Export(WindowsEventQuery query, FileSystemPath destination, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        cancellationToken.ThrowIfCancellationRequested();
        if (!overwrite && File.Exists(destination.Value))
            throw new IOException("The export destination already exists.");
        var temporary = Path.Combine(Path.GetDirectoryName(destination.Value)!, ".psf-events-" + Guid.NewGuid().ToString("N") + ".evtx");
        try
        {
            session.ExportLog(query.Source, query.SourceKind == WindowsEventSourceKind.Channel ? PathType.LogName : PathType.FilePath, query.ToQueryXml(), temporary);
            cancellationToken.ThrowIfCancellationRequested();
            if (overwrite && File.Exists(destination.Value))
                File.Replace(temporary, destination.Value, null);
            else
                File.Move(temporary, destination.Value);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task ExportAsync(WindowsEventQuery query, FileSystemPath destination, bool overwrite = false, CancellationToken cancellationToken = default)
        => Task.Run(() => Export(query, destination, overwrite, cancellationToken), cancellationToken);
}
