using System;
using System.Collections;
using System.Collections.Generic;

namespace AdNoctem.Substrate.Core.Compatibility;

/// <summary>Ordered result fields with explicit presence, including explicit nulls.</summary>
internal sealed class OperationResult
{
    public IReadOnlyList<KeyValuePair<string, object?>> Fields { get; }

    private OperationResult(List<KeyValuePair<string, object?>> fields) =>
        Fields = fields.AsReadOnly();

    public static OperationResult Create(
        string? target,
        string? action,
        string? status,
        IReadOnlyDictionary<string, object?> supplied,
        IDictionary? extra = null
    )
    {
        var fields = new List<KeyValuePair<string, object?>>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, object? value)
        {
            if (names.Add(name))
                fields.Add(new KeyValuePair<string, object?>(name, value));
        }
        void Optional(string parameter, string? output = null)
        {
            if (supplied.TryGetValue(parameter, out var value))
                Add(output ?? parameter, value);
        }
        Add("Target", target);
        Optional("Source");
        Optional("Scope");
        Add("Action", action);
        Add("Status", status);
        Optional("Detail");
        Optional("SkippedReason");
        Optional("ErrorMessage", "Error");

        foreach (
            var name in new[]
            {
                "Changed",
                "AlreadyCompliant",
                "Before",
                "After",
                "ExitCode",
                "RebootRequired",
                "Duration",
                "RunId",
            }
        )
            Optional(name);

        if (extra != null)
            foreach (DictionaryEntry entry in extra)
                Add(Convert.ToString(entry.Key) ?? "", entry.Value);

        return new OperationResult(fields);
    }
}
