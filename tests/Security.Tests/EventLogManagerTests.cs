using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AdNoctem.Substrate.IO;
using Xunit;

namespace AdNoctem.Substrate.Security.Tests;

public sealed class EventLogManagerTests
{
    [Fact]
    public async Task NativeReaderDetachesRecordsAndExportsAReadOnlyEvtxSnapshot()
    {
        var manager = new EventLogManager();
        Assert.True(manager.ChannelExists("System"));
        Assert.False(manager.ChannelExists("AdNoctem.Substrate.Synthetic.Nonexistent"));
        var recent = await manager.ReadAsync(new WindowsEventQuery("System", maximumEvents: 1));
        var first = Assert.Single(recent);
        Assert.Equal("System", first.LogName);
        Assert.NotEmpty(first.Xml);
        Assert.Contains(
            manager.Read(
                new WindowsEventQuery(
                    "System",
                    Enumerable.Range(1, 40).Append(first.Id),
                    maximumEvents: 100
                )
            ),
            record => record.RecordId == first.RecordId
        );
        var file = FileSystemPath.Parse(
            Path.Combine(
                Path.GetTempPath(),
                "AdNoctem.Substrate.PowerShell-event-" + Guid.NewGuid().ToString("N") + ".evtx"
            )
        );

        try
        {
            var query = new WindowsEventQuery(
                "System",
                new[] { first.Id },
                startTime: first.TimeCreated!.Value.AddSeconds(-1),
                endTime: first.TimeCreated.Value.AddSeconds(1)
            );
            await manager.ExportAsync(query, file);
            Assert.True(File.Exists(file.Value));
            Assert.Contains(
                manager.Read(new WindowsEventQuery(file)),
                record => record.RecordId == first.RecordId
            );
            Assert.Throws<IOException>(() => manager.Export(query, file));

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() =>
                    manager.Read(query, cancellationToken: cancellation.Token)
                );
            }
        }
        finally
        {
            File.Delete(file.Value);
        }
    }

    [Fact]
    public void EventPayloadRetainsOrderedDataAndRejectsExternalEntities()
    {
        var payload = WindowsEventPayload.Parse(
            "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><EventData><Data Name='User'>first</Data><Data>unnamed</Data><Data Name='User'>last</Data><Data Name='Empty'/></EventData><UserData><Value>custom</Value></UserData></Event>"
        );
        Assert.Equal("last", payload.GetValue("User"));
        Assert.Null(payload.Fields[1].Name);
        Assert.Equal("", payload.GetValue("Empty"));
        Assert.Contains("custom", payload.UserDataXml);
        Assert.Throws<XmlException>(() =>
            WindowsEventPayload.Parse(
                "<!DOCTYPE Event [<!ENTITY x SYSTEM 'file:///never-read'>]><Event>&x;</Event>"
            )
        );
        var query = new WindowsEventQuery(
            "System",
            new[] { 1, 1, 2 },
            providerName: "a'b",
            startTime: DateTimeOffset.Parse("2026-01-01T01:00:00+01:00")
        );
        Assert.Equal(2, query.EventIds.Count);
        Assert.Contains("2026-01-01T00:00:00.0000000Z", query.ToXPath());
        Assert.Contains("@Name=\"a'b\"", query.ToXPath());
        Assert.Throws<ArgumentException>(() =>
            new WindowsEventQuery("System", providerName: "a'\"b")
        );
        var catalog = new SecurityEventCatalog(
            new[]
            {
                new SecurityEventDefinition(1, "System", "one", "First", "Group"),
                new SecurityEventDefinition(1, "System", "two", "Second", "Group"),
            }
        );
        Assert.Equal(2, catalog.CreateQueries(catalog.Find(group: "group")).Count);
        Assert.Contains(SecurityEventCatalog.Default.Find(group: "Logon"), item => item.Id == 4624);
        Assert.Contains(SecurityEventCatalog.Default.Definitions, item => item.Id == 7045);
        var filter = new WindowsEventFilter(
            new[] { 1 },
            new[]
            {
                new WindowsEventFieldFilter(
                    new[] { "User" },
                    new[] { "^last$" },
                    EventFieldMatchKind.Regex
                ),
                new WindowsEventFieldFilter(
                    new[] { "User" },
                    new[] { "$" },
                    EventFieldMatchKind.EndsWith,
                    negate: true
                ),
            }
        );
        Assert.True(filter.Matches(1, payload));
        Assert.False(filter.Matches(2, payload));
    }
}
