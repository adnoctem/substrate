using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdNoctem.Substrate.Documents;

/// <summary>Authored emphasis. Presentation roles such as a bold heading are not marks.</summary>
[Flags]
public enum TextMarks
{
    None = 0,
    Bold = 1,
    Italic = 2,
}

/// <summary>Inline content of a paragraph, list item or table cell.</summary>
public abstract class DocumentInline
{
    private protected DocumentInline() { }

    internal abstract void AppendText(StringBuilder text);
}

/// <summary>Literal Unicode text with significant whitespace. Never markup.</summary>
public sealed class TextInline : DocumentInline
{
    public string Text { get; }
    public TextMarks Marks { get; }

    public TextInline(string text, TextMarks marks = TextMarks.None)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));

        if (text.Length == 0)
            throw new ArgumentException("Text must not be empty.", nameof(text));

        if (((int)marks & ~(int)(TextMarks.Bold | TextMarks.Italic)) != 0)
            throw new ArgumentOutOfRangeException(nameof(marks));

        Marks = marks;
    }

    internal override void AppendText(StringBuilder text) => text.Append(Text);
}

/// <summary>A deliberate line break inside a paragraph; not a paragraph boundary.</summary>
public sealed class LineBreakInline : DocumentInline
{
    public static LineBreakInline Instance { get; } = new LineBreakInline();

    private LineBreakInline() { }

    internal override void AppendText(StringBuilder text) => text.Append('\n');
}

/// <summary>An external link whose destination is independent of its visible content.</summary>
/// <remarks>Destinations are absolute http, https or mailto URIs. Content holds text and line breaks only.</remarks>
public sealed class HyperlinkInline : DocumentInline
{
    public Uri Destination { get; }
    public IReadOnlyList<DocumentInline> Content { get; }

    public HyperlinkInline(Uri destination, IEnumerable<DocumentInline> content)
    {
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));

        if (!destination.IsAbsoluteUri || !IsSupportedScheme(destination.Scheme))
            throw new ArgumentException(
                "Only absolute http, https and mailto destinations are supported.",
                nameof(destination)
            );

        Content = Inlines.Freeze(content, nameof(content));

        if (Content.Count == 0 || Content.Any(item => item is HyperlinkInline))
            throw new ArgumentException(
                "Link content must be nonempty and must not contain links.",
                nameof(content)
            );
    }

    internal static bool IsSupportedScheme(string scheme) =>
        scheme == Uri.UriSchemeHttp
        || scheme == Uri.UriSchemeHttps
        || scheme == Uri.UriSchemeMailto;

    internal override void AppendText(StringBuilder text)
    {
        foreach (var item in Content)
            item.AppendText(text);
    }
}

/// <summary>Block content of a rich-text field.</summary>
public abstract class DocumentBlock
{
    private protected DocumentBlock() { }
}

public sealed class ParagraphBlock : DocumentBlock
{
    public IReadOnlyList<DocumentInline> Content { get; }

    public ParagraphBlock(IEnumerable<DocumentInline> content)
    {
        Content = Inlines.Freeze(content, nameof(content));

        if (Content.Count == 0)
            throw new ArgumentException("A paragraph must not be empty.", nameof(content));
    }
}

public enum ListKind
{
    Bullet,
    Decimal,
}

/// <summary>An ordered sequence of items. Start is the first item's number; it is 1 for bullets.</summary>
public sealed class ListBlock : DocumentBlock
{
    public ListKind Kind { get; }
    public int Start { get; }
    public IReadOnlyList<ListItem> Items { get; }

    public ListBlock(ListKind kind, int start, IEnumerable<ListItem> items)
    {
        if (!Enum.IsDefined(typeof(ListKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start));

        Kind = kind;
        Start = kind == ListKind.Bullet ? 1 : start;
        Items = Array.AsReadOnly(
            (items ?? throw new ArgumentNullException(nameof(items))).ToArray()
        );

        if (Items.Count == 0 || Items.Any(item => item == null))
            throw new ArgumentException("A list needs at least one item.", nameof(items));
    }
}

/// <summary>One list paragraph and an optional nested list.</summary>
public sealed class ListItem
{
    public IReadOnlyList<DocumentInline> Content { get; }
    public ListBlock? Sublist { get; }

    public ListItem(IEnumerable<DocumentInline> content, ListBlock? sublist = null)
    {
        Content = Inlines.Freeze(content, nameof(content));

        if (Content.Count == 0)
            throw new ArgumentException("A list item must not be empty.", nameof(content));

        Sublist = sublist;
    }
}

/// <summary>Helpers for canonical inline sequences.</summary>
public static class Inlines
{
    /// <summary>Concatenates the visible text, using a line feed for line breaks.</summary>
    public static string ToPlainText(IEnumerable<DocumentInline> content)
    {
        var text = new StringBuilder();

        foreach (var item in content ?? throw new ArgumentNullException(nameof(content)))
            item.AppendText(text);

        return text.ToString();
    }

    /// <summary>Merges adjacent text with identical marks, so run fragmentation never changes the model.</summary>
    public static IReadOnlyList<DocumentInline> Normalize(IEnumerable<DocumentInline> content)
    {
        var result = new List<DocumentInline>();

        foreach (var item in content ?? throw new ArgumentNullException(nameof(content)))
        {
            if (
                item is TextInline text
                && result.Count != 0
                && result[result.Count - 1] is TextInline previous
                && previous.Marks == text.Marks
            )
                result[result.Count - 1] = new TextInline(previous.Text + text.Text, text.Marks);
            else if (item is HyperlinkInline link)
                result.Add(new HyperlinkInline(link.Destination, Normalize(link.Content)));
            else
                result.Add(item ?? throw new ArgumentException("Null inline.", nameof(content)));
        }

        return result.AsReadOnly();
    }

    internal static IReadOnlyList<DocumentInline> Freeze(
        IEnumerable<DocumentInline> content,
        string parameter
    )
    {
        var items = (content ?? throw new ArgumentNullException(parameter)).ToArray();

        if (items.Any(item => item == null))
            throw new ArgumentException("Null inline.", parameter);

        return Array.AsReadOnly(items);
    }
}
