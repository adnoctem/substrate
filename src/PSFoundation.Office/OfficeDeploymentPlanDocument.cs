using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PSFoundation.Registry;

namespace PSFoundation.Office;

/// <summary>An immutable serialized deployment plan with explicit request scope and observation fingerprints. It never grants execution authority.</summary>
public sealed class OfficeDeploymentPlanDocument
{
    internal static readonly string[] RequiredFields = { "SchemaVersion", "Action", "MachineId", "Configuration", "SourcePath", "RemoveProductId", "RemoveMsi", "Language", "Settings", "Before", "InventoryFingerprint", "State", "Eligible", "Blockers", "Warnings", "LanguageTransition", "MediaFingerprint" };
    internal OfficeDocument Data { get; }
    public OfficeDeploymentRequest Request { get; }
    public string MachineId => Data.Text("MachineId")!;
    public string InventoryFingerprint => Data.Text("InventoryFingerprint")!;
    public string? MediaFingerprint => Data.Text("MediaFingerprint");
    public bool Eligible => Data.Boolean("Eligible");
    public OfficeDeploymentState State => ParseEnum<OfficeDeploymentState>(Data.Text("State"));
    public IReadOnlyList<string> Blockers => Array.AsReadOnly(Data.Strings("Blockers"));
    public IReadOnlyList<string> Warnings => Array.AsReadOnly(Data.Strings("Warnings"));
    public string ToJson() => Data.ToJson();
    public static OfficeDeploymentPlanDocument Parse(string json) => new OfficeDeploymentPlanDocument(OfficeDocument.Parse(json));
    internal OfficeDeploymentPlanDocument(OfficeDocument data)
    {
        data.Require(RequiredFields, "Media");
        if (data.Integer("SchemaVersion") != 1)
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Plan schema is unsupported.");
        if (string.IsNullOrWhiteSpace(data.Text("MachineId")) || string.IsNullOrWhiteSpace(data.Text("InventoryFingerprint")))
            throw OfficeDocument.Invalid();
        var action = ParseEnum<OfficeDeploymentAction>(data.Text("Action"));
        OfficeConfiguration? target = null;
        if (data["Configuration"] != null)
        {
            var normalized = NormalizeConfiguration(data.Object("Configuration"), out target);
            data["Configuration"] = normalized;
        }
        var settings = data.Object("Settings");
        OfficeUpdateConfiguration? updates = null;
        OfficeApplicationPreference[]? preferences = null;
        if (action == OfficeDeploymentAction.SetUpdateConfiguration)
        {
            settings.Require(Array.Empty<string>(), "Enabled", "UpdatePath", "TargetVersion", "Channel");
            updates = new OfficeUpdateConfiguration(settings.Has("Enabled") ? settings.Boolean("Enabled") : (bool?)null,
                settings.Text("UpdatePath"), settings.Has("TargetVersion") ? Version.Parse(settings.Text("TargetVersion")!) : null,
                settings.Has("Channel") ? ParseEnum<OfficeChannel>(settings.Text("Channel")) : (OfficeChannel?)null);
        }
        else if (action == OfficeDeploymentAction.SetApplicationPreference)
        {
            settings.Require(new[] { "Preferences" });
            preferences = settings.Array("Preferences").Select(value =>
            {
                var item = value as OfficeDocument ?? throw OfficeDocument.Invalid();
                item.Require(new[] { "Key", "Name", "Value", "Type", "App", "Id" });
                var app = item.Text("App")!;
                if (!new[] { "word16", "excel16", "ppt16", "outlook16", "access16", "onenote16" }.Contains(app, StringComparer.OrdinalIgnoreCase))
                    throw OfficeDocument.Invalid();
                var kind = item.Text("Type");
                if (!OfficeDocument.Equal(kind, "REG_DWORD") && !OfficeDocument.Equal(kind, "REG_SZ"))
                    throw OfficeDocument.Invalid();
                var preferenceValue = item["Value"] is string text ? text : item["Value"] is long integer ? integer.ToString(CultureInfo.InvariantCulture) : throw OfficeDocument.Invalid();
                return new OfficeApplicationPreference(RegistryPath.Parse("HKCU\\" + item.Text("Key")), item.Text("Name")!, preferenceValue,
                    OfficeDocument.Equal(kind, "REG_DWORD") ? OfficePreferenceValueType.DWord : OfficePreferenceValueType.String,
                    OfficeDocument.Equal(app, "ppt16") ? OfficePreferenceApplication.PowerPoint : ParseEnum<OfficePreferenceApplication>(app.Substring(0, app.Length - 2)), item.Text("Id")!);
            }).ToArray();
        }
        else
            settings.Require(Array.Empty<string>());
        Request = new OfficeDeploymentRequest(action, target, data.Text("SourcePath"), data.Strings("RemoveProductId"), data.Boolean("RemoveMsi"), data.Strings("Language"), updates, preferences);
        _ = data.Object("Before");
        _ = data.Boolean("Eligible");
        _ = ParseEnum<OfficeDeploymentState>(data.Text("State"));
        _ = data.Strings("Blockers");
        _ = data.Strings("Warnings");
        _ = data.Text("MediaFingerprint");
        Data = data;
    }

