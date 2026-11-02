using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AdNoctem.Substrate.Documents;

// Effective authored emphasis under ECMA-376 toggle rules: direct formatting is absolute; otherwise the paragraph-style and
// character-style chains are each resolved (the nearest basedOn definition wins) and combined with XOR; otherwise document
// defaults apply. Table, numbering and theme formatting are presentation and never produce marks.
internal sealed class WordStyles
{
    internal const string PlaceholderStyle = "PlaceholderText";

    private readonly Dictionary<string, Style> styles;
    private readonly string? defaultParagraphStyle;
    private readonly bool defaultBold;
    private readonly bool defaultItalic;

    internal WordStyles(MainDocumentPart main)
    {
        var root = main.StyleDefinitionsPart?.Styles;
        styles = new Dictionary<string, Style>(StringComparer.Ordinal);

        foreach (var style in root?.Elements<Style>() ?? Enumerable.Empty<Style>())
            if (style.StyleId?.Value is string id && !styles.ContainsKey(id))
                styles.Add(id, style);

        defaultParagraphStyle = styles
            .Values.FirstOrDefault(style =>
                style.Type?.Value == StyleValues.Paragraph && style.Default?.Value == true
            )
            ?.StyleId?.Value;
        var defaults = root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle;
        defaultBold = Toggle(defaults?.Bold) ?? false;
        defaultItalic = Toggle(defaults?.Italic) ?? false;
    }

    internal TextMarks Marks(Run run, Paragraph? paragraph)
    {
        var direct = run.RunProperties;
        var paragraphStyle = ParagraphStyle(paragraph);
        var characterStyle = direct?.RunStyle?.Val?.Value;
        var marks = TextMarks.None;

        if (
            Resolve(
                Toggle(direct?.Bold),
                paragraphStyle,
                characterStyle,
                style => style.StyleRunProperties?.Bold,
                defaultBold
            )
        )
            marks |= TextMarks.Bold;

        if (
            Resolve(
                Toggle(direct?.Italic),
                paragraphStyle,
                characterStyle,
                style => style.StyleRunProperties?.Italic,
                defaultItalic
            )
        )
            marks |= TextMarks.Italic;

        return marks;
    }

    internal bool IsHidden(Run run, Paragraph? paragraph) =>
        Resolve(
            Toggle(run.RunProperties?.Vanish),
            ParagraphStyle(paragraph),
            run.RunProperties?.RunStyle?.Val?.Value,
            style => style.StyleRunProperties?.Vanish,
            false
        );

    internal static bool IsPlaceholder(Run run) =>
        run.RunProperties?.RunStyle?.Val?.Value == PlaceholderStyle;

    /// <summary>Numbering identity and level from direct paragraph properties or the paragraph-style chain.</summary>
    internal (int NumberingId, int Level)? Numbering(Paragraph paragraph)
    {
        var direct = paragraph.ParagraphProperties?.NumberingProperties;
        int? id = direct?.NumberingId?.Val?.Value;
        int? level = direct?.NumberingLevelReference?.Val?.Value;

        foreach (var style in Chain(ParagraphStyle(paragraph)))
        {
            var inherited = style.StyleParagraphProperties?.NumberingProperties;
            id ??= inherited?.NumberingId?.Val?.Value;
            level ??= inherited?.NumberingLevelReference?.Val?.Value;
        }

        return id is int value && value != 0 ? (value, level ?? 0) : null;
    }

    internal Style? Find(string id) => styles.TryGetValue(id, out var style) ? style : null;

    private string? ParagraphStyle(Paragraph? paragraph) =>
        paragraph?.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? defaultParagraphStyle;

    private bool Resolve(
        bool? direct,
        string? paragraphStyle,
        string? characterStyle,
        Func<Style, OnOffType?> select,
        bool fallback
    )
    {
        if (direct.HasValue)
            return direct.Value;

        var fromParagraph = Inherited(paragraphStyle, select);
        var fromCharacter = Inherited(characterStyle, select);

        if (fromParagraph.HasValue || fromCharacter.HasValue)
            return (fromParagraph ?? false) ^ (fromCharacter ?? false);

        return fallback;
    }

