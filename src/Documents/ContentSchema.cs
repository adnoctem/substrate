using System;
using System.Collections.Generic;
using System.Linq;

namespace AdNoctem.Substrate.Documents;

public enum FieldKind
{
    /// <summary>One paragraph of text. Authored marks are ignored because the field's role owns its presentation.</summary>
    PlainText,

    /// <summary>Paragraphs with supported marks, and optionally links and lists.</summary>
    RichText,

    /// <summary>One table with a fixed column count and one paragraph per cell.</summary>
    Table,

    /// <summary>A container whose content must be held by its declared child fields.</summary>
    Group,
}

/// <summary>Declares one content-control tag, its kind, cardinality and permitted content.</summary>
/// <remarks>Every field occurs at most once. A required child is required only when its group is present and not empty.</remarks>
public sealed class FieldDefinition
{
    public string Tag { get; }
    public FieldKind Kind { get; }
    public bool Required { get; }
    public bool AllowHyperlinks { get; }
    public bool AllowLists { get; }
    public int Columns { get; }
    public IReadOnlyList<FieldDefinition> Children { get; }

    private FieldDefinition(
        string tag,
        FieldKind kind,
        bool required,
        bool allowHyperlinks,
        bool allowLists,
        int columns,
        IEnumerable<FieldDefinition> children
    )
    {
        if (string.IsNullOrWhiteSpace(tag) || tag.Trim() != tag)
            throw new ArgumentException(
                "A tag must be nonempty without surrounding spaces.",
                nameof(tag)
            );

        Tag = tag;
        Kind = kind;
        Required = required;
        AllowHyperlinks = allowHyperlinks;
        AllowLists = allowLists;
        Columns = columns;
        Children = Array.AsReadOnly(children.ToArray());
    }

    public static FieldDefinition PlainText(string tag, bool required) =>
        new FieldDefinition(tag, FieldKind.PlainText, required, false, false, 0, []);

    public static FieldDefinition RichText(
        string tag,
        bool required,
        bool allowHyperlinks = true,
        bool allowLists = false
    ) => new FieldDefinition(tag, FieldKind.RichText, required, allowHyperlinks, allowLists, 0, []);

    /// <remarks>Cells may hold marks and, when allowed, links. Header rows marked to repeat are author captions and are skipped.</remarks>
    public static FieldDefinition Table(
        string tag,
        int columns,
        bool required,
        bool allowHyperlinks = false
    )
    {
        if (columns < 1)
            throw new ArgumentOutOfRangeException(nameof(columns));

        return new FieldDefinition(
            tag,
            FieldKind.Table,
            required,
            allowHyperlinks,
            false,
            columns,
            []
        );
    }

    public static FieldDefinition Group(
        string tag,
        bool required,
        params FieldDefinition[] children
    )
    {
        if (children == null || children.Length == 0 || children.Any(child => child == null))
            throw new ArgumentException("A group needs child fields.", nameof(children));

        if (children.Any(child => child.Kind == FieldKind.Group))
            throw new ArgumentException("Groups cannot be nested.", nameof(children));

        return new FieldDefinition(tag, FieldKind.Group, required, false, false, 0, children);
    }
}

/// <summary>The fields recognized in a document. Tags with the prefix that are not declared are reported as unknown.</summary>
/// <remarks>Controls tagged with an instruction tag hold template guidance only; they are skipped and never extracted.
/// Controls with other tags, or none, are transparent containers.</remarks>
public sealed class ContentSchema
{
    public string TagPrefix { get; }
    public IReadOnlyList<FieldDefinition> Fields { get; }
    public IReadOnlyList<string> InstructionTags { get; }

    internal IReadOnlyDictionary<string, FieldDefinition> AllFields { get; }
    internal IReadOnlyDictionary<string, FieldDefinition> Parents { get; }

