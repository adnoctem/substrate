using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Documents;

/// <summary>Output sections that a template pack can expose.</summary>
public enum PressReleaseSectionKind
{
    Specifications,
    FactBox,
    Media,
    CompanyDescriptions,
    Editorial,
    Contact,
}

/// <summary>One language of a loaded template pack, including validated layout inputs.</summary>
public sealed class TemplateLanguage
{
    public string Code { get; }
    public string Masthead { get; }
    public string DatelineSeparator { get; }
    public string SpecificationSeparator { get; }
    public IReadOnlyList<PressReleaseSectionKind> Sections { get; }

    internal HtmlTemplate Html { get; }
    internal byte[] Docx { get; }

    internal TemplateLanguage(
        string code,
        string masthead,
        string datelineSeparator,
        string specificationSeparator,
        IEnumerable<PressReleaseSectionKind> sections,
        HtmlTemplate html,
        byte[] docx
    )
    {
        Code = code;
        Masthead = masthead;
        DatelineSeparator = datelineSeparator;
        SpecificationSeparator = specificationSeparator;
        Sections = Array.AsReadOnly(sections.ToArray());
        Html = html;
        Docx = docx;
    }
}

/// <summary>A versioned, validated template pack loaded from a pack.json manifest and its referenced files.</summary>
/// <remarks>The pack defines schema compatibility, languages, localized fixed labels, output section mappings and the
/// layout inputs. All referenced files are read and hashed before a pack is returned.</remarks>
public sealed class TemplatePack
{
    private const int MaximumManifestBytes = 1024 * 1024;

    public string Version { get; }
    public string OutputLayoutVersion { get; }
    public PressReleaseSchema Schema { get; }
    public IReadOnlyDictionary<string, TemplateLanguage> Languages { get; }

    private TemplatePack(
        string version,
        string outputLayoutVersion,
        PressReleaseSchema schema,
        Dictionary<string, TemplateLanguage> languages
    )
    {
        Version = version;
        OutputLayoutVersion = outputLayoutVersion;
        Schema = schema;
        Languages = new ReadOnlyDictionary<string, TemplateLanguage>(languages);
    }

    /// <summary>Loads and validates the pack rooted at the supplied directory.</summary>
    /// <exception cref="DocumentException">The manifest, a referenced file or the HTML template is invalid.</exception>
    public static TemplatePack Load(FileSystemPath directory)
    {
        if (directory == null)
            throw new ArgumentNullException(nameof(directory));

        if (!Directory.Exists(directory.Value))
            throw Invalid("The template pack directory does not exist: " + directory.Value + ".");

        var manifestPath = Path.Combine(directory.Value, "pack.json");

        if (!File.Exists(manifestPath))
            throw Invalid("The template pack manifest pack.json does not exist.");

        var fields = Fields(
            ReadManifest(manifestPath),
            "pack.json",
            ["packVersion", "schema", "schemaVersions", "outputLayoutVersion", "languages"],
            "companyDescriptions"
        );
        var version = Required(Text(fields, "packVersion"), "packVersion");
        var outputLayoutVersion = Required(
            Text(fields, "outputLayoutVersion"),
            "outputLayoutVersion"
        );
        var schemaId = Required(Text(fields, "schema"), "schema");

        if (!string.Equals(schemaId, PressReleaseTags.SchemaId, StringComparison.Ordinal))
            throw new DocumentException(
                DocumentFailureReason.UnsupportedTemplateVersion,
                "The template pack targets schema \""
                    + schemaId
                    + "\"; this library supports "
                    + PressReleaseTags.SchemaId
                    + "."
            );

        var schemaVersions = Unique(
            StringArray(fields, "schemaVersions", "pack.json", true)
                .Select(value => Required(value, "schemaVersions"))
                .ToArray(),
            "schemaVersions"
        );

        if (schemaVersions.Length == 0)
            throw Invalid("\"schemaVersions\" requires at least one version.");

        var companyDescriptions =
            fields.TryGetValue("companyDescriptions", out var companyElement)
            && companyElement.GetAttribute("type") != "null"
                ? Unique(
                    StringArray(fields, "companyDescriptions", "pack.json", false)
                        .Select(value => Required(value, "companyDescriptions"))
                        .ToArray(),
                    "companyDescriptions"
                )
                : [];

        var languageElements = Array(fields, "languages", "pack.json", true);
        var codes = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < languageElements.Length; index++)
        {
            var language = Fields(
                languageElements[index],
                "languages[" + index + "]",
                [
                    "code",
                    "masthead",
                    "datelineSeparator",
                    "specificationSeparator",
                    "html",
                    "docx",
                    "sections",
                ]
            );
            var code = Required(Text(language, "code"), "languages[].code");

            if (!codes.Add(code))
                throw Invalid("The language \"" + code + "\" appears more than once.");
        }

