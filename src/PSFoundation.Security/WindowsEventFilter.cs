using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PSFoundation.Security;

public enum EventFieldMatchKind { Equals, Regex, EndsWith, IntegerEquals }
/// <summary>Matches any supplied field against any supplied value. Negation excludes that match; regex work has a finite per-match timeout.</summary>
public sealed class WindowsEventFieldFilter
{
    public IReadOnlyList<string> FieldNames { get; }
    public IReadOnlyList<string> Values { get; }
    public EventFieldMatchKind MatchKind { get; }
    public bool Negate { get; }
    public bool IgnoreCase { get; }
    private readonly Regex[]? expressions;
    public WindowsEventFieldFilter(IEnumerable<string> fieldNames, IEnumerable<string> values, EventFieldMatchKind matchKind = EventFieldMatchKind.Equals,
        bool negate = false, bool ignoreCase = true, TimeSpan? regexTimeout = null)
    {
        var names = (fieldNames ?? throw new ArgumentNullException(nameof(fieldNames))).ToArray();
        var patterns = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
        if (names.Length == 0 || names.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one named event field is required.", nameof(fieldNames));
        if (patterns.Length == 0 || patterns.Any(item => item == null))
            throw new ArgumentException("At least one non-null match value is required.", nameof(values));
        if (!Enum.IsDefined(typeof(EventFieldMatchKind), matchKind))
            throw new ArgumentOutOfRangeException(nameof(matchKind));
        var timeout = regexTimeout ?? TimeSpan.FromSeconds(1);
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(regexTimeout));
        FieldNames = Array.AsReadOnly(names);
        Values = Array.AsReadOnly(patterns);
        MatchKind = matchKind;
        Negate = negate;
        IgnoreCase = ignoreCase;
        if (matchKind == EventFieldMatchKind.Regex)
            expressions = patterns.Select(value => new Regex(value,
            RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None), timeout)).ToArray();
    }
    public bool Matches(WindowsEventPayload payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var matched = FieldNames.Any(name =>
        {
            var value = payload.GetValue(name, StringComparison.OrdinalIgnoreCase);
            if (value == null)
                return false;
            if (expressions != null)
                return expressions.Any(expression => expression.IsMatch(value));
            return Values.Any(expected => MatchKind == EventFieldMatchKind.EndsWith ? value.EndsWith(expected, comparison)
                : MatchKind == EventFieldMatchKind.IntegerEquals ? Integer(value, out var number) && Integer(expected, out var other) && number == other
                : string.Equals(value, expected, comparison));
        });
        return Negate ? !matched : matched;
    }
    private static bool Integer(string value, out int number) => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? int.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number)
        : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
}
/// <summary>Combines event identity and named-field predicates against detached event data.</summary>
public sealed class WindowsEventFilter
{
    public IReadOnlyList<int> EventIds { get; }
    public IReadOnlyList<WindowsEventFieldFilter> Fields { get; }
    public WindowsEventFilter(IEnumerable<int>? eventIds = null, IEnumerable<WindowsEventFieldFilter>? fields = null)
    {
        var ids = (eventIds ?? Array.Empty<int>()).Distinct().ToArray();
        var conditions = (fields ?? Array.Empty<WindowsEventFieldFilter>()).ToArray();
        if (ids.Any(id => id < 0 || id > ushort.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(eventIds));
        if (conditions.Any(condition => condition == null))
            throw new ArgumentException("Filters cannot contain null.", nameof(fields));
        EventIds = Array.AsReadOnly(ids);
        Fields = Array.AsReadOnly(conditions);
    }
    public bool Matches(int id, WindowsEventPayload payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        return (EventIds.Count == 0 || EventIds.Contains(id)) && Fields.All(field => field.Matches(payload));
    }
    public bool Matches(WindowsEventSnapshot snapshot)
    { if (snapshot == null) throw new ArgumentNullException(nameof(snapshot)); return Matches(snapshot.Id, snapshot.Payload); }
}
