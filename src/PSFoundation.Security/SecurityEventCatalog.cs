using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PSFoundation.Security;

public sealed class SecurityEventDefinition
{
    public int Id { get; }
    public string LogName { get; }
    public string? ProviderName { get; }
    public string? Name { get; }
    public string? Group { get; }
    public SecurityEventDefinition(int id, string logName, string? providerName = null, string? name = null, string? group = null)
    {
        if (id < 0 || id > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(logName))
            throw new ArgumentException("An event channel is required.", nameof(logName));
        Id = id;
        LogName = logName;
        ProviderName = providerName;
        Name = name;
        Group = group;
    }
}
/// <summary>Immutable semantic event definitions. Configuration formats and PowerShell field bags remain outside the library.</summary>
public sealed class SecurityEventCatalog
{
    private static readonly Lazy<SecurityEventCatalog> DefaultCatalog = new Lazy<SecurityEventCatalog>(() =>
    {
        using (var stream = typeof(SecurityEventCatalog).Assembly.GetManifestResourceStream("PSFoundation.Security.default-event-definitions.json")!)
            return Load(stream);
    });
    public static SecurityEventCatalog Default => DefaultCatalog.Value;

    /// <summary>Reads event definitions from a JSON array. The caller retains ownership of the stream.</summary>
    public static SecurityEventCatalog Load(Stream json)
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));
        var rows = (DefinitionData[]?)new DataContractJsonSerializer(typeof(DefinitionData[])).ReadObject(json)
            ?? throw new SerializationException("An array of event definitions is required.");
        return new SecurityEventCatalog(rows.Select(row => row == null
            ? throw new SerializationException("An event definition cannot be null.")
            : new SecurityEventDefinition(row.Id, row.LogName!, row.ProviderName, row.Name, row.Group)));
    }

    [DataContract]
    private sealed class DefinitionData
    {
        [DataMember(IsRequired = true)] public int Id { get; set; }
        [DataMember(IsRequired = true)] public string? LogName { get; set; }
        [DataMember] public string? ProviderName { get; set; }
        [DataMember] public string? Name { get; set; }
        [DataMember] public string? Group { get; set; }
    }

    public IReadOnlyList<SecurityEventDefinition> Definitions { get; }
    public SecurityEventCatalog(IEnumerable<SecurityEventDefinition> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));
        var copy = definitions.ToArray();
        if (copy.Any(item => item == null))
            throw new ArgumentException("Definitions cannot contain null.", nameof(definitions));
        Definitions = Array.AsReadOnly(copy);
    }
    public IReadOnlyList<SecurityEventDefinition> Find(string? group = null, string? logName = null, string? providerName = null,
        IEnumerable<int>? eventIds = null, string? name = null)
    {
        var ids = eventIds == null ? null : new HashSet<int>(eventIds);
        bool Match(string? expected, string? actual) => string.IsNullOrEmpty(expected) || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        return Array.AsReadOnly(Definitions.Where(item => Match(group, item.Group) && Match(logName, item.LogName) && Match(providerName, item.ProviderName)
            && Match(name, item.Name) && (ids == null || ids.Count == 0 || ids.Contains(item.Id))).ToArray());
    }
    /// <summary>Creates separate channel/provider queries so IDs from one provider cannot accidentally match another provider.</summary>
    public IReadOnlyList<WindowsEventQuery> CreateQueries(IEnumerable<SecurityEventDefinition> definitions, DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null, int maximumEventsPerQuery = 1000)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));
        var copy = definitions.ToArray();
        if (copy.Any(item => item == null))
            throw new ArgumentException("Definitions cannot contain null.", nameof(definitions));
        return Array.AsReadOnly(copy.GroupBy(item => new { item.LogName, item.ProviderName }).Select(group => new WindowsEventQuery(group.Key.LogName,
            group.Select(item => item.Id), group.Key.ProviderName, startTime, endTime, maximumEventsPerQuery)).ToArray());
    }
}