        if (codes.Count == 0)
            throw Invalid("At least one language is required.");

        PressReleaseSchema schema;

        try
        {
            schema = new PressReleaseSchema(codes, schemaVersions, companyDescriptions);
        }
        catch (ArgumentException ex)
        {
            throw new DocumentException(
                DocumentFailureReason.InvalidTemplatePack,
                "The template pack schema declaration is invalid: " + ex.Message,
                ex
            );
        }

        var languages = new Dictionary<string, TemplateLanguage>(StringComparer.Ordinal);

        for (var index = 0; index < languageElements.Length; index++)
        {
            var languageFields = Fields(
                languageElements[index],
                "languages[" + index + "]",
                [
                    "code",
                    "masthead",
                    "datelineSeparator",
                    "specificationSeparator",
                    "html",
                    "docx",
                    "sections",
                ]
            );
            var code = Required(Text(languageFields, "code"), "languages[].code");
            var sections = Sections(
                StringArray(languageFields, "sections", "languages[].sections", true),
                code
            );

            if (!sections.Contains(PressReleaseSectionKind.Contact))
                throw Invalid("Language \"" + code + "\" must map the required contact section.");

            var declaresCompanyDescriptions = schema.CompanyDescriptionIds.Count != 0;

            if (
                declaresCompanyDescriptions
                != sections.Contains(PressReleaseSectionKind.CompanyDescriptions)
            )
                throw Invalid(
                    "Language \""
                        + code
                        + "\" must map company descriptions exactly when the schema declares them."
                );

            var htmlSource = "HTML template for \"" + code + "\"";
            var htmlFile = Fields(
                Object(languageFields, "html", "languages[].html"),
                "languages[].html",
                ["path", "sha256"]
            );
            var docxFile = Fields(
                Object(languageFields, "docx", "languages[].docx"),
                "languages[].docx",
                ["path", "sha256"]
            );
            var htmlBytes = Asset(directory, htmlFile, htmlSource);
            var docxBytes = Asset(directory, docxFile, "DOCX base for \"" + code + "\"");
            var html = HtmlTemplate.Parse(Decode(htmlBytes, htmlSource), sections, htmlSource);
            languages.Add(
                code,
                new TemplateLanguage(
                    code,
                    NonEmpty(Text(languageFields, "masthead"), "languages[].masthead"),
                    Separator(
                        Text(languageFields, "datelineSeparator"),
                        "languages[].datelineSeparator"
                    ),
                    Separator(
                        Text(languageFields, "specificationSeparator"),
                        "languages[].specificationSeparator"
                    ),
                    sections,
                    html,
                    docxBytes
                )
            );
        }

