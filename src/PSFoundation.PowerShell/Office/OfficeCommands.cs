using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using PSFoundation.Office;
using PSFoundation.PowerShell.Windows;

namespace PSFoundation.PowerShell.Office;

[Cmdlet(VerbsCommon.New, "OfficeDeploymentConfiguration"), OutputType(typeof(PSObject))]
public sealed class NewOfficeDeploymentConfigurationCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0), ValidateSet("Standard2019Volume", "ProPlus2019Volume", "Standard2021Volume", "ProPlus2021Volume", "Standard2024Volume", "ProPlus2024Volume", "O365ProPlusRetail", "O365BusinessRetail")]
    public string TargetProductId { get; set; } = "";
    [Parameter(Position = 1), ValidateSet("32", "64")] public string Architecture { get; set; } = "64";
    [Parameter(Position = 2), ValidateSet("PerpetualVL2019", "PerpetualVL2021", "PerpetualVL2024", "Current", "MonthlyEnterprise", "SemiAnnual")] public string? Channel { get; set; }
    [Parameter(Position = 3)] public string[]? Language { get; set; }
    [Parameter(Position = 4), ValidatePattern(@"^16\.0\.\d+\.\d+$")] public string? Version { get; set; }
    [Parameter(Position = 5), ValidateSet("Access", "Excel", "Groove", "Lync", "OneDrive", "OneNote", "Outlook", "PowerPoint", "Publisher", "Teams", "Word")]
    public string[] ExcludeApp { get; set; } = Array.Empty<string>();
    [Parameter] public SwitchParameter AutoSourceLocales { get; set; }
    [Parameter(Position = 6), ValidateSet("InstalledOffice", "OperatingSystem")] public string LocaleSource { get; set; } = "InstalledOffice";
    protected override void ProcessRecord()
    {
        var explicitLanguage = MyInvocation.BoundParameters.ContainsKey(nameof(Language));
        if (explicitLanguage && AutoSourceLocales || MyInvocation.BoundParameters.ContainsKey(nameof(LocaleSource)) && !AutoSourceLocales)
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Language and automatic discovery are mutually exclusive; LocaleSource requires AutoSourceLocales.");
        var requested = explicitLanguage ? Language ?? Array.Empty<string>() : Array.Empty<string>();
        var languages = explicitLanguage ? requested : new[] { "en-us" };
        var source = explicitLanguage ? "Explicit" : "Default";
        var evidence = Array.Empty<string>();
        if (AutoSourceLocales)
        {
            var resolver = new OfficeLocaleResolver();
            var selection = string.Equals(LocaleSource, "OperatingSystem", StringComparison.OrdinalIgnoreCase)
                ? resolver.FromOperatingSystem() : resolver.FromInstalledOffice(new OfficeInventoryManager().Read(Cancellation));
            languages = selection.Languages.ToArray();
            evidence = selection.Evidence.ToArray();
            source = LocaleSource;
        }
        var config = OfficeCompatibility.Target(TargetProductId, Architecture, Channel, languages, Version, ExcludeApp);
        WriteObject(SystemOutput.Object("SchemaVersion", 1, "TargetProductId", TargetProductId, "Architecture", Architecture, "Channel", Channel ?? config.Channel.ToString(),
            "Language", config.Languages.Cast<object>().ToArray(), "PrimaryLanguage", config.PrimaryLanguage, "Version", Version ?? "",
            "ExcludeApp", ExcludeApp.Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).Cast<object>().ToArray(),
            "RequestedLanguages", requested.Cast<object>().ToArray(), "LocaleSource", source, "LocaleEvidence", evidence.Cast<object>().ToArray()));
    }
}
[Cmdlet(VerbsCommon.Get, "OfficeInventory"), OutputType(typeof(PSObject))]
public sealed class GetOfficeInventoryCommand : SystemCommand
{
    protected override void ProcessRecord() => WriteObject(OfficeCompatibility.Inventory(new OfficeInventoryManager().Read(Cancellation)));
}
[Cmdlet(VerbsCommon.Get, "OfficeActivationStatus"), OutputType(typeof(PSObject))]
public sealed class GetOfficeActivationStatusCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string TargetProductId { get; set; } = "";
    protected override void ProcessRecord()
    {
        var state = Enum.TryParse<OfficeProduct>(TargetProductId, true, out var product) && Enum.IsDefined(typeof(OfficeProduct), product) && string.Equals(product.ToString(), TargetProductId, StringComparison.OrdinalIgnoreCase)
            ? new OfficeActivationManager().GetStatus(product, InventoryTimeout, Cancellation) : OfficeActivationState.Unknown;
        WriteObject(SystemOutput.Object("TargetProductId", TargetProductId, "Status", state.ToString()));
    }
}