    private bool? Inherited(string? id, Func<Style, OnOffType?> select)
    {
        foreach (var style in Chain(id))
            if (Toggle(select(style)) is bool value)
                return value;

        return null;
    }

    private IEnumerable<Style> Chain(string? id)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (id != null && seen.Add(id) && styles.TryGetValue(id, out var style))
        {
            yield return style;
            id = style.BasedOn?.Val?.Value;
        }
    }

    private static bool? Toggle(OnOffType? value) =>
        value == null ? null : value.Val == null || value.Val.Value;
}

// Word list numbering: counters belong to the abstract definition and continue across instances unless an instance
// overrides the start of a level; starting a level resets deeper levels.
internal sealed class WordNumbering
{
    private readonly Dictionary<int, NumberingInstance> instances =
        new Dictionary<int, NumberingInstance>();
    private readonly Dictionary<int, AbstractNum> definitions = new Dictionary<int, AbstractNum>();
    private readonly Dictionary<int, int?[]> counters = new Dictionary<int, int?[]>();
    private readonly HashSet<int> started = new HashSet<int>();
    private readonly WordStyles styles;

    internal WordNumbering(MainDocumentPart main, WordStyles styles)
    {
        this.styles = styles;
        var root = main.NumberingDefinitionsPart?.Numbering;

        foreach (var instance in root?.Elements<NumberingInstance>() ?? [])
            if (instance.NumberID?.Value is int id && !instances.ContainsKey(id))
                instances.Add(id, instance);

        foreach (var definition in root?.Elements<AbstractNum>() ?? [])
            if (definition.AbstractNumberId?.Value is int id && !definitions.ContainsKey(id))
                definitions.Add(id, definition);
    }

    internal sealed class Item
    {
        internal int Definition { get; }
        internal ListKind? Kind { get; }
        internal string Format { get; }
        internal int Number { get; }

        internal Item(int definition, ListKind? kind, string format, int number)
        {
            Definition = definition;
            Kind = kind;
            Format = format;
            Number = number;
        }
    }

    /// <summary>Advances the counter for one numbered paragraph. Null means the numbering reference is broken.</summary>
    internal Item? Next(int numberingId, int level)
    {
        if (level < 0 || level > 8 || !instances.TryGetValue(numberingId, out var instance))
            return null;

        var definitionId = instance.AbstractNumId?.Val?.Value;

        if (definitionId is not int id || !definitions.TryGetValue(id, out var definition))
            return null;

        // A style-linked definition delegates to the numbering of its linked style.
        if (
            definition.NumberingStyleLink?.Val?.Value is string link
            && styles
                .Find(link)
                ?.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value
                is int linked
            && instances.TryGetValue(linked, out var target)
            && target.AbstractNumId?.Val?.Value is int targetId
            && definitions.TryGetValue(targetId, out var targetDefinition)
        )
        {
            id = targetId;
            definition = targetDefinition;
        }

        var overrides = instance.Elements<LevelOverride>().ToArray();
        var levelOverride = overrides.FirstOrDefault(item => item.LevelIndex?.Value == level);
        var properties =
            levelOverride?.Level
            ?? definition.Elements<Level>().FirstOrDefault(item => item.LevelIndex?.Value == level);
        var format = properties?.NumberingFormat?.Val?.InnerText ?? "decimal";
        ListKind? kind =
            format == "bullet" ? ListKind.Bullet
            : format == "decimal" ? ListKind.Decimal
            : null;

        if (!counters.TryGetValue(id, out var state))
            counters.Add(id, state = new int?[9]);

        if (started.Add(numberingId))
            foreach (var item in overrides)
                if (
                    item.StartOverrideNumberingValue != null
                    && item.LevelIndex?.Value is int index
                    && index is >= 0 and <= 8
                )
                    state[index] = null;

        var start =
            levelOverride?.StartOverrideNumberingValue?.Val?.Value
            ?? properties?.StartNumberingValue?.Val?.Value
            ?? 0;
        state[level] = state[level] is int current ? current + 1 : start;

        for (var deeper = level + 1; deeper < state.Length; deeper++)
            state[deeper] = null;

        return new Item(id, kind, format, state[level]!.Value);
    }
}