        return new TemplatePack(version, outputLayoutVersion, schema, languages);
    }

    internal static TemplateLanguage SelectLanguage(PressRelease release, TemplatePack pack)
    {
        if (!pack.Languages.TryGetValue(release.Language, out var language))
            throw new DocumentException(
                DocumentFailureReason.UnsupportedLanguage,
                "The template pack does not support language \"" + release.Language + "\"."
            );

        if (
            !pack.Schema.TemplateVersions.Any(version =>
                string.Equals(version, release.TemplateVersion, StringComparison.Ordinal)
            )
        )
            throw new DocumentException(
                DocumentFailureReason.UnsupportedTemplateVersion,
                "The template pack does not support authoring template version \""
                    + release.TemplateVersion
                    + "\"."
            );

        return language;
    }

    private static XmlElement ReadManifest(string path)
    {
        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (FileNotFoundException)
        {
            throw Invalid("The template pack manifest pack.json does not exist.");
        }

        if (bytes.Length > MaximumManifestBytes)
            throw Invalid("The template pack manifest exceeds the size limit.");

        var quotas = new XmlDictionaryReaderQuotas
        {
            MaxDepth = 16,
            MaxStringContentLength = MaximumManifestBytes,
            MaxArrayLength = 10000,
            MaxNameTableCharCount = 4096,
        };

        try
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
            {
                var document = new XmlDocument { XmlResolver = null };
                document.Load(reader);

                return document.DocumentElement ?? throw Invalid("The manifest is empty.");
            }
        }
        catch (Exception ex) when (ex is XmlException or SerializationException or FormatException)
        {
            throw new DocumentException(
                DocumentFailureReason.InvalidTemplatePack,
                "The template pack manifest is invalid: " + ex.Message,
                ex
            );
        }
    }

    private static Dictionary<string, XmlElement> Fields(
        XmlElement element,
        string description,
        string[] required,
        params string[] optional
    )
    {
        RequireType(element, "object", description);

        if (element.HasAttribute("__type"))
            throw Invalid(description + ": type metadata is not allowed.");

        var fields = new Dictionary<string, XmlElement>(StringComparer.Ordinal);

        foreach (var child in element.ChildNodes.OfType<XmlElement>())
        {
            if (
                child.NamespaceURI.Length != 0
                || (
                    !required.Contains(child.Name, StringComparer.Ordinal)
                    && !optional.Contains(child.Name, StringComparer.Ordinal)
                )
                || fields.ContainsKey(child.Name)
            )
                throw Invalid(
                    description + ": unexpected or duplicate member \"" + child.Name + "\"."
                );

            fields.Add(child.Name, child);
        }

        foreach (var name in required)
            if (!fields.ContainsKey(name))
                throw Invalid(description + ": missing member \"" + name + "\".");

        return fields;
    }

    private static string Text(Dictionary<string, XmlElement> fields, string name)
    {
        if (!fields.TryGetValue(name, out var element))
            throw Invalid("Missing member \"" + name + "\".");

        if (element.GetAttribute("type") == "null")
            throw Invalid("Member \"" + name + "\" cannot be null.");

        RequireType(element, "string", "\"" + name + "\"");
        return element.InnerText;
    }

    private static XmlElement[] Array(
        Dictionary<string, XmlElement> fields,
        string name,
        string description,
        bool required
    )
    {
        if (!fields.TryGetValue(name, out var element))
            return required ? throw Invalid(description + ": missing \"" + name + "\".") : [];

        if (element.GetAttribute("type") == "null")
            return required
                ? throw Invalid(description + ": \"" + name + "\" cannot be null.")
                : [];

        RequireType(element, "array", description + ": \"" + name + "\"");
        return element.ChildNodes.OfType<XmlElement>().ToArray();
    }

    private static string[] StringArray(
        Dictionary<string, XmlElement> fields,
        string name,
        string description,
        bool required
    )
    {
        var elements = Array(fields, name, description, required);
        var values = new string[elements.Length];

        for (var index = 0; index < elements.Length; index++)
        {
            RequireType(elements[index], "string", description + ": \"" + name + "\"");
            values[index] = elements[index].InnerText;
        }

        return values;
    }

    private static XmlElement Object(
        Dictionary<string, XmlElement> fields,
        string name,
        string description
    )
    {
        if (!fields.TryGetValue(name, out var element))
            throw Invalid(description + ": missing \"" + name + "\".");

        if (element.GetAttribute("type") == "null")
            throw Invalid(description + ": \"" + name + "\" cannot be null.");

        RequireType(element, "object", description + ": \"" + name + "\"");
        return element;
    }

    private static void RequireType(XmlElement element, string type, string description)
    {
        if (!string.Equals(element.GetAttribute("type"), type, StringComparison.Ordinal))
            throw Invalid(description + " must be a JSON " + type + ".");
    }

    private static PressReleaseSectionKind[] Sections(string[] names, string language)
    {
        if (names.Length == 0)
            throw Invalid("Language \"" + language + "\" must declare its output sections.");

        var result = new List<PressReleaseSectionKind>();
        var seen = new HashSet<PressReleaseSectionKind>();

        foreach (var name in names)
        {
            if (
                string.IsNullOrWhiteSpace(name)
                || name.Trim() != name
                || !TrySection(name, out var kind)
            )
                throw Invalid(
                    "Language \"" + language + "\" declares unknown section \"" + name + "\"."
                );

            if (!seen.Add(kind))
                throw Invalid(
                    "Language \"" + language + "\" declares section \"" + name + "\" twice."
                );

            result.Add(kind);
        }

        return result.ToArray();
    }

    private static bool TrySection(string name, out PressReleaseSectionKind kind)
    {
        switch (name)
        {
            case "specifications":
                kind = PressReleaseSectionKind.Specifications;
                return true;
            case "factBox":
                kind = PressReleaseSectionKind.FactBox;
                return true;
            case "media":
                kind = PressReleaseSectionKind.Media;
                return true;
            case "companyDescriptions":
                kind = PressReleaseSectionKind.CompanyDescriptions;
                return true;
            case "editorial":
                kind = PressReleaseSectionKind.Editorial;
                return true;
            case "contact":
                kind = PressReleaseSectionKind.Contact;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static byte[] Asset(
        FileSystemPath root,
        Dictionary<string, XmlElement> file,
        string description
    )
    {
        var relative = RelativePath(Text(file, "path"), description);
        var full = Path.GetFullPath(Path.Combine(root.Value, relative));
        var rootPath = Path.GetFullPath(root.Value);
        var rootPrefix =
            rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new DocumentException(
                DocumentFailureReason.UnsafeTemplatePath,
                description + ": the path leaves the template pack directory."
            );

        if (!File.Exists(full))
            throw Missing(description, "The file \"" + relative + "\" does not exist.");

        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (FileNotFoundException)
        {
            throw Missing(description, "The file \"" + relative + "\" does not exist.");
        }

        var expected = Hash(Text(file, "sha256"), description);
        var actual = ComputeHash(bytes);

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new DocumentException(
                DocumentFailureReason.TemplateAssetHashMismatch,
                description + ": SHA-256 does not match the manifest."
            );

        return bytes;
    }

    private static string RelativePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new DocumentException(
                DocumentFailureReason.UnsafeTemplatePath,
                description + ": a relative file path is required."
            );

        if (Path.IsPathRooted(path) || path.IndexOf(':') >= 0)
            throw new DocumentException(
                DocumentFailureReason.UnsafeTemplatePath,
                description + ": the file path must be relative."
            );

        var segments = path.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.None
        );

        if (
            segments.Length == 0
            || segments.Any(segment =>
                segment.Length == 0
                || segment == "."
                || segment == ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            )
        )
            throw new DocumentException(
                DocumentFailureReason.UnsafeTemplatePath,
                description + ": the file path is not a simple relative path."
            );

        return Path.Combine(segments);
    }

    private static string Hash(string value, string description)
    {
        if (value.Length != 64 || !value.All(character => Uri.IsHexDigit(character)))
            throw new DocumentException(
                DocumentFailureReason.InvalidTemplatePack,
                description + ": a 64-character SHA-256 value is required."
            );

        return value;
    }

    private static string ComputeHash(byte[] bytes)
    {
        using (var sha = SHA256.Create())
            return string.Concat(sha.ComputeHash(bytes).Select(item => item.ToString("x2")));
    }

    private static string Decode(byte[] bytes, string description)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return text.Length != 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }
        catch (DecoderFallbackException ex)
        {
            throw new DocumentException(
                DocumentFailureReason.InvalidTemplate,
                description + ": the file is not valid UTF-8.",
                ex
            );
        }
    }

    private static string[] Unique(string[] values, string name)
    {
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw Invalid("Values in \"" + name + "\" must be unique.");

        return values;
    }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() != value
            ? throw Invalid("\"" + name + "\" requires a nonempty trimmed value.")
            : value;

    private static string NonEmpty(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw Invalid("\"" + name + "\" requires a nonempty value.")
            : value;

    private static string Separator(string value, string name) =>
        value.Length == 0 ? throw Invalid("\"" + name + "\" cannot be empty.") : value;

    private static DocumentException Invalid(string message) =>
        new DocumentException(DocumentFailureReason.InvalidTemplatePack, message);

    private static DocumentException Missing(string description, string message) =>
        new DocumentException(
            DocumentFailureReason.MissingTemplateAsset,
            description + ": " + message
        );
}
