using System;
using System.Collections.Generic;
using System.Linq;

namespace PSFoundation.Core;

/// <summary>Applies overrides to the first matching record, copying only fields already present. Mutations are deliberate and not transactional.</summary>
public sealed class RecordMerger
{
    /// <summary>Updates the first record matching each override's Name and optional Path, without adding new fields.</summary>
    /// <remarks>String matches ignore case. Overrides without a Name or matching record are skipped; earlier mutations survive later failures.</remarks>
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
/// <summary>Literal text transformations with no shell parsing or escaping policy.</summary>
public static class TextConverter
{
    /// <summary>Replaces every opposite-style quote with the selected quote character.</summary>
    /// <remarks>This is textual substitution, not safe command-line quoting or a language-aware syntax conversion.</remarks>
    public static string ConvertQuotes(string text, QuoteStyle style)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (!Enum.IsDefined(typeof(QuoteStyle), style))
            throw new ArgumentOutOfRangeException(nameof(style));
        return style == QuoteStyle.Double ? text.Replace('\'', '"') : text.Replace('"', '\'');
    }
}