    public ContentSchema(
        string tagPrefix,
        IEnumerable<FieldDefinition> fields,
        IEnumerable<string>? instructionTags = null
    )
    {
        if (string.IsNullOrWhiteSpace(tagPrefix))
            throw new ArgumentException("A tag prefix is required.", nameof(tagPrefix));

        TagPrefix = tagPrefix;
        Fields = Array.AsReadOnly(
            (fields ?? throw new ArgumentNullException(nameof(fields))).ToArray()
        );
        InstructionTags = Array.AsReadOnly((instructionTags ?? []).ToArray());

        var all = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);
        var parents = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);

        void Add(FieldDefinition field)
        {
            if (field == null)
                throw new ArgumentException("Null field.", nameof(fields));

            if (!field.Tag.StartsWith(tagPrefix, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Every field tag must start with the prefix: " + field.Tag,
                    nameof(fields)
                );

            if (all.ContainsKey(field.Tag) || InstructionTags.Contains(field.Tag))
                throw new ArgumentException("Duplicate tag: " + field.Tag, nameof(fields));

            all.Add(field.Tag, field);
        }

        foreach (var field in Fields)
        {
            Add(field);

            foreach (var child in field.Children)
            {
                Add(child);
                parents.Add(child.Tag, field);
            }
        }

        AllFields = all;
        Parents = parents;
    }
}

/// <summary>Extracted value of one field.</summary>
public abstract class FieldContent
{
    public string Tag { get; }

    private protected FieldContent(string tag) =>
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));
}

public sealed class TextFieldContent : FieldContent
{
    /// <summary>The verbatim text; deliberate line breaks are line feeds.</summary>
    public string Text { get; }

    public TextFieldContent(string tag, string text)
        : base(tag) => Text = text ?? throw new ArgumentNullException(nameof(text));
}

public sealed class RichFieldContent : FieldContent
{
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    public RichFieldContent(string tag, IEnumerable<DocumentBlock> blocks)
        : base(tag) =>
        Blocks = Array.AsReadOnly(
            (blocks ?? throw new ArgumentNullException(nameof(blocks))).ToArray()
        );
}

public sealed class TableFieldContent : FieldContent
{
    /// <summary>Data rows in source order; each row has the declared number of cells.</summary>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<DocumentInline>>> Rows { get; }

    public TableFieldContent(string tag, IEnumerable<IEnumerable<IEnumerable<DocumentInline>>> rows)
        : base(tag) =>
        Rows = Array.AsReadOnly(
            (rows ?? throw new ArgumentNullException(nameof(rows)))
                .Select(row =>
                    (IReadOnlyList<IReadOnlyList<DocumentInline>>)
                        Array.AsReadOnly(
                            row.Select(cell =>
                                    (IReadOnlyList<DocumentInline>)Array.AsReadOnly(cell.ToArray())
                                )
                                .ToArray()
                        )
                )
                .ToArray()
        );
}

public sealed class GroupFieldContent : FieldContent
{
    /// <summary>Present, nonempty children by tag.</summary>
    public IReadOnlyDictionary<string, FieldContent> Children { get; }

    public GroupFieldContent(string tag, IEnumerable<FieldContent> children)
        : base(tag) =>
        Children = (children ?? throw new ArgumentNullException(nameof(children))).ToDictionary(
            child => child.Tag,
            StringComparer.Ordinal
        );
}

/// <summary>The result of reading a source document against a schema.</summary>
/// <remarks>Fields holds present, nonempty top-level fields. Absent or genuinely empty optional fields are omitted. When
/// Succeeded is false, callers must not publish output derived from Fields.</remarks>
public sealed class ContentExtraction
{
    public IReadOnlyDictionary<string, string> Properties { get; }
    public IReadOnlyDictionary<string, FieldContent> Fields { get; }
    public IReadOnlyList<DocumentDiagnostic> Diagnostics { get; }
    public bool Succeeded => !DocumentDiagnostic.HasErrors(Diagnostics);

    internal ContentExtraction(
        IDictionary<string, string> properties,
        IDictionary<string, FieldContent> fields,
        IReadOnlyList<DocumentDiagnostic> diagnostics
    )
    {
        Properties = new Dictionary<string, string>(properties, StringComparer.Ordinal);
        Fields = new Dictionary<string, FieldContent>(fields, StringComparer.Ordinal);
        Diagnostics = diagnostics;
    }
}
