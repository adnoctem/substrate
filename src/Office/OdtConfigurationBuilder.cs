using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace AdNoctem.Substrate.Office;

/// <summary>Creates secret-free ODT documents with explicit scope. Does not discover state, download media, write files or execute setup.exe.</summary>
public sealed class OdtConfigurationBuilder
{
    public XmlDocument Download(OfficeConfiguration configuration, string mediaPath) =>
        Add(configuration, mediaPath, false, false, false);

    public XmlDocument Install(OfficeConfiguration configuration, string mediaPath) =>
        Add(configuration, mediaPath, true, true, false);

    /// <remarks>Click-to-Run removal is a separate explicit operation. MSI removal is broad and must be authorized by the caller.</remarks>
    public XmlDocument Migrate(
        OfficeConfiguration configuration,
        string mediaPath,
        bool removeMsi
    ) => Add(configuration, mediaPath, true, true, removeMsi);

    public XmlDocument Update(OfficeConfiguration configuration, string mediaPath) =>
        Add(configuration, mediaPath, true, false, false);

    /// <remarks>The configuration specifies the complete resulting language and application selection, not only the added resources.</remarks>
    public XmlDocument AddLanguage(OfficeConfiguration configuration, string mediaPath) =>
        Add(configuration, mediaPath, true, false, false);

    public XmlDocument SetApplicationSelection(
        OfficeConfiguration configuration,
        string mediaPath
    ) => Add(configuration, mediaPath, true, false, false);

    public XmlDocument RemoveProducts(IEnumerable<string> productIds)
    {
        var ids = NormalizeProductIds(productIds);

        if (ids.Count == 0)
            throw new OfficeException(
                OfficeFailureReason.InvalidAuthority,
                "Product removal requires exact product IDs."
            );

        var document = NewDocument();
        var remove = Element(document.DocumentElement!, "Remove", "All", "FALSE");

        foreach (var id in ids)
            Element(remove, "Product", "ID", id);

        ExecutionProperties(document);

        return document;
    }

    public XmlDocument RemoveLanguage(OfficeProduct product, IEnumerable<string> languages)
    {
        if (!Enum.IsDefined(typeof(OfficeProduct), product))
            throw new ArgumentOutOfRangeException(nameof(product));

        var selected = OfficeLanguages.Normalize(languages);

        if (selected.Count == 0)
            throw new OfficeException(
                OfficeFailureReason.InvalidAuthority,
                "Language removal requires exact languages."
            );

        var document = NewDocument();
        var remove = Element(document.DocumentElement!, "Remove", "All", "FALSE");
        var item = Element(remove, "Product", "ID", product.ToString());

        foreach (var language in selected)
            Element(item, "Language", "ID", language);

        ExecutionProperties(document);

        return document;
    }

    public XmlDocument SetUpdateConfiguration(OfficeUpdateConfiguration settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var document = NewDocument();
        var updates = Element(document.DocumentElement!, "Updates");

        if (settings.Enabled.HasValue)
            updates.SetAttribute("Enabled", settings.Enabled.Value.ToString());

        if (settings.UpdatePath != null)
            updates.SetAttribute("UpdatePath", settings.UpdatePath);

        if (settings.TargetVersion != null)
            updates.SetAttribute("TargetVersion", settings.TargetVersion.ToString());

        if (settings.Channel.HasValue)
            updates.SetAttribute("Channel", settings.Channel.Value.ToString());

        ExecutionProperties(document);

        return document;
    }

    public XmlDocument SetApplicationPreferences(
        IEnumerable<OfficeApplicationPreference> preferences
    )
    {
        var items = (preferences ?? throw new ArgumentNullException(nameof(preferences))).ToArray();

        if (items.Length == 0 || items.Any(item => item == null))
            throw new OfficeException(
                OfficeFailureReason.InvalidConfiguration,
                "At least one application preference is required; null entries are invalid."
            );

        var document = NewDocument();
        var settings = Element(document.DocumentElement!, "AppSettings");

        foreach (var item in items)
            Element(
                settings,
                "User",
                "Key",
                item.Path.SubKey,
                "Name",
                item.Name,
                "Value",
                item.Value,
                "Type",
                item.Type == OfficePreferenceValueType.DWord ? "REG_DWORD" : "REG_SZ",
                "App",
                item.ApplicationId,
                "Id",
                item.Id
            );

        return document;
    }

