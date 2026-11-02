using System;
using System.Collections.Generic;
using System.Text;

namespace AdNoctem.Substrate.Documents;

/// <summary>A parsed HTML template with validated value slots and optional section regions.</summary>
/// <remarks>Markers are <c>{{name}}</c>, <c>{{#section}}</c> and <c>{{/section}}</c>. Sections do not nest. Every
/// optional region declared by the pack must appear, and no unknown marker is accepted. Literal template markup is
/// trusted; generated values are inserted only through explicit, validated slots.</remarks>
internal sealed class HtmlTemplate
{
    private static readonly string[] CommonValues =
    [
        "language",
        "title",
        "masthead",
        "headline",
        "body",
        "contact.heading",
        "contact.details",
    ];

    private static readonly Dictionary<PressReleaseSectionKind, string[]> SectionValues = new()
    {
        [PressReleaseSectionKind.Specifications] =
        [
            "specifications.heading",
            "specifications.rows",
        ],
        [PressReleaseSectionKind.FactBox] = ["factbox.heading", "factbox.body"],
        [PressReleaseSectionKind.Media] = ["media"],
        [PressReleaseSectionKind.CompanyDescriptions] = ["about"],
        [PressReleaseSectionKind.Editorial] = ["editorial"],
    };

    private static readonly Dictionary<string, PressReleaseSectionKind> SectionNames = new(
        StringComparer.Ordinal
    )
    {
        ["specifications"] = PressReleaseSectionKind.Specifications,
        ["factbox"] = PressReleaseSectionKind.FactBox,
        ["media"] = PressReleaseSectionKind.Media,
        ["about"] = PressReleaseSectionKind.CompanyDescriptions,
        ["editorial"] = PressReleaseSectionKind.Editorial,
    };

    private readonly IReadOnlyList<HtmlNode> nodes;

    internal HashSet<PressReleaseSectionKind> Sections { get; } = new();

    private HtmlTemplate(IReadOnlyList<HtmlNode> nodes) => this.nodes = nodes;

    internal static HtmlTemplate Parse(
        string text,
        IEnumerable<PressReleaseSectionKind> declaredSections,
        string source
    )
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        var declared = new HashSet<PressReleaseSectionKind>(declaredSections);
        var root = new List<HtmlNode>();
        var values = new HashSet<string>(StringComparer.Ordinal);
        var sectionValues = new Dictionary<PressReleaseSectionKind, HashSet<string>>();
        PressReleaseSectionKind? open = null;
        var openNodes = new List<HtmlNode>();
        var position = 0;

        while (position < text.Length)
        {
            var start = text.IndexOf("{{", position, StringComparison.Ordinal);

            if (start < 0)
            {
                AddLiteral(open == null ? root : openNodes, text.Substring(position));
                break;
            }

            AddLiteral(open == null ? root : openNodes, text.Substring(position, start - position));
            var end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);

            if (end < 0)
                throw Invalid(source, "A marker is not closed with }}.");

            var marker = text.Substring(start + 2, end - start - 2).Trim();

            if (marker.Length == 0)
                throw Invalid(source, "An empty marker is not allowed.");

            if (marker[0] == '#')
            {
                var name = marker.Substring(1).Trim();

                if (!SectionNames.TryGetValue(name, out var kind))
                    throw Invalid(source, "Unknown section marker \"{{#" + name + "}}\".");

                if (open != null)
                    throw Invalid(source, "Section markers must not nest.");

                if (!declared.Contains(kind))
                    throw Invalid(
                        source,
                        "The template uses section \""
                            + name
                            + "\", which the pack does not declare."
                    );

                if (sectionValues.ContainsKey(kind))
                    throw Invalid(source, "Section \"" + name + "\" appears more than once.");

                sectionValues[kind] = new HashSet<string>(StringComparer.Ordinal);
                open = kind;
                openNodes = new List<HtmlNode>();
            }
            else if (marker[0] == '/')
            {
                var name = marker.Substring(1).Trim();

                if (
                    !SectionNames.TryGetValue(name, out var kind)
                    || open != kind
                    || !sectionValues.ContainsKey(kind)
                )
                    throw Invalid(source, "Section marker \"{{/" + name + "}}\" does not match.");

                root.Add(new HtmlSectionNode(kind, openNodes));
                open = null;
                openNodes = null!;
            }
            else
            {
                var common = Array.IndexOf(CommonValues, marker) >= 0;
                PressReleaseSectionKind? owner = null;

                foreach (var pair in SectionValues)
                    if (Array.IndexOf(pair.Value, marker) >= 0)
                        owner = pair.Key;

                if (!common && owner == null)
                    throw Invalid(source, "Unknown value marker \"{{" + marker + "}}\".");

                if (common && open != null)
                    throw Invalid(
                        source,
                        "Value marker \"{{" + marker + "}}\" must be outside section regions."
                    );

                if (owner != null && open != owner)
                    throw Invalid(
                        source,
                        "Value marker \"{{" + marker + "}}\" must appear inside its section region."
                    );

                var target = open == null ? values : sectionValues[open.Value];
                target.Add(marker);
                var node = new HtmlValueNode(marker);

                if (open == null)
                    root.Add(node);
                else
                    openNodes.Add(node);
            }

