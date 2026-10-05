using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace AdNoctem.Substrate.Office;

/// <summary>A manifest entry containing a relative media path, byte length, and SHA256 digest.</summary>
public sealed class OfficeMediaFile
{
    public string Path { get; }
    public long Length { get; }
    public string Hash { get; }
    public OfficeMediaFile(string path, long length, string hash)
    {
        if (!OfficeVersionResolver.IsMatch(path, @"^Office/Data/[A-Za-z0-9._/-]+$") || path.EndsWith("/", StringComparison.Ordinal)
            || OfficeVersionResolver.IsMatch(path, @"(^|/)\.\.?(/|$)|//|[. ](/|$)") || length <= 0 || !OfficeVersionResolver.IsMatch(hash, "^[a-fA-F0-9]{64}$"))
            throw new OfficeException(OfficeFailureReason.UnsafeManifest, "Invalid or colliding media file record.");
        Path = path;
        Length = length;
        Hash = hash.ToLowerInvariant();
    }
}

/// <summary>An immutable schema-2 media manifest. File records are assertions until compared with protected on-disk content.</summary>
public sealed class OfficeMediaManifest
{
    public const string FileName = "psfoundation-office-media.json";
    public const int MaximumJsonBytes = 16 * 1024 * 1024;
    public OfficeConfiguration Configuration { get; }
    public string VersionText { get; }
    public string ToolVersion { get; }
    public IReadOnlyList<OfficeMediaFile> Files { get; }
    public OfficeMediaManifest(OfficeConfiguration configuration, string toolVersion, IEnumerable<OfficeMediaFile> files)
        : this(configuration, configuration?.Version?.ToString() ?? "", toolVersion, files) { }
    private OfficeMediaManifest(OfficeConfiguration configuration, string versionText, string toolVersion, IEnumerable<OfficeMediaFile> files)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (configuration.Version == null)
            throw new OfficeException(OfficeFailureReason.ReprepareMedia, "A media manifest requires a pinned build.");
        if (toolVersion == null)
            throw new ArgumentNullException(nameof(toolVersion));
        var copy = (files ?? throw new ArgumentNullException(nameof(files))).ToArray();
        if (copy.Any(file => file == null) || copy.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Length)
            throw new OfficeException(OfficeFailureReason.UnsafeManifest, "Invalid or colliding media file record.");
        VersionText = versionText;
        ToolVersion = toolVersion;
        Files = Array.AsReadOnly(copy);
    }
    public static OfficeMediaManifest Parse(string json)
    {
        var fields = OfficeJsonContract.Fields(OfficeJsonContract.Read(json), "SchemaVersion", "Product", "Architecture", "Channel", "AvailableLanguages", "Version", "ToolVersion", "Files");
        var versionText = OfficeJsonContract.Text(fields["Version"]);
        if (OfficeJsonContract.Integer(fields["SchemaVersion"]) != 2 || !OfficeVersionResolver.IsMatch(versionText, @"^16\.0\.\d+\.\d+$"))
            throw new OfficeException(OfficeFailureReason.ReprepareMedia, "Unsupported or incomplete media schema; prepare again.");
        var languages = OfficeJsonContract.Array(fields["AvailableLanguages"]).Select(OfficeJsonContract.Text).ToArray();
        var available = OfficeLanguages.Normalize(languages);
        if (available.Count == 0 || available.Count != languages.Length)
            throw new OfficeException(OfficeFailureReason.InvalidMedia, "Media languages are empty or duplicated.");
        var product = OfficeJsonContract.Text(fields["Product"]);
        var architecture = OfficeJsonContract.TextOrNumber(fields["Architecture"]);
        var channel = OfficeJsonContract.Text(fields["Channel"]);
        if (!Enum.TryParse<OfficeProduct>(product, true, out var selectedProduct) || !Enum.IsDefined(typeof(OfficeProduct), selectedProduct)
            || !string.Equals(selectedProduct.ToString(), product, StringComparison.OrdinalIgnoreCase)
            || !Enum.TryParse<OfficeChannel>(channel, true, out var selectedChannel) || !Enum.IsDefined(typeof(OfficeChannel), selectedChannel)
            || !string.Equals(selectedChannel.ToString(), channel, StringComparison.OrdinalIgnoreCase) || architecture != "32" && architecture != "64"
            || !Version.TryParse(versionText, out var version))
            throw new OfficeException(OfficeFailureReason.InvalidMedia, "Invalid media product, architecture, channel or build.");
        var configuration = new OfficeConfiguration(selectedProduct, architecture == "64" ? OfficeArchitecture.X64 : OfficeArchitecture.X86, selectedChannel, available, version);
        var files = OfficeJsonContract.Array(fields["Files"]).Select(file =>
        {
            var record = OfficeJsonContract.Fields(file, "Path", "Length", "Hash");
            return new OfficeMediaFile(OfficeJsonContract.Text(record["Path"]), OfficeJsonContract.Integer(record["Length"]), OfficeJsonContract.Text(record["Hash"]));
        });
        return new OfficeMediaManifest(configuration, versionText, OfficeJsonContract.Text(fields["ToolVersion"]), files);
    }
    /// <summary>Serializes the schema only. This JSON is not a PowerShell compatibility fingerprint.</summary>
    public string ToJson()
    {
        var data = new ManifestData
        {
            SchemaVersion = 2,
            Product = Configuration.Product.ToString(),
            Architecture = ((int)Configuration.Architecture).ToString(CultureInfo.InvariantCulture),
            Channel = Configuration.Channel.ToString(),
            AvailableLanguages = Configuration.Languages.ToArray(),
            Version = VersionText,
            ToolVersion = ToolVersion,
            Files = Files.Select(file => new FileData { Path = file.Path, Length = file.Length, Hash = file.Hash }).ToArray()
        };
        using (var stream = new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(ManifestData)).WriteObject(stream, data);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
    [DataContract]
    private sealed class ManifestData
    {
        [DataMember(Order = 0)] public int SchemaVersion { get; set; }
        [DataMember(Order = 1)] public string? Product { get; set; }
        [DataMember(Order = 2)] public string? Architecture { get; set; }
        [DataMember(Order = 3)] public string? Channel { get; set; }
        [DataMember(Order = 4)] public string[]? AvailableLanguages { get; set; }
        [DataMember(Order = 5)] public string? Version { get; set; }
        [DataMember(Order = 6)] public string? ToolVersion { get; set; }
        [DataMember(Order = 7)] public FileData[]? Files { get; set; }
    }
    [DataContract]
    private sealed class FileData
    {
        [DataMember(Order = 0)] public string? Path { get; set; }
        [DataMember(Order = 1)] public long Length { get; set; }
        [DataMember(Order = 2)] public string? Hash { get; set; }
    }
}

