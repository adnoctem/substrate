using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace PSFoundation.Security;

public sealed class WindowsEventField
{
    public string? Name { get; }
    public string Value { get; }
    internal WindowsEventField(string? name, string value) { Name = name; Value = value; }
}
public sealed class WindowsEventPayload
{
    public IReadOnlyList<WindowsEventField> Fields { get; }
    public string? UserDataXml { get; }
    private WindowsEventPayload(IEnumerable<WindowsEventField> fields, string? userData)
    { Fields = Array.AsReadOnly(fields.ToArray()); UserDataXml = userData; }
    /// <summary>Parses bounded event XML without DTDs or external entities. Keeps unnamed and repeated fields in source order.</summary>
    public static WindowsEventPayload Parse(string xml)
    {
        if (xml == null)
            throw new ArgumentNullException(nameof(xml));
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 };
        using (var text = new StringReader(xml))
        using (var reader = XmlReader.Create(text, settings))
        {
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null || root.Name.LocalName != "Event" || (root.Name.NamespaceName.Length != 0 && root.Name.NamespaceName != "http://schemas.microsoft.com/win/2004/08/events/event"))
                throw new InvalidDataException("Expected a Windows event XML document.");
            var ns = root.Name.Namespace;
            var fields = root.Element(ns + "EventData")?.Elements(ns + "Data").Select(element => new WindowsEventField((string?)element.Attribute("Name"), element.Value))
                ?? Enumerable.Empty<WindowsEventField>();
            return new WindowsEventPayload(fields, root.Element(ns + "UserData")?.ToString(SaveOptions.DisableFormatting));
        }
    }
    public string? GetValue(string name, StringComparison comparison = StringComparison.Ordinal)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        return Fields.LastOrDefault(field => string.Equals(field.Name, name, comparison))?.Value;
    }
}
