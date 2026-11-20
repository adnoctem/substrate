using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Core;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Core;

public static class DataCompatibility
{
    /// <summary>
    /// Re-indents compact JSON with a fixed indentation. Windows PowerShell 5.1's ConvertTo-Json aligns nested values
    /// under their keys, so deep reports drift far to the right; PowerShell 7 indents differently again. Also restores
    /// the ' &lt; &gt; &amp; characters that Windows PowerShell 5.1 escapes as \u00XX, which are valid JSON literals.
    /// </summary>
    public static string FormatJson(string Json, int Indent)
    {
        if (string.IsNullOrEmpty(Json))
            return Json;

        var unit = new string(' ', Math.Max(0, Indent));
        var builder = new System.Text.StringBuilder(Json.Length * 2);
        var depth = 0;
        var inString = false;

        for (var index = 0; index < Json.Length; index++)
        {
            var current = Json[index];

            if (inString)
            {
                // An escape is copied whole, so an escaped backslash before "u0027" stays literal text.
                if (current == '\\' && index + 1 < Json.Length)
                {
                    var escape = Json[index + 1];

                    if (
                        escape == 'u'
                        && index + 5 < Json.Length
                        && Readable(Json.Substring(index + 2, 4), out var literal)
                    )
                    {
                        builder.Append(literal);
                        index += 5;
                        continue;
                    }

                    builder.Append(current).Append(escape);
                    index++;
                    continue;
                }

                builder.Append(current);

                if (current == '"')
                    inString = false;

                continue;
            }

            switch (current)
            {
                case '"':
                    inString = true;
                    builder.Append(current);
                    break;
                case '{':
                case '[':
                    var closing = current == '{' ? '}' : ']';
                    var next = NextSignificant(Json, index + 1);

                    // Empty objects and arrays stay on one line.
                    if (next >= 0 && Json[next] == closing)
                    {
                        builder.Append(current).Append(closing);
                        index = next;
                        break;
                    }

                    depth++;
                    builder.Append(current).Append('\n').Append(Repeat(unit, depth));
                    break;
                case '}':
                case ']':
                    depth--;
                    builder.Append('\n').Append(Repeat(unit, depth)).Append(current);
                    break;
                case ',':
                    builder.Append(current).Append('\n').Append(Repeat(unit, depth));
                    break;
                case ':':
                    builder.Append(": ");
                    break;
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                    break;
                default:
                    builder.Append(current);
                    break;
            }
        }

        return builder.ToString().Replace("\n", Environment.NewLine);
    }

    private static bool Readable(string hex, out char literal)
    {
        literal = '\0';

        switch (hex.ToLowerInvariant())
        {
            case "0027":
                literal = '\'';
                return true;
            case "003c":
                literal = '<';
                return true;
            case "003e":
                literal = '>';
                return true;
            case "0026":
                literal = '&';
                return true;
            default:
                return false;
        }
    }

    private static int NextSignificant(string json, int start)
    {
        for (var index = start; index < json.Length; index++)
        {
            if (!char.IsWhiteSpace(json[index]))
                return index;
        }

        return -1;
    }

    private static string Repeat(string unit, int count)
    {
        var builder = new System.Text.StringBuilder(unit.Length * Math.Max(0, count));

        for (var index = 0; index < count; index++)
            builder.Append(unit);

        return builder.ToString();
    }

    public static void Merge(Array Base, Array Overrides)
    {
        var originals = Base.Cast<object>()
            .Select(value => PSObject.AsPSObject(value).BaseObject)
            .OfType<IDictionary>()
            .ToArray();
        var records = originals
            .Select(value =>
                value
                    .Cast<DictionaryEntry>()
                    .ToDictionary(
                        item => LanguagePrimitives.ConvertTo<string>(item.Key),
                        item => (object?)item.Value,
                        StringComparer.OrdinalIgnoreCase
                    )
            )
            .ToArray();
        var changes = Overrides
            .Cast<object>()
            .Select(value =>
                (IReadOnlyDictionary<string, object?>)
                    PSObject
                        .AsPSObject(value)
                        .Properties.ToDictionary(
                            property => property.Name,
                            property => property.Value,
                            StringComparer.OrdinalIgnoreCase
                        )
            )
            .ToArray();
        new RecordMerger().ApplyOverrides(records, changes);

        for (var index = 0; index < records.Length; index++)
            foreach (var key in originals[index].Keys.Cast<object>().ToArray())
                originals[index][key] = records[index][LanguagePrimitives.ConvertTo<string>(key)];
    }
}
