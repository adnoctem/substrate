using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Security;

namespace AdNoctem.Substrate.PowerShell.Security;

public static class EventQueryCompatibility
{
    public static WindowsEventQuery[] Prepare(
        Hashtable[]? definitions,
        string? group,
        int[]? ids,
        string[]? logs,
        DateTime start,
        DateTime end,
        int maximum,
        Hashtable configuration
    )
    {
        var rows = new List<Hashtable>(definitions ?? Array.Empty<Hashtable>());

        if (!string.IsNullOrEmpty(group))
        {
            var groupData = (Hashtable)EventConfigurationCompatibility.Group(group!, configuration);
            var resolved = EventConfigurationCompatibility.Definitions(
                configuration,
                group,
                null,
                null,
                null,
                null
            );

            if (resolved.Length != 0)
                rows.AddRange(resolved);
            else
                foreach (var log in Strings(groupData["DefaultLogs"]))
                foreach (var id in Integers(groupData["EventIds"]))
                    rows.Add(new Hashtable { ["LogName"] = log, ["Id"] = id });
        }

        if (ids != null && ids.Length != 0)
        {
            if (logs != null && logs.Length != 0)
                foreach (var log in logs)
                foreach (var id in ids)
                    rows.Add(new Hashtable { ["LogName"] = log, ["Id"] = id });
            else
            {
                var resolved = EventConfigurationCompatibility.Definitions(
                    configuration,
                    null,
                    null,
                    null,
                    ids,
                    null
                );

                if (
                    ids.Any(id =>
                        !resolved.Any(row => LanguagePrimitives.ConvertTo<int>(row["Id"]) == id)
                    )
                )
                    throw new ArgumentException(
                        "Supply -LogName for event IDs that have no channel in the configuration."
                    );

                rows.AddRange(resolved);
            }
        }

        if (rows.Count == 0)
            throw new ArgumentException(
                "No event definitions supplied or resolved. Provide -Definition, -Group, or -Id."
            );

        // Preserve the established per-channel grouping. The reusable catalog can additionally group by provider.
        return rows.GroupBy(
                row => LanguagePrimitives.ConvertTo<string>(row["LogName"]),
                StringComparer.OrdinalIgnoreCase
            )
            .OrderBy(bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
            .Select(bucket => new WindowsEventQuery(
                bucket.Key,
                bucket
                    .Select(row => LanguagePrimitives.ConvertTo<int>(row["Id"]))
                    .Distinct()
                    .OrderBy(id => id),
                startTime: new DateTimeOffset(start),
                endTime: new DateTimeOffset(end),
                maximumEvents: maximum == 0 ? int.MaxValue : maximum
            ))
            .ToArray();
    }

    /// <summary>Records handed to PowerShell retain caller ownership, matching Get-WinEvent. Readers and owned query sessions close when enumeration ends.</summary>
    public static IEnumerable<EventRecord> Read(
        WindowsEventQuery[] queries,
        string[]? computers,
        bool skipMissing,
        Action<string> reportError
    )
    {
        foreach (var query in queries)
        foreach (
            var computer in computers == null || computers.Length == 0
                ? new string?[] { null }
                : computers
        )
        {
            EventLogSession? owned = null;
            EventLogReadSession? reader = null;

            try
            {
                Exception? failure = null;

                try
                {
                    if (
                        !string.IsNullOrEmpty(computer)
                        && computer != "."
                        && !string.Equals(computer, "localhost", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(
                            computer,
                            Environment.MachineName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        owned = new EventLogSession(computer);

                    var manager = new EventLogManager(owned);

                    if (!skipMissing || manager.ChannelExists(query.Source))
                        reader = manager.OpenReader(query);
                }
                catch (Exception error)
                {
                    failure = error;
                }

                if (failure != null)
                {
                    reportError("Failed to query log '" + query.Source + "': " + failure.Message);
                    continue;
                }

                if (reader == null)
                    continue;

                while (true)
                {
                    EventRecord? record = null;

                    try
                    {
                        record = reader.ReadNextRecord();
                    }
                    catch (Exception error)
                    {
                        failure = error;
                    }

                    if (failure != null)
                    {
                        reportError(
                            "Failed to query log '" + query.Source + "': " + failure.Message
                        );
                        break;
                    }

                    if (record == null)
                        break;

                    yield return record;
                }
            }
            finally
            {
                reader?.Dispose();
                owned?.Dispose();
            }
        }
    }

    public static IEnumerable<PSObject> ReadGroup(
        string group,
        DateTime start,
        DateTime end,
        int[]? ids,
        string[]? computers,
        int maximum,
        Hashtable configuration,
        Hashtable filters,
        Action<string> reportError
    )
    {
        var groupData = (Hashtable)EventConfigurationCompatibility.Group(group, configuration);
        var conditions = new List<WindowsEventFieldFilter>();
        void Add(string option, string[] fields, EventFieldMatchKind kind)
        {
            if (filters[option] == null || !LanguagePrimitives.IsTrue(filters[option]))
                return;

            conditions.Add(new WindowsEventFieldFilter(fields, Strings(filters[option]), kind));
        }
        Add("LogonType", new[] { "LogonType" }, EventFieldMatchKind.IntegerEquals);
        Add("TargetUserName", new[] { "TargetUserName" }, EventFieldMatchKind.Equals);
        Add("SubjectUserName", new[] { "SubjectUserName" }, EventFieldMatchKind.Equals);
        Add("ServiceName", new[] { "ServiceName" }, EventFieldMatchKind.Equals);
        Add(
            "UserName",
            new[] { "UserName", "UserId", "User", "SubjectUserName" },
            EventFieldMatchKind.Regex
        );
        Add(
            "ScriptBlockText",
            new[] { "ScriptBlockText", "Message", "Payload" },
            EventFieldMatchKind.Regex
        );
        Add("TaskName", new[] { "TaskName", "Name" }, EventFieldMatchKind.Regex);

        if (
            group == "Logon"
            && !LanguagePrimitives.IsTrue(filters["IncludeSystem"])
            && groupData["DefaultSuppressions"] is Hashtable suppressions
        )
        {
            var names = Strings(suppressions["TargetUserName"]);

            if (names.Length != 0)
                conditions.Add(
                    new WindowsEventFieldFilter(new[] { "TargetUserName" }, names, negate: true)
                );

            var suffixes = Strings(suppressions["TargetUserNameEndsWith"]);

            if (suffixes.Length != 0)
                conditions.Add(
                    new WindowsEventFieldFilter(
                        new[] { "TargetUserName" },
                        suffixes,
                        EventFieldMatchKind.EndsWith,
                        negate: true
                    )
                );
        }

        var filter = new WindowsEventFilter(
            ids == null || ids.Length == 0 ? Integers(groupData["EventIds"]) : ids,
            conditions
        );
        var queries = Prepare(null, group, null, null, start, end, maximum, configuration);

        foreach (var record in Read(queries, computers, group == "Sysmon", reportError))
        {
            var transferred = false;

            try
            {
                var payload = WindowsEventPayload.Parse(record.ToXml());

                if (!filter.Matches(record.Id, payload))
                    continue;

                var result = EventConfigurationCompatibility.ConvertEvent(
                    PSObject.AsPSObject(record),
                    configuration
                );
                transferred = true;
                yield return result;
            }
            finally
            {
                if (!transferred)
                    record.Dispose();
            }
        }
    }

    private static string[] Strings(object? value) =>
        value == null ? Array.Empty<string>() : LanguagePrimitives.ConvertTo<string[]>(value);

    private static int[] Integers(object? value) =>
        value == null ? Array.Empty<int>() : LanguagePrimitives.ConvertTo<int[]>(value);
}
