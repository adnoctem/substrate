using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdNoctem.Substrate.Documents;

public enum DocumentDiagnosticSeverity
{
    Error,
    Warning,
}

/// <summary>Stable diagnostic identities. Names are a compatibility contract; messages are explanatory text only.</summary>
public enum DocumentDiagnosticCode
{
    InvalidPackage,
    UnsupportedDocumentType,
    MacroEnabledDocument,
    MissingMetadata,
    UnsupportedSchema,
    ConflictingMetadata,
    MissingRequiredField,
    DuplicateField,
    UnknownField,
    EmptyField,
    PlaceholderContent,
    IncompleteSection,
    UnboundContent,
    UnresolvedRevision,
    CommentsPresent,
    UnsupportedContent,
    UnsupportedList,
    InvalidStructure,
    InvalidTableRow,
    BrokenHyperlink,
    UnsafeHyperlink,
    IgnoredFormatting,
}

/// <summary>An actionable finding about a source document, with no document text beyond a short excerpt.</summary>
/// <remarks>Field is the content-control tag, when one applies. Location is a logical position such as "paragraph 3" or "row 2",
/// never an XML path. Remedy tells the author how to correct the source.</remarks>
public sealed class DocumentDiagnostic
{
    public DocumentDiagnosticSeverity Severity { get; }
    public DocumentDiagnosticCode Code { get; }
    public string? Field { get; }
    public string? Location { get; }
    public string Message { get; }
    public string Remedy { get; }

    public DocumentDiagnostic(
        DocumentDiagnosticSeverity severity,
        DocumentDiagnosticCode code,
        string? field,
        string? location,
        string message,
        string remedy
    )
    {
        if (!Enum.IsDefined(typeof(DocumentDiagnosticSeverity), severity))
            throw new ArgumentOutOfRangeException(nameof(severity));

        if (!Enum.IsDefined(typeof(DocumentDiagnosticCode), code))
            throw new ArgumentOutOfRangeException(nameof(code));

        Severity = severity;
        Code = code;
        Field = field;
        Location = location;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Remedy = remedy ?? throw new ArgumentNullException(nameof(remedy));
    }

    public override string ToString()
    {
        var text = new StringBuilder();
        text.Append(Severity).Append(' ').Append(Code).Append(": ");

        if (Field != null)
            text.Append(Field).Append(Location == null ? ": " : ", ");

        if (Location != null)
            text.Append(Location).Append(": ");

        return text.Append(Message).Append(' ').Append(Remedy).ToString();
    }

    internal static bool HasErrors(IEnumerable<DocumentDiagnostic> diagnostics) =>
        diagnostics.Any(item => item.Severity == DocumentDiagnosticSeverity.Error);
}

internal sealed class DiagnosticList
{
    private readonly List<DocumentDiagnostic> items = new List<DocumentDiagnostic>();

    internal IReadOnlyList<DocumentDiagnostic> Items => items.AsReadOnly();
    internal bool HasErrors => DocumentDiagnostic.HasErrors(items);

    internal void Error(
        DocumentDiagnosticCode code,
        string? field,
        string? location,
        string message,
        string remedy
    ) =>
        items.Add(
            new DocumentDiagnostic(
                DocumentDiagnosticSeverity.Error,
                code,
                field,
                location,
                message,
                remedy
            )
        );

    internal void Warning(
        DocumentDiagnosticCode code,
        string? field,
        string? location,
        string message,
        string remedy
    ) =>
        items.Add(
            new DocumentDiagnostic(
                DocumentDiagnosticSeverity.Warning,
                code,
                field,
                location,
                message,
                remedy
            )
        );

    internal void AddRange(IEnumerable<DocumentDiagnostic> diagnostics) =>
        items.AddRange(diagnostics);

    // Short excerpts locate unbound text without reproducing an unpublished release in logs.
    internal static string Excerpt(string text)
    {
        var compact = string.Join(
            " ",
            text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        );
        return compact.Length <= 40 ? compact : compact.Substring(0, 40) + "…";
    }
}
