using System;
using System.Globalization;

namespace AdNoctem.Substrate.Registry;

[Flags]
public enum RegistrySearchTargets
{
    KeyNames = 1,
    ValueNames = 2,
    ValueData = 4,
    All = KeyNames | ValueNames | ValueData,
}

/// <summary>Selects registry names or data to search, case sensitivity, and maximum traversal depth.</summary>
public sealed class RegistrySearchOptions
{
    public RegistrySearchTargets Targets { get; }
    public int MaxDepth { get; }
    public bool CaseSensitive { get; }

    public RegistrySearchOptions(
        RegistrySearchTargets targets = RegistrySearchTargets.All,
        int maxDepth = int.MaxValue,
        bool caseSensitive = false
    )
    {
        if (targets == 0 || (targets & ~RegistrySearchTargets.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(targets));

        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));

        Targets = targets;
        MaxDepth = maxDepth;
        CaseSensitive = caseSensitive;
    }
}

/// <summary>The key or named value that matched a literal search, with flags identifying the matching fields.</summary>
public sealed class RegistrySearchMatch
{
    public RegistryPath Path { get; }
    public RegistryValueEntry? Entry { get; }
    public RegistrySearchTargets Matched { get; }

    internal RegistrySearchMatch(
        RegistryPath path,
        RegistryValueEntry? entry,
        RegistrySearchTargets matched
    )
    {
        Path = path;
        Entry = entry;
        Matched = matched;
    }

    internal static string Format(RegistryValue value) =>
        value.Data is byte[] bytes ? BitConverter.ToString(bytes).Replace("-", "")
        : value.Data is string[] strings ? string.Join("\n", strings)
        : Convert.ToString(value.Data, CultureInfo.InvariantCulture) ?? "";
}