    public static OfficeDeploymentPlanDocument Create(OfficeDeploymentRequest request, OfficeInventory inventory, OfficeMediaAssessment? media = null)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        var before = OfficeInventorySerializer.Document(inventory);
        var settings = OfficeDocument.Create();
        if (request.UpdateConfiguration != null)
        {
            var updates = request.UpdateConfiguration;
            if (updates.Enabled.HasValue)
                settings["Enabled"] = updates.Enabled.Value;
            if (updates.UpdatePath != null)
                settings["UpdatePath"] = updates.UpdatePath;
            if (updates.TargetVersion != null)
                settings["TargetVersion"] = updates.TargetVersion.ToString();
            if (updates.Channel.HasValue)
                settings["Channel"] = updates.Channel.Value.ToString();
        }
        if (request.Preferences.Count != 0)
            settings["Preferences"] = request.Preferences.Select(value => OfficeDocument.Create("Key", value.Path.SubKey, "Name", value.Name, "Value", value.Value,
                "Type", value.Type == OfficePreferenceValueType.DWord ? "REG_DWORD" : "REG_SZ", "App", PreferenceApp(value.Application), "Id", value.Id)).ToArray();
        var data = OfficeDocument.Create("SchemaVersion", 1, "Action", request.Action.ToString(), "MachineId", inventory.MachineId,
            "Configuration", request.Configuration == null ? null : ConfigurationDocument(request.Configuration), "SourcePath", request.SourcePath,
            "RemoveProductId", request.RemoveProductIds.Cast<object?>().ToArray(), "RemoveMsi", request.RemoveMsi, "Language", request.Languages.Cast<object?>().ToArray(),
            "Settings", settings, "Before", before, "InventoryFingerprint", OfficeDocument.Fingerprint(before), "State", "Clean", "Eligible", false,
            "Blockers", Array.Empty<object>(), "Warnings", Array.Empty<object>(), "LanguageTransition", null, "Media", null, "MediaFingerprint", null);
        return new OfficeDeploymentPlanDocument(data).Refresh(inventory, media);
    }

    internal OfficeDeploymentPlanDocument Refresh(OfficeInventory inventory, OfficeMediaAssessment? media)
    {
        var data = Data.Copy();
        if (media?.Valid == true && Request.Configuration != null && Request.Configuration.Version == null)
            data.Object("Configuration")["Version"] = media.Manifest!.VersionText;
        var normalized = new OfficeDeploymentPlanDocument(data);
        var observations = PlanningInventory(OfficeInventorySerializer.Document(inventory));
        // Preserve exact version spelling at this wire boundary, including leading zeros.
        var rawVersion = data["Configuration"] is OfficeDocument config ? config.Text("Version") : null;
        var normalizedVersion = normalized.Request.Configuration?.Version?.ToString();
        if (!string.IsNullOrEmpty(rawVersion) && rawVersion != normalizedVersion)
            observations = new OfficePlanningInventory(observations.MachineId, observations.Products.Select(product => new OfficeObservedConfiguration(product.ProductId, product.Architecture,
                product.Channel, product.Version == rawVersion ? normalizedVersion : product.Version == normalizedVersion ? rawVersion : product.Version,
                product.PrimaryLanguage, product.Languages, product.ExcludedApplications)), observations.Msi, observations.Unknowns, observations.VerificationLimitations);
        var plan = new OfficeDeploymentPlanner().Create(normalized.Request, observations, media == null ? null : new OfficePlanningMedia(media.Valid, media.Reason?.ToString(), media.Manifest?.Configuration.Version));
        data["Before"] = OfficeInventorySerializer.Document(inventory);
        data["MachineId"] = inventory.MachineId;
        data["InventoryFingerprint"] = OfficeDocument.Fingerprint(data["Before"]);
        data["State"] = plan.State.ToString();
        data["Eligible"] = plan.Eligible;
        data["Blockers"] = plan.Blockers.Cast<object?>().ToArray();
        data["Warnings"] = plan.Warnings.Cast<object?>().ToArray();
        var transition = plan.LanguageTransition;
        data["LanguageTransition"] = transition == null ? null : OfficeDocument.Create("Known", transition.Known, "Before", transition.Before, "After", transition.After,
            "Added", transition.Added, "Removed", transition.Removed, "PrimaryBefore", transition.PrimaryBefore, "PrimaryAfter", transition.PrimaryAfter);
        data["MediaFingerprint"] = media?.Valid == true ? OfficeDocument.Fingerprint(OfficeDocument.Parse(media.ManifestJson!)) : null;
        // Retain the informational report when imported. It is never used as integrity evidence.
        return new OfficeDeploymentPlanDocument(data);
    }

    internal static OfficePlanningInventory PlanningInventory(OfficeDocument data) => new OfficePlanningInventory(data.Text("MachineId")!,
        data.Array("Products").Select(value =>
        {
            var product = value as OfficeDocument ?? throw OfficeDocument.Invalid();
            return new OfficeObservedConfiguration(product.Text("ProductId")!, product.Text("Architecture"), product.Text("Channel"), product.Text("Version"), product.Text("PrimaryLanguage"),
                product["Languages"] == null ? null : product.Strings("Languages"), product["ExcludeApp"] == null ? null : product.Strings("ExcludeApp"));
        }), data.Array("Msi").Select(value => { var product = value as OfficeDocument ?? throw OfficeDocument.Invalid(); return new OfficeObservedMsiProduct(product.Text("Name"), product.Text("Version")); }),
        data.Strings("Unknowns"), data.Has("VerificationLimitations") ? data.Strings("VerificationLimitations") : Array.Empty<string>());

    internal static OfficeDocument NormalizeConfiguration(OfficeDocument data, out OfficeConfiguration target)
    {
        data.Require(new[] { "SchemaVersion", "TargetProductId", "Architecture", "Channel", "Language", "PrimaryLanguage", "Version", "ExcludeApp", "RequestedLanguages", "LocaleSource", "LocaleEvidence" });
        if (data.Integer("SchemaVersion") != 1)
            throw OfficeDocument.Invalid();
        var architecture = data.Text("Architecture");
        if (architecture != "32" && architecture != "64")
            throw OfficeDocument.Invalid();
        var version = data.Text("Version");
        if (!string.IsNullOrEmpty(version) && !OfficeVersionResolver.IsMatch(version, @"^16\.0\.\d+\.\d+$"))
            throw OfficeDocument.Invalid();
        target = new OfficeConfiguration(ParseEnum<OfficeProduct>(data.Text("TargetProductId")), architecture == "64" ? OfficeArchitecture.X64 : OfficeArchitecture.X86,
            ParseEnum<OfficeChannel>(data.Text("Channel")), data.Strings("Language"), string.IsNullOrEmpty(version) ? null : Version.Parse(version), data.Strings("ExcludeApp").Select(ParseEnum<OfficeApplication>));
        if (!OfficeDocument.Equal(data.Text("PrimaryLanguage"), target.PrimaryLanguage))
            throw OfficeDocument.Invalid();
        _ = ParseEnum<OfficeLocaleSource>(data.Text("LocaleSource"));
        return OfficeDocument.Create("SchemaVersion", 1, "TargetProductId", data.Text("TargetProductId"), "Architecture", architecture, "Channel", data.Text("Channel"),
            "Language", target.Languages.Cast<object?>().ToArray(), "PrimaryLanguage", target.PrimaryLanguage, "Version", version ?? "",
            "ExcludeApp", data.Strings("ExcludeApp").Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).Cast<object?>().ToArray(),
            "RequestedLanguages", data.Strings("RequestedLanguages").Cast<object?>().ToArray(), "LocaleSource", data.Text("LocaleSource"), "LocaleEvidence", data.Strings("LocaleEvidence").Cast<object?>().ToArray());
    }
    private static OfficeDocument ConfigurationDocument(OfficeConfiguration value) => OfficeDocument.Create("SchemaVersion", 1, "TargetProductId", value.Product.ToString(),
        "Architecture", ((int)value.Architecture).ToString(CultureInfo.InvariantCulture), "Channel", value.Channel.ToString(), "Language", value.Languages.Cast<object?>().ToArray(),
        "PrimaryLanguage", value.PrimaryLanguage, "Version", value.Version?.ToString() ?? "", "ExcludeApp", value.ExcludedApplications.Select(item => (object?)item.ToString()).ToArray(),
        "RequestedLanguages", value.Languages.Cast<object?>().ToArray(), "LocaleSource", "Explicit", "LocaleEvidence", Array.Empty<object>());
    private static string PreferenceApp(OfficePreferenceApplication value) => value == OfficePreferenceApplication.PowerPoint ? "ppt16" : value.ToString().ToLowerInvariant() + "16";
    internal static T ParseEnum<T>(string? value) where T : struct
    {
        if (value == null || !Enum.TryParse<T>(value, true, out var parsed) || !Enum.IsDefined(typeof(T), parsed) || !OfficeDocument.Equal(parsed.ToString(), value))
            throw OfficeDocument.Invalid();
        return parsed;
    }
}