// The framework JSON reader preserves duplicate members and value types, unlike a loose object/dictionary conversion.
// No CLR type metadata is interpreted. The schema decides every accepted member.
internal static class OfficeJsonContract
{
    internal static XmlElement Read(string json)
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));
        if (json.Length > OfficeMediaManifest.MaximumJsonBytes)
            throw Invalid();
        var bytes = new UTF8Encoding(false, true).GetBytes(json.TrimStart('\uFEFF'));
        if (bytes.Length > OfficeMediaManifest.MaximumJsonBytes)
            throw Invalid();
        var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 32, MaxStringContentLength = OfficeMediaManifest.MaximumJsonBytes, MaxArrayLength = 1000000, MaxNameTableCharCount = 65536 };
        using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
        {
            var document = new XmlDocument { XmlResolver = null };
            document.Load(reader);
            return document.DocumentElement ?? throw Invalid();
        }
    }
    internal static Dictionary<string, XmlElement> Fields(XmlElement element, params string[] required)
    {
        RequireType(element, "object");
        if (element.HasAttribute("__type"))
            throw Invalid();
        var fields = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);
        foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>())
        {
            if (child.NamespaceURI.Length != 0 || !required.Contains(child.Name, StringComparer.OrdinalIgnoreCase) || fields.ContainsKey(child.Name))
                throw Invalid();
            fields.Add(child.Name, child);
        }
        if (fields.Count != required.Length)
            throw Invalid();
        return fields;
    }
    internal static XmlElement[] Array(XmlElement element)
    { RequireType(element, "array"); return element.ChildNodes.OfType<XmlElement>().ToArray(); }
    internal static string Text(XmlElement element)
    { RequireType(element, "string"); return element.InnerText; }
    internal static string TextOrNumber(XmlElement element)
    { if (element.GetAttribute("type") == "number") return element.InnerText; return Text(element); }
    internal static long Integer(XmlElement element)
    {
        RequireType(element, "number");
        if (!long.TryParse(element.InnerText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            throw Invalid();
        return value;
    }
    private static void RequireType(XmlElement element, string type)
    { var actual = element.GetAttribute("type"); if ((actual.Length == 0 ? "string" : actual) != type) throw Invalid(); }
    private static OfficeException Invalid() => new OfficeException(OfficeFailureReason.InvalidContract, "Invalid Office JSON contract.");
}