/// <summary>PowerShell object shapes retained at the module boundary; the Office library does not depend on PowerShell.</summary>
public static partial class OfficeCompatibility
{
    internal static OfficeConfiguration Target(string product, string architecture, string? channel, IEnumerable<string> languages, string? version, IEnumerable<string> excluded)
        => new OfficeConfiguration((OfficeProduct)Enum.Parse(typeof(OfficeProduct), product, true), architecture == "64" ? OfficeArchitecture.X64 : OfficeArchitecture.X86,
            string.IsNullOrEmpty(channel) ? (OfficeChannel?)null : (OfficeChannel)Enum.Parse(typeof(OfficeChannel), channel!, true), languages,
            string.IsNullOrEmpty(version) ? null : System.Version.Parse(version), excluded.Select(value => (OfficeApplication)Enum.Parse(typeof(OfficeApplication), value, true)));
    public static PSObject Inventory(OfficeInventory inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        return SystemOutput.Object("SchemaVersion", 1, "MachineId", inventory.MachineId,
            "Products", inventory.Products.Select(product => SystemOutput.Object("ProductId", product.ProductId,
                "Architecture", product.Architecture.HasValue ? ((int)product.Architecture.Value).ToString(CultureInfo.InvariantCulture) : null,
                "Version", product.Version?.ToString(), "VersionSource", product.InstalledVersion.Source?.ToString(), "Channel", product.Channel?.ToString(),
                "Languages", Array(product.Languages), "PrimaryLanguage", product.PrimaryLanguage, "RegisteredLanguages", Array(product.RegisteredLanguages),
                "ExcludeApp", Array(product.ExcludedApplications), "Evidence", Array(product.Evidence))).Cast<object>().ToArray(),
            "Msi", inventory.Msi.Select(item => SystemOutput.Object("ProductCode", item.ProductCode, "Name", item.Name, "Version", item.Version, "RegistryView", item.RegistryView.ToString(), "LanguageId", item.LanguageId, "ResourceKind", item.ResourceKind.ToString())).Cast<object>().ToArray(),
            "RelatedComponents", inventory.RelatedComponents.Select(Related).Cast<object>().ToArray(), "Unknowns", Array(inventory.Unknowns),
            "RegisteredResources", inventory.RegistryRecords.Where(record => record.Path.SubKey.IndexOf(@"ClickToRun\ProductReleaseIDs", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(record => SystemOutput.Object("View", record.View.ToString(), "Path", record.Path.SubKey, "SubKeys", Array(record.SubKeys), "ActiveConfiguration", record.GetValue("ActiveConfiguration"), "Version", record.GetValue("Version"))).Cast<object>().ToArray(),
            "AppPathEvidence", inventory.AppPaths.Select(path => SystemOutput.Object("RegistryView", path.RegistryView.ToString(), "RegistryPath", path.RegistryPath,
                "RawTarget", path.RawTarget, "ResolvedTarget", path.ResolvedTarget, "State", path.State.ToString(), "Reason", path.Reason.ToString())).Cast<object>().ToArray(),
            "LanguageEvidence", inventory.RegistryRecords.Where(record => record.Path.SubKey.EndsWith(@"\Common\LanguageResources", StringComparison.OrdinalIgnoreCase))
                .Select(record => SystemOutput.Object("View", record.View.ToString(), "Path", record.Path.SubKey, "SKULanguage", record.GetValue("SKULanguage"), "InstallLanguage", record.GetValue("InstallLanguage"))).Cast<object>().ToArray(),
            "VerificationLimitations", Array(inventory.VerificationLimitations));
    }
    private static PSObject Related(OfficeRelatedComponent item)
    {
        var result = SystemOutput.Object("ProductCode", item.ProductCode, "Name", item.Name, "Version", item.Version, "RegistryView", item.RegistryView.ToString(), "Role", item.Role.ToString());
        if (item.Role == OfficeRelatedRole.PatchRegistration)
        {
            result.Properties.Add(new PSNoteProperty("ParentProductCode", item.ParentProductCode));
            result.Properties.Add(new PSNoteProperty("SystemComponent", item.SystemComponent));
        }
        return result;
    }
    private static object[]? Array(IEnumerable<string>? values) => values?.Cast<object>().ToArray();
    private static object? Get(object value, string name) => value is IDictionary dictionary ? dictionary[name] : PSObject.AsPSObject(value).Properties[name]?.Value;
    private static string? Text(object? value) => value == null ? null : LanguagePrimitives.ConvertTo<string>(value);
    private static IEnumerable<object> Items(object? value)
    {
        if (value == null)
            yield break;
        if (value is PSObject wrapped)
            value = wrapped.BaseObject;
        if (value is IEnumerable sequence && !(value is string))
        { foreach (var item in sequence) if (item != null) yield return item; }
        else
            yield return value;
    }
    private static string[]? Strings(object? value) => value == null ? null : Items(value).Select(item => Text(item) ?? "").ToArray();
    /// <summary>Assesses a normalized legacy target and serialized observation without executing a script or changing the machine.</summary>
    public static PSObject Assess(object configuration, object inventory)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        var target = Target(Text(Get(configuration, "TargetProductId"))!, Text(Get(configuration, "Architecture"))!, Text(Get(configuration, "Channel")),
            Strings(Get(configuration, "Language"))!, Text(Get(configuration, "Version")), Strings(Get(configuration, "ExcludeApp"))!);
        var products = Items(Get(inventory, "Products")).Select(value => new OfficeObservedConfiguration(Text(Get(value, "ProductId")) ?? "", Text(Get(value, "Architecture")), Text(Get(value, "Channel")), Text(Get(value, "Version")),
            Text(Get(value, "PrimaryLanguage")), Strings(Get(value, "Languages")), Strings(Get(value, "ExcludeApp")))).ToArray();
        var assessment = new OfficeDeploymentValidator().Evaluate(target, products, Items(Get(inventory, "Msi")).Any(), Strings(Get(inventory, "Unknowns")));
        var differences = assessment.Discrepancies.ToList();
        // Preserve exact legacy version text at the boundary, including leading zeroes.
        var selected = products.Where(product => string.Equals(product.ProductId, target.Product.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray();
        var version = Text(Get(configuration, "Version"));
        if (selected.Length == 1 && !string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(selected[0].Version))
        {
            differences.Remove("Version");
            if (!string.Equals(version, selected[0].Version, StringComparison.OrdinalIgnoreCase))
            {
                var position = differences.FindIndex(value => value == "PrimaryLanguage" || value == "Languages" || value == "ExcludeApp" || value == "OtherProducts");
                differences.Insert(position < 0 ? differences.Count : position, "Version");
            }
        }
        return SystemOutput.Object("SchemaVersion", 1, "Compliant", differences.Count == 0 && assessment.Unknowns.Count == 0, "Discrepancies", Array(differences), "Unknowns", Array(assessment.Unknowns), "Inventory", inventory);
    }
}
