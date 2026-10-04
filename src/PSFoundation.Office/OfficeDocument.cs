using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace PSFoundation.Office;

// Ordered data-only JSON for the existing Office wire contract. Never reflects or instantiates application types.
internal sealed class OfficeDocument
{
    private readonly List<KeyValuePair<string, object?>> fields = new List<KeyValuePair<string, object?>>();
    internal IEnumerable<string> Names => fields.Select(pair => pair.Key);
    internal object? this[string name]
    {
        get => fields.FirstOrDefault(pair => Equal(pair.Key, name)).Value;
        set
        {
            var index = fields.FindIndex(pair => Equal(pair.Key, name));
            if (index < 0)
                fields.Add(new KeyValuePair<string, object?>(name, value));
            else
                fields[index] = new KeyValuePair<string, object?>(fields[index].Key, value);
        }
    }
    internal static OfficeDocument Create(params object?[] pairs)
    {
        if (pairs.Length % 2 != 0)
            throw new ArgumentException("Expected field/value pairs.", nameof(pairs));
        var value = new OfficeDocument();
        for (var index = 0; index < pairs.Length; index += 2)
            value[(string)pairs[index]!] = pairs[index + 1];
        return value;
    }
    internal bool Has(string name) => fields.Any(pair => Equal(pair.Key, name));
    internal string? Text(string name) => this[name] == null ? null : this[name] is string text ? text : throw Invalid();
    internal bool Boolean(string name) => this[name] is bool value ? value : throw Invalid();
    internal long Integer(string name) => this[name] is long value ? value : this[name] is int small ? small : throw Invalid();
    internal OfficeDocument Object(string name) => this[name] is OfficeDocument value ? value : throw Invalid();
    internal object?[] Array(string name) => this[name] is object?[] value ? value : throw Invalid();
    internal string[] Strings(string name) => Array(name).Select(value => value as string ?? throw Invalid()).ToArray();
    internal void Require(IEnumerable<string> required, params string[] optional)
    {
        var names = required.ToArray();
        if (names.Any(name => !Has(name)) || Names.Any(name => !names.Concat(optional).Contains(name, StringComparer.OrdinalIgnoreCase)))
            throw Invalid();
    }
    internal static OfficeDocument Parse(string json)
    {
        try
        { return Read(OfficeJsonContract.Read(json)) as OfficeDocument ?? throw Invalid(); }
        catch (XmlException) { throw Invalid(); }
    }
    internal OfficeDocument Copy() => Parse(ToJson());
    internal string ToJson() => Json(this);
    internal static string Fingerprint(object? value, bool escapeHtml = false)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Json(value, escapeHtml)))).Replace("-", "").ToLowerInvariant();
    }
    internal static bool MatchesFingerprint(object? value, string? fingerprint)
        => Equal(Fingerprint(value), fingerprint) || Equal(Fingerprint(value, true), fingerprint);
    internal static string Json(object? value, bool escapeHtml = false)
    {
        var builder = new StringBuilder();
        Write(value, builder, 0, escapeHtml);
        if (builder.Length > OfficeMediaManifest.MaximumJsonBytes || Encoding.UTF8.GetByteCount(builder.ToString()) > OfficeMediaManifest.MaximumJsonBytes)
            throw Invalid();
        return builder.ToString();
    }
    private static object? Read(XmlElement element)
    {
        if (element.HasAttribute("__type"))
            throw Invalid();
        switch (element.GetAttribute("type"))
        {
            case "null":
                return null;
            case "boolean":
                return XmlConvert.ToBoolean(element.InnerText);
            case "number":
                if (long.TryParse(element.InnerText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                    return number;
                return new JsonNumber(element.InnerText);
            case "array":
                return element.ChildNodes.OfType<XmlElement>().Select(Read).ToArray();
            case "object":
                var result = new OfficeDocument();
                foreach (var child in element.ChildNodes.OfType<XmlElement>())
                {
                    if (child.NamespaceURI.Length != 0 || result.Has(child.Name))
                        throw Invalid();
                    result[child.Name] = Read(child);
                }
                return result;
            case "":
            case "string":
                return element.InnerText;
            default:
                throw Invalid();
        }
    }
    private static void Write(object? value, StringBuilder output, int depth, bool escapeHtml)
    {
        if (depth > 30 || output.Length > OfficeMediaManifest.MaximumJsonBytes)
            throw Invalid();
        if (value == null)
        { output.Append("null"); return; }
        if (value is string text)
        { Quote(text, output, escapeHtml); return; }
        if (value is bool boolean)
        { output.Append(boolean ? "true" : "false"); return; }
        if (value is JsonNumber number)
        { output.Append(number.Text); return; }
        if (value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong)
        { output.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
        var comma = false;
        if (value is OfficeDocument document)
        {
            output.Append('{');
            foreach (var pair in document.fields)
            {
                if (comma)
                    output.Append(',');
                comma = true;
                Quote(pair.Key, output, escapeHtml);
                output.Append(':');
                Write(pair.Value, output, depth + 1, escapeHtml);
            }
            output.Append('}');
            return;
        }
        if (value is IEnumerable sequence)
        {
            output.Append('[');
            foreach (var item in sequence)
            { if (comma) output.Append(','); comma = true; Write(item, output, depth + 1, escapeHtml); }
            output.Append(']');
            return;
        }
        throw Invalid();
    }
    private static void Quote(string text, StringBuilder output, bool escapeHtml)
    {
        output.Append('"');
        foreach (var character in text)
        {
            switch (character)
            {
                case '"':
                    output.Append("\\\"");
                    break;
                case '\\':
                    output.Append("\\\\");
                    break;
                case '\b':
                    output.Append("\\b");
                    break;
                case '\f':
                    output.Append("\\f");
                    break;
                case '\n':
                    output.Append("\\n");
                    break;
                case '\r':
                    output.Append("\\r");
                    break;
                case '\t':
                    output.Append("\\t");
                    break;
                default:
                    if (character < 32 || character == '\u0085' || character == '\u2028' || character == '\u2029' || escapeHtml && "<>&'".IndexOf(character) >= 0)
                        output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        output.Append(character);
                    break;
            }
        }
        output.Append('"');
    }
    internal static bool Equal(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    internal static OfficeException Invalid() => new OfficeException(OfficeFailureReason.InvalidContract, "Invalid Office data contract.");
    private sealed class JsonNumber
    {
        internal string Text { get; }
        internal JsonNumber(string text) { Text = text; }
    }
}

/// <summary>Stable Office observation serialization shared by plans, recovery records and other applications.</summary>
public static class OfficeInventorySerializer
{
    public static string ToJson(OfficeInventory inventory, bool escapeHtml = false) => OfficeDocument.Json(Document(inventory), escapeHtml);
    public static string GetFingerprint(OfficeInventory inventory, bool escapeHtml = false) => OfficeDocument.Fingerprint(Document(inventory), escapeHtml);
    internal static OfficeDocument Document(OfficeInventory inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        return OfficeDocument.Create("SchemaVersion", 1, "MachineId", inventory.MachineId,
            "Products", inventory.Products.Select(product => OfficeDocument.Create("ProductId", product.ProductId,
                "Architecture", product.Architecture.HasValue ? ((int)product.Architecture.Value).ToString(CultureInfo.InvariantCulture) : null,
                "Version", product.Version?.ToString(), "VersionSource", product.InstalledVersion.Source?.ToString(), "Channel", product.Channel?.ToString(),
                "Languages", Strings(product.Languages), "PrimaryLanguage", product.PrimaryLanguage, "RegisteredLanguages", Strings(product.RegisteredLanguages),
                "ExcludeApp", Strings(product.ExcludedApplications), "Evidence", Strings(product.Evidence))).ToArray(),
            "Msi", inventory.Msi.Select(item => OfficeDocument.Create("ProductCode", item.ProductCode, "Name", item.Name, "Version", item.Version, "RegistryView", item.RegistryView.ToString(), "LanguageId", item.LanguageId, "ResourceKind", item.ResourceKind.ToString())).ToArray(),
            "RelatedComponents", inventory.RelatedComponents.Select(Related).ToArray(), "Unknowns", Strings(inventory.Unknowns),
            "RegisteredResources", inventory.RegistryRecords.Where(record => record.Path.SubKey.IndexOf(@"ClickToRun\ProductReleaseIDs", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(record => OfficeDocument.Create("View", record.View.ToString(), "Path", record.Path.SubKey, "SubKeys", Strings(record.SubKeys), "ActiveConfiguration", record.GetValue("ActiveConfiguration"), "Version", record.GetValue("Version"))).ToArray(),
            "AppPathEvidence", inventory.AppPaths.Select(path => OfficeDocument.Create("RegistryView", path.RegistryView.ToString(), "RegistryPath", path.RegistryPath,
                "RawTarget", path.RawTarget, "ResolvedTarget", path.ResolvedTarget, "State", path.State.ToString(), "Reason", path.Reason.ToString())).ToArray(),
            "LanguageEvidence", inventory.RegistryRecords.Where(record => record.Path.SubKey.EndsWith(@"\Common\LanguageResources", StringComparison.OrdinalIgnoreCase))
                .Select(record => OfficeDocument.Create("View", record.View.ToString(), "Path", record.Path.SubKey, "SKULanguage", record.GetValue("SKULanguage"), "InstallLanguage", record.GetValue("InstallLanguage"))).ToArray(),
            "VerificationLimitations", Strings(inventory.VerificationLimitations));
    }
    private static object?[]? Strings(IEnumerable<string>? values) => values?.Cast<object?>().ToArray();
    private static OfficeDocument Related(OfficeRelatedComponent item)
    {
        var value = OfficeDocument.Create("ProductCode", item.ProductCode, "Name", item.Name, "Version", item.Version, "RegistryView", item.RegistryView.ToString(), "Role", item.Role.ToString());
        if (item.Role == OfficeRelatedRole.PatchRegistration)
        { value["ParentProductCode"] = item.ParentProductCode; value["SystemComponent"] = item.SystemComponent; }
        return value;
    }
}
