using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdNoctem.Substrate.Office;

public enum OfficeDeploymentAction { Install, Remove, Migrate, Update, SetUpdateConfiguration, AddLanguage, RemoveLanguage, SetApplicationSelection, SetApplicationPreference }
public enum OfficeDeploymentState { Clean, Unknown, Conflict, Compliant, Incomplete }

/// <summary>The requested scope, not authorization to execute. Construction rejects authority unrelated to the selected action.</summary>
public sealed class OfficeDeploymentRequest
{
    public OfficeDeploymentAction Action { get; }
    public OfficeConfiguration? Configuration { get; }
    public string? SourcePath { get; }
    public IReadOnlyList<string> RemoveProductIds { get; }
    public bool RemoveMsi { get; }
    public IReadOnlyList<string> Languages { get; }
    public OfficeUpdateConfiguration? UpdateConfiguration { get; }
    public IReadOnlyList<OfficeApplicationPreference> Preferences { get; }
    public OfficeDeploymentRequest(OfficeDeploymentAction action, OfficeConfiguration? configuration = null, string? sourcePath = null,
        IEnumerable<string>? removeProductIds = null, bool removeMsi = false, IEnumerable<string>? languages = null,
        OfficeUpdateConfiguration? updateConfiguration = null, IEnumerable<OfficeApplicationPreference>? preferences = null)
    {
        if (!Enum.IsDefined(typeof(OfficeDeploymentAction), action))
            throw new ArgumentOutOfRangeException(nameof(action));
        var ids = OdtConfigurationBuilder.NormalizeProductIds(removeProductIds ?? Array.Empty<string>());
        var selected = OfficeLanguages.Normalize(languages ?? Array.Empty<string>());
        var settings = (preferences ?? Array.Empty<OfficeApplicationPreference>()).ToArray();
        if (settings.Any(value => value == null))
            throw new ArgumentException("Preferences cannot contain null.", nameof(preferences));
        if (action != OfficeDeploymentAction.Remove && action != OfficeDeploymentAction.Migrate && (ids.Count != 0 || removeMsi))
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "This action cannot remove products.");
        if (action != OfficeDeploymentAction.AddLanguage && action != OfficeDeploymentAction.RemoveLanguage && selected.Count != 0)
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Language selection belongs only to language operations.");
        if (action != OfficeDeploymentAction.Remove && configuration == null)
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "This operation requires a complete target configuration.");
        if (action == OfficeDeploymentAction.Remove && (configuration != null || !string.IsNullOrEmpty(sourcePath) || ids.Count == 0 && !removeMsi))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Removal requires an explicit selection and accepts no installation configuration or media.");
        if ((action == OfficeDeploymentAction.AddLanguage || action == OfficeDeploymentAction.RemoveLanguage) && selected.Count == 0)
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Select at least one language resource.");
        if ((action == OfficeDeploymentAction.SetUpdateConfiguration) != (updateConfiguration != null) || (action == OfficeDeploymentAction.SetApplicationPreference) != (settings.Length != 0))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Settings must belong to the selected operation.");
        if (updateConfiguration?.Channel != null && configuration != null)
            _ = new OfficeConfiguration(configuration.Product, configuration.Architecture, updateConfiguration.Channel, configuration.Languages);
        Action = action;
        Configuration = configuration;
        SourcePath = sourcePath;
        RemoveProductIds = ids;
        RemoveMsi = removeMsi;
        Languages = selected;
        UpdateConfiguration = updateConfiguration;
        Preferences = Array.AsReadOnly(settings);
    }
}

public sealed class OfficeObservedMsiProduct
{
    public string? Name { get; }
    public string? Version { get; }
    public OfficeObservedMsiProduct(string? name, string? version) { Name = name; Version = version; }
}

