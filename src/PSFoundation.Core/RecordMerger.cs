using System;
using System.Collections.Generic;
using System.Linq;

namespace PSFoundation.Core;

/// <summary>Applies overrides to the first matching record, copying only fields already present. Mutations are deliberate and not transactional.</summary>
public sealed class RecordMerger
{
    public void ApplyOverrides(IEnumerable<IDictionary<string, object?>> records, IEnumerable<IReadOnlyDictionary<string, object?>> overrides)
    {
        var targets = (records ?? throw new ArgumentNullException(nameof(records))).ToArray();
        foreach (var change in overrides ?? throw new ArgumentNullException(nameof(overrides)))
        {
            if (!change.TryGetValue("Name", out var name) || name == null)
                continue;
            change.TryGetValue("Path", out var path);
            var target = targets.FirstOrDefault(item => item.TryGetValue("Name", out var candidate) && Same(candidate, name)
                && (string.IsNullOrEmpty(Convert.ToString(path)) || item.TryGetValue("Path", out var candidatePath) && Same(candidatePath, path)));
            if (target == null)
                continue;
            foreach (var key in target.Keys.ToArray())
                if (change.TryGetValue(key, out var value))
                    target[key] = value;
        }
    }
    private static bool Same(object? left, object? right) => left is string || right is string
        ? string.Equals(Convert.ToString(left), Convert.ToString(right), StringComparison.OrdinalIgnoreCase) : Equals(left, right);
}
public enum QuoteStyle { Single, Double }
public static class TextConverter
{
    public static string ConvertQuotes(string text, QuoteStyle style)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (!Enum.IsDefined(typeof(QuoteStyle), style))
            throw new ArgumentOutOfRangeException(nameof(style));
        return style == QuoteStyle.Double ? text.Replace('\'', '"') : text.Replace('"', '\'');
    }
}
