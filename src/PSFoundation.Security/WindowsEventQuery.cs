using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using PSFoundation.IO;

namespace PSFoundation.Security;

public enum WindowsEventSourceKind { Channel, File }
/// <summary>A bounded event query for a live channel or saved file, with explicit ordering and time filters.</summary>
public sealed class WindowsEventQuery
{
    public string Source { get; }
    public WindowsEventSourceKind SourceKind { get; }
    public IReadOnlyList<int> EventIds { get; }
    public string? ProviderName { get; }
    public DateTimeOffset? StartTime { get; }
    public DateTimeOffset? EndTime { get; }
    public int MaximumEvents { get; }
    public bool NewestFirst { get; }
    public WindowsEventQuery(string channel, IEnumerable<int>? eventIds = null, string? providerName = null,
        DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, int maximumEvents = 1000, bool newestFirst = true)
        : this(channel, WindowsEventSourceKind.Channel, eventIds, providerName, startTime, endTime, maximumEvents, newestFirst) { }
    public WindowsEventQuery(FileSystemPath file, IEnumerable<int>? eventIds = null, string? providerName = null,
        DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, int maximumEvents = 1000, bool newestFirst = true)
        : this((file ?? throw new ArgumentNullException(nameof(file))).Value, WindowsEventSourceKind.File, eventIds, providerName, startTime, endTime, maximumEvents, newestFirst) { }
    private WindowsEventQuery(string source, WindowsEventSourceKind kind, IEnumerable<int>? eventIds, string? providerName,
        DateTimeOffset? startTime, DateTimeOffset? endTime, int maximumEvents, bool newestFirst)
    {
        if (string.IsNullOrWhiteSpace(source) || source.IndexOf('\0') >= 0)
            throw new ArgumentException("An event channel or file is required.", nameof(source));
        var ids = (eventIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (ids.Any(id => id < 0 || id > ushort.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(eventIds));
        if (providerName != null && (string.IsNullOrWhiteSpace(providerName) || providerName.IndexOf('\0') >= 0))
            throw new ArgumentException("Provider names must not be blank or contain NUL.", nameof(providerName));
        if (startTime > endTime)
            throw new ArgumentException("The start time must not exceed the end time.");
        if (maximumEvents < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumEvents));
        // Windows Event Log supports a restricted XPath subset, without concat(). Do not interpolate an unquotable provider name.
        if (providerName != null)
            Literal(providerName);
        Source = source;
        SourceKind = kind;
        EventIds = Array.AsReadOnly(ids);
        ProviderName = providerName;
        StartTime = startTime;
        EndTime = endTime;
        MaximumEvents = maximumEvents;
        NewestFirst = newestFirst;
    }
    public string ToXPath()
    {
        var predicates = new List<string>();
        if (EventIds.Count != 0)
            predicates.Add("(" + string.Join(" or ", EventIds.Select(id => "EventID=" + id.ToString(CultureInfo.InvariantCulture))) + ")");
        if (ProviderName != null)
            predicates.Add("Provider[@Name=" + Literal(ProviderName) + "]");
        if (StartTime.HasValue)
            predicates.Add("TimeCreated[@SystemTime>='" + Time(StartTime.Value) + "']");
        if (EndTime.HasValue)
            predicates.Add("TimeCreated[@SystemTime<='" + Time(EndTime.Value) + "']");
        return predicates.Count == 0 ? "*" : "*[System[" + string.Join(" and ", predicates) + "]]";
    }
    /// <summary>Builds a structured query, splitting large ID lists to stay within Windows Event Log's per-selector expression limit.</summary>
    public string ToQueryXml()
    {
        var query = new XElement("Query", new XAttribute("Id", "0"), new XAttribute("Path", Source));
        if (EventIds.Count == 0)
            query.Add(new XElement("Select", new XAttribute("Path", Source), ToXPath()));
        for (var offset = 0; offset < EventIds.Count; offset += 15)
        {
            var slice = new WindowsEventQuery(Source, SourceKind, EventIds.Skip(offset).Take(15), ProviderName, StartTime, EndTime, MaximumEvents, NewestFirst);
            query.Add(new XElement("Select", new XAttribute("Path", Source), slice.ToXPath()));
        }
        return new XElement("QueryList", query).ToString(SaveOptions.DisableFormatting);
    }
    private static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    private static string Literal(string value) => value.IndexOf('\'') < 0 ? "'" + value + "'" : value.IndexOf('"') < 0 ? "\"" + value + "\""
        : throw new ArgumentException("A provider name containing both quote styles cannot be represented by the Windows Event Log XPath subset.");
}