/// <summary>Detached observations used for planning. Never use a supplied observation in place of execution-time discovery.</summary>
public sealed class OfficePlanningInventory
{
    public string MachineId { get; }
    public IReadOnlyList<OfficeObservedConfiguration> Products { get; }
    public IReadOnlyList<OfficeObservedMsiProduct> Msi { get; }
    public IReadOnlyList<string> Unknowns { get; }
    public IReadOnlyList<string> VerificationLimitations { get; }
    public OfficePlanningInventory(string machineId, IEnumerable<OfficeObservedConfiguration> products, IEnumerable<OfficeObservedMsiProduct> msi,
        IEnumerable<string> unknowns, IEnumerable<string> verificationLimitations)
    {
        MachineId = machineId ?? throw new ArgumentNullException(nameof(machineId));
        Products = Copy(products, nameof(products));
        Msi = Copy(msi, nameof(msi));
        Unknowns = Copy(unknowns, nameof(unknowns));
        VerificationLimitations = Copy(verificationLimitations, nameof(verificationLimitations));
    }
    public static OfficePlanningInventory FromInventory(OfficeInventory inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        return new OfficePlanningInventory(inventory.MachineId, inventory.Products.Select(product => new OfficeObservedConfiguration(product.ProductId,
            product.Architecture.HasValue ? ((int)product.Architecture.Value).ToString(CultureInfo.InvariantCulture) : null,
            product.Channel?.ToString(), product.Version?.ToString(), product.PrimaryLanguage, product.Languages, product.ExcludedApplications)),
            inventory.Msi.Select(product => new OfficeObservedMsiProduct(product.Name, product.Version)), inventory.Unknowns, inventory.VerificationLimitations);
    }
    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameter) where T : class
    {
        var items = (values ?? throw new ArgumentNullException(parameter)).ToArray();
        if (items.Any(item => item == null))
            throw new ArgumentException("Observations cannot contain null.", parameter);
        return Array.AsReadOnly(items);
    }
}

/// <summary>A supplied media validation outcome for offline planning. This is not a reusable proof of file integrity.</summary>
public sealed class OfficePlanningMedia
{
    public bool Valid { get; }
    public string? ReasonCode { get; }
    public Version? Version { get; }
    public OfficePlanningMedia(bool valid, string? reasonCode = null, Version? version = null)
    {
        if (!valid && string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("Invalid media requires a reason.", nameof(reasonCode));
        Valid = valid;
        ReasonCode = reasonCode;
        Version = version;
    }
}
public sealed class OfficeLanguageTransition
{
    public bool Known { get; }
    public IReadOnlyList<string> Before { get; }
    public IReadOnlyList<string> After { get; }
    public IReadOnlyList<string> Added { get; }
    public IReadOnlyList<string> Removed { get; }
    public IReadOnlyList<string> PrimaryBefore { get; }
    public string PrimaryAfter { get; }
    internal OfficeLanguageTransition(bool known, string[] before, OfficeConfiguration target, string[] primaryBefore)
    {
        Known = known;
        Before = Array.AsReadOnly(before);
        After = target.Languages;
        PrimaryBefore = Array.AsReadOnly(primaryBefore);
        PrimaryAfter = target.PrimaryLanguage;
        Added = Array.AsReadOnly(target.Languages.Except(before, StringComparer.OrdinalIgnoreCase).ToArray());
        Removed = Array.AsReadOnly(before.Except(target.Languages, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
/// <summary>A pure planning decision, including prerequisites, blockers, and language transitions.</summary>
public sealed class OfficeDeploymentPlan
{
    public OfficeDeploymentRequest Request { get; }
    public OfficePlanningInventory Before { get; }
    public OfficeConfiguration? Configuration { get; }
    public OfficeDeploymentState State { get; }
    public bool Eligible => Blockers.Count == 0;
    public IReadOnlyList<string> Blockers { get; }
    public IReadOnlyList<string> Warnings { get; }
    public OfficeLanguageTransition? LanguageTransition { get; }
    public OfficePlanningMedia? Media { get; }
    internal OfficeDeploymentPlan(OfficeDeploymentRequest request, OfficePlanningInventory before, OfficeConfiguration? configuration, OfficeDeploymentState state,
        IEnumerable<string> blockers, IEnumerable<string> warnings, OfficeLanguageTransition? transition, OfficePlanningMedia? media)
    {
        Request = request;
        Before = before;
        Configuration = configuration;
        State = state;
        Blockers = Array.AsReadOnly(blockers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        LanguageTransition = transition;
        Media = media;
    }
}