    internal static IReadOnlyList<string> NormalizeProductIds(IEnumerable<string> productIds)
    {
        if (productIds == null)
            throw new ArgumentNullException(nameof(productIds));

        var result = new List<string>();

        foreach (var raw in productIds)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new OfficeException(
                    OfficeFailureReason.InvalidConfiguration,
                    "Empty identifiers are not allowed."
                );

            var id = raw.Trim();

            if (
                !OfficeVersionResolver.IsMatch(
                    id,
                    @"^(O365(ProPlus|Business)|ProPlus\d{0,4}|Standard\d{0,4}|Professional\d{0,4}|HomeBusiness\d{0,4}|HomeStudent\d{0,4}|Personal\d{0,4}|Visio(Pro|Std)\d{0,4}|Project(Pro|Std)\d{0,4}|Access\d{0,4}|Excel\d{0,4}|Outlook\d{0,4}|PowerPoint\d{0,4}|Word\d{0,4}|Publisher\d{0,4})(Retail|Volume)$"
                )
            )
                throw new OfficeException(
                    OfficeFailureReason.Unsupported,
                    "Removal selection is outside the supported Office product-ID families."
                );

            if (!result.Contains(id, StringComparer.OrdinalIgnoreCase))
                result.Add(id);
        }

        return result.AsReadOnly();
    }

    private static XmlDocument Add(
        OfficeConfiguration configuration,
        string mediaPath,
        bool execution,
        bool activate,
        bool removeMsi
    )
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        if (string.IsNullOrWhiteSpace(mediaPath) || execution && configuration.Version == null)
            throw new OfficeException(
                OfficeFailureReason.InvalidConfiguration,
                "Execution requires a target, media path, and pinned build."
            );

        var document = NewDocument();
        var root = document.DocumentElement!;
        var add = Element(
            root,
            "Add",
            "OfficeClientEdition",
            ((int)configuration.Architecture).ToString(CultureInfo.InvariantCulture),
            "Channel",
            configuration.Channel.ToString(),
            "SourcePath",
            mediaPath,
            "AllowCdnFallback",
            "FALSE"
        );

        if (configuration.Version != null)
            add.SetAttribute("Version", configuration.Version.ToString());

        var product = Element(add, "Product", "ID", configuration.Product.ToString());

        foreach (var language in configuration.Languages)
            Element(product, "Language", "ID", language);

        foreach (var application in configuration.ExcludedApplications)
            Element(product, "ExcludeApp", "ID", application.ToString());

        if (removeMsi)
            Element(root, "RemoveMSI");

        if (activate && configuration.IsVolumeProduct)
            Element(root, "Property", "Name", "AUTOACTIVATE", "Value", "1");

        if (execution)
            ExecutionProperties(document);

        return document;
    }

    private static XmlDocument NewDocument()
    {
        var document = new XmlDocument { XmlResolver = null };
        document.AppendChild(document.CreateElement("Configuration"));

        return document;
    }

    private static XmlElement Element(XmlElement parent, string name, params string[] attributes)
    {
        var element = parent.OwnerDocument!.CreateElement(name);

        for (var index = 0; index < attributes.Length; index += 2)
            element.SetAttribute(attributes[index], attributes[index + 1]);

        parent.AppendChild(element);

        return element;
    }

    private static void ExecutionProperties(XmlDocument document)
    {
        Element(document.DocumentElement!, "Display", "Level", "None", "AcceptEULA", "TRUE");
        Element(
            document.DocumentElement!,
            "Property",
            "Name",
            "FORCEAPPSHUTDOWN",
            "Value",
            "FALSE"
        );
    }
}
