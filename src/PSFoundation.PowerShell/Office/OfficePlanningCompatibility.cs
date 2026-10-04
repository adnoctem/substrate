using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using PSFoundation.Office;
using PSFoundation.PowerShell.Windows;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Office;

public static partial class OfficeCompatibility
{
    private static OfficeConfiguration Configuration(object value) => Target(Text(Get(value, "TargetProductId"))!, Text(Get(value, "Architecture"))!, Text(Get(value, "Channel")),
        Strings(Get(value, "Language"))!, Text(Get(value, "Version")), Strings(Get(value, "ExcludeApp"))!);
    private static OfficeUpdateConfiguration Updates(object settings) => new OfficeUpdateConfiguration(
        Get(settings, "Enabled") is bool enabled ? enabled : (bool?)null, Text(Get(settings, "UpdatePath")),
        Get(settings, "TargetVersion") is object version ? System.Version.Parse(Text(version)!) : null,
        Get(settings, "Channel") is object channel ? (OfficeChannel)Enum.Parse(typeof(OfficeChannel), Text(channel)!, true) : (OfficeChannel?)null);
    private static OfficeApplicationPreference[] Preferences(object settings) => Items(Get(settings, "Preferences")).Select(value =>
    {
        var app = Text(Get(value, "App"))!;
        var application = string.Equals(app, "ppt16", StringComparison.OrdinalIgnoreCase) ? OfficePreferenceApplication.PowerPoint
            : (OfficePreferenceApplication)Enum.Parse(typeof(OfficePreferenceApplication), app.Substring(0, app.Length - 2), true);
        return new OfficeApplicationPreference(RegistryPath.Parse("HKCU\\" + Text(Get(value, "Key"))), Text(Get(value, "Name"))!, Text(Get(value, "Value"))!,
            string.Equals(Text(Get(value, "Type")), "REG_DWORD", StringComparison.OrdinalIgnoreCase) ? OfficePreferenceValueType.DWord : OfficePreferenceValueType.String,
            application, Text(Get(value, "Id"))!);
    }).ToArray();
    private static OfficeDeploymentRequest Request(string action, object? configuration, string? sourcePath, string[] removeProductIds, bool removeMsi, string[] languages, object settings)
    {
        var selectedAction = (OfficeDeploymentAction)Enum.Parse(typeof(OfficeDeploymentAction), action, true);
        return new OfficeDeploymentRequest(selectedAction, configuration == null ? null : Configuration(configuration), sourcePath, removeProductIds, removeMsi, languages,
            selectedAction == OfficeDeploymentAction.SetUpdateConfiguration ? Updates(settings) : null,
            selectedAction == OfficeDeploymentAction.SetApplicationPreference ? Preferences(settings) : null);
    }
    public static void ValidateRequest(string action, object? configuration, string? sourcePath, string[] removeProductIds, bool removeMsi, string[] languages, object settings)
        => _ = Request(action, configuration, sourcePath, removeProductIds, removeMsi, languages, settings);
    public static PSObject CreatePlan(string action, object? configuration, string? sourcePath, string[] removeProductIds, bool removeMsi, string[] languages,
        object settings, object inventory, object? media, string inventoryFingerprint)
    {
        var request = Request(action, configuration, sourcePath, removeProductIds, removeMsi, languages, settings);
        // The legacy boundary compares exact version text; the typed core normally uses Version.ToString().
        // Exchange only equivalent spellings for comparison, while retaining original observations in the report.
        string? ObservedVersion(object value)
        {
            var observed = Text(Get(value, "Version"));
            var raw = configuration == null ? null : Text(Get(configuration, "Version"));
            var normalized = request.Configuration?.Version?.ToString();
            if (raw == null || normalized == null || raw == normalized)
                return observed;
            return observed == raw ? normalized : observed == normalized ? raw : observed;
        }
        var observations = new OfficePlanningInventory(Text(Get(inventory, "MachineId"))!,
            Items(Get(inventory, "Products")).Select(value => new OfficeObservedConfiguration(Text(Get(value, "ProductId")) ?? "", Text(Get(value, "Architecture")),
                Text(Get(value, "Channel")), ObservedVersion(value), Text(Get(value, "PrimaryLanguage")), Strings(Get(value, "Languages")), Strings(Get(value, "ExcludeApp")))),
            Items(Get(inventory, "Msi")).Select(value => new OfficeObservedMsiProduct(Text(Get(value, "Name")), Text(Get(value, "Version")))),
            Strings(Get(inventory, "Unknowns")) ?? System.Array.Empty<string>(), Strings(Get(inventory, "VerificationLimitations")) ?? System.Array.Empty<string>());
        var valid = media != null && LanguagePrimitives.IsTrue(Get(media, "Valid"));
        var manifest = media == null ? null : Get(media, "Manifest");
        var mediaVersion = manifest == null ? null : Text(Get(manifest, "Version"));
        var result = new OfficeDeploymentPlanner().Create(request, observations, media == null ? null : new OfficePlanningMedia(valid,
            Text(Get(media, "ReasonCode")), string.IsNullOrEmpty(mediaVersion) ? null : System.Version.Parse(mediaVersion)));
        var transition = result.LanguageTransition;
        var languageTransition = transition == null ? null : SystemOutput.Object("Known", transition.Known, "Before", Array(transition.Before), "After", Array(transition.After),
            "Added", Array(transition.Added), "Removed", Array(transition.Removed), "PrimaryBefore", Array(transition.PrimaryBefore), "PrimaryAfter", transition.PrimaryAfter);
        return SystemOutput.Object("SchemaVersion", 1, "Action", action, "MachineId", Get(inventory, "MachineId"), "Configuration", configuration, "SourcePath", sourcePath,
            "RemoveProductId", Array(request.RemoveProductIds), "RemoveMsi", removeMsi, "Language", Array(request.Languages), "Settings", settings, "Before", inventory,
            "InventoryFingerprint", inventoryFingerprint, "State", result.State.ToString(), "Eligible", result.Eligible, "Blockers", Array(result.Blockers), "Warnings", Array(result.Warnings),
            "LanguageTransition", languageTransition, "Media", media, "MediaFingerprint", valid ? Get(media!, "Fingerprint") : null);
    }
    public static XmlDocument CreateXml(string action, object? configuration, string? mediaPath, string[] removeProductIds, bool removeMsi, string[] languages, object settings)
    {
        var target = configuration == null ? null : Configuration(configuration);
        var builder = new OdtConfigurationBuilder();
        XmlDocument document;
        switch (action.ToLowerInvariant())
        {
            case "download":
                document = builder.Download(target!, mediaPath!);
                break;
            case "install":
                document = builder.Install(target!, mediaPath!);
                break;
            case "migrate":
                document = builder.Migrate(target!, mediaPath!, removeMsi);
                break;
            case "update":
                document = builder.Update(target!, mediaPath!);
                break;
            case "addlanguage":
                document = builder.AddLanguage(target!, mediaPath!);
                break;
            case "setapplicationselection":
                document = builder.SetApplicationSelection(target!, mediaPath!);
                break;
            case "remove":
                document = builder.RemoveProducts(removeProductIds);
                break;
            case "removelanguage":
                document = builder.RemoveLanguage(target!.Product, languages);
                break;
            case "setupdateconfiguration":
                document = builder.SetUpdateConfiguration(Updates(settings));
                break;
            case "setapplicationpreference":
                document = builder.SetApplicationPreferences(Preferences(settings));
                break;
            default:
                throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Unsupported XML operation.");
        }
        // Preserve the validated PowerShell representation (case, attribute order and exact version text).
        if (configuration != null)
        {
            if (document.SelectSingleNode("/Configuration/Add") is XmlElement add)
            {
                add.SetAttribute("Channel", Text(Get(configuration, "Channel"))!);
                if (target!.Version != null)
                    add.SetAttribute("Version", Text(Get(configuration, "Version"))!);
                var applications = Strings(Get(configuration, "ExcludeApp"))!;
                var index = 0;
                foreach (XmlElement exclusion in document.SelectNodes("/Configuration/Add/Product/ExcludeApp")!)
                    exclusion.SetAttribute("ID", applications[index++]);
            }
            foreach (XmlElement product in document.SelectNodes("/Configuration/Add/Product | /Configuration/Remove/Product")!)
                if (string.Equals(product.GetAttribute("ID"), target!.Product.ToString(), StringComparison.OrdinalIgnoreCase))
                    product.SetAttribute("ID", Text(Get(configuration, "TargetProductId"))!);
        }
        if (document.SelectSingleNode("/Configuration/Updates") is XmlElement updates)
        {
            updates.RemoveAllAttributes();
            var names = settings is IDictionary dictionary ? dictionary.Keys.Cast<object>().Select(value => Text(value)!)
                : PSObject.AsPSObject(settings).Properties.Select(property => property.Name);
            foreach (var name in names)
                updates.SetAttribute(name, Text(Get(settings, name))!);
        }
        var preferences = Items(Get(settings, "Preferences")).ToArray();
        var nodes = document.SelectNodes("/Configuration/AppSettings/User")!;
        for (var index = 0; index < nodes.Count; index++)
            foreach (var name in new[] { "Key", "Name", "Value", "Type", "App", "Id" })
                ((XmlElement)nodes[index]!).SetAttribute(name, Text(Get(preferences[index], name))!);
        return document;
    }
}