            position = end + 2;
        }

        if (open != null)
            throw Invalid(source, "Section \"" + open.Value + "\" is not closed.");

        foreach (var name in CommonValues)
            if (!values.Contains(name))
                throw Invalid(source, "Required value marker \"{{" + name + "}}\" is missing.");

        foreach (var kind in declared)
        {
            if (kind == PressReleaseSectionKind.Contact)
                continue;

            if (!sectionValues.TryGetValue(kind, out var present))
                throw Invalid(
                    source,
                    "Required section region \"" + SectionMarker(kind) + "\" is missing."
                );

            foreach (var name in SectionValues[kind])
                if (!present.Contains(name))
                    throw Invalid(
                        source,
                        "Required value marker \"{{" + name + "}}\" is missing from its section."
                    );
        }

        foreach (var kind in sectionValues.Keys)
            if (!declared.Contains(kind))
                throw Invalid(
                    source,
                    "Section \"" + SectionMarker(kind) + "\" is not supported by the pack."
                );

        var result = new HtmlTemplate(root);
        result.Sections.UnionWith(sectionValues.Keys);
        return result;
    }

    internal string Render(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyCollection<PressReleaseSectionKind> presentSections
    )
    {
        var present = new HashSet<PressReleaseSectionKind>(presentSections);
        var output = new StringBuilder();

        foreach (var node in nodes)
            node.Append(output, values, present);

        return output.ToString();
    }

    private static void AddLiteral(List<HtmlNode> nodes, string text)
    {
        if (text.Length != 0)
            nodes.Add(new HtmlLiteralNode(text));
    }

    private static string SectionMarker(PressReleaseSectionKind kind) =>
        kind switch
        {
            PressReleaseSectionKind.Specifications => "{{#specifications}}",
            PressReleaseSectionKind.FactBox => "{{#factbox}}",
            PressReleaseSectionKind.Media => "{{#media}}",
            PressReleaseSectionKind.CompanyDescriptions => "{{#about}}",
            PressReleaseSectionKind.Editorial => "{{#editorial}}",
            _ => kind.ToString(),
        };

    private static DocumentException Invalid(string source, string message) =>
        new DocumentException(DocumentFailureReason.InvalidTemplate, source + ": " + message);

    private abstract class HtmlNode
    {
        internal abstract void Append(
            StringBuilder output,
            IReadOnlyDictionary<string, string> values,
            HashSet<PressReleaseSectionKind> presentSections
        );
    }

    private sealed class HtmlLiteralNode : HtmlNode
    {
        private readonly string text;

        internal HtmlLiteralNode(string text) => this.text = text;

        internal override void Append(
            StringBuilder output,
            IReadOnlyDictionary<string, string> values,
            HashSet<PressReleaseSectionKind> presentSections
        ) => output.Append(text);
    }

    private sealed class HtmlValueNode : HtmlNode
    {
        private readonly string name;

        internal HtmlValueNode(string name) => this.name = name;

        internal override void Append(
            StringBuilder output,
            IReadOnlyDictionary<string, string> values,
            HashSet<PressReleaseSectionKind> presentSections
        )
        {
            if (!values.TryGetValue(name, out var value))
                throw new DocumentException(
                    DocumentFailureReason.InvalidTemplate,
                    "The HTML template value \"" + name + "\" has no generated content."
                );

            output.Append(value);
        }
    }

    private sealed class HtmlSectionNode : HtmlNode
    {
        private readonly PressReleaseSectionKind kind;
        private readonly IReadOnlyList<HtmlNode> children;

        internal HtmlSectionNode(PressReleaseSectionKind kind, IReadOnlyList<HtmlNode> children)
        {
            this.kind = kind;
            this.children = children;
        }

        internal override void Append(
            StringBuilder output,
            IReadOnlyDictionary<string, string> values,
            HashSet<PressReleaseSectionKind> presentSections
        )
        {
            if (!presentSections.Contains(kind))
                return;

            foreach (var child in children)
                child.Append(output, values, presentSections);
        }
    }
}
