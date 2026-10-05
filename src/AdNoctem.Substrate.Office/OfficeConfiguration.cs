using System;
using System.Collections.Generic;
using System.Linq;

namespace AdNoctem.Substrate.Office;

public enum OfficeArchitecture { X86 = 32, X64 = 64 }
public enum OfficeChannel { Current, MonthlyEnterprise, SemiAnnual, PerpetualVL2019, PerpetualVL2021, PerpetualVL2024 }
public enum OfficeProduct { Standard2019Volume, ProPlus2019Volume, Standard2021Volume, ProPlus2021Volume, Standard2024Volume, ProPlus2024Volume, O365ProPlusRetail, O365BusinessRetail }
public enum OfficeApplication { Access, Excel, Groove, Lync, OneDrive, OneNote, Outlook, PowerPoint, Publisher, Teams, Word }
public enum OfficeLocaleSource { Default, Explicit, InstalledOffice, OperatingSystem, Recovery }
public enum OfficeFailureReason { InvalidConfiguration, InvalidAuthority, Unsupported, InvalidContract, LocaleDiscoveryFailed, MachineIdentityUnavailable, InvalidMachineIdentity, UnknownInventory, UnsafePath, UntrustedMedia, UntrustedTool, Conflict, ToolExtractionFailed, ReprepareMedia, InvalidMedia, UnsafeManifest, MediaMismatch, MediaIntegrityFailed, MissingMedia, MissingLanguageMedia, InvalidProductKey, ConfigurationCleanupFailed, DownloadFailed, InvalidRecoveryRecord, UnsupportedPilotRecovery, StaleMedia, DeploymentBusy, ElevationRequired, RebootRequired, PreflightFailed, StalePlan, ApplicationsRunning, InsufficientSpace, VerificationFailed, NativeFailure, OperationFailed, Cancelled }

/// <summary>An Office operation failure with a stable reason classification and optional underlying exception.</summary>
public sealed class OfficeException : InvalidOperationException
{
    public OfficeFailureReason Reason { get; }
    public OfficeException(OfficeFailureReason reason, string message, Exception? innerException = null) : base(message, innerException)
    { Reason = reason; Data["OfficeReason"] = reason.ToString(); }
}

/// <summary>An explicit target, with no authority to install or remove products.</summary>
public sealed class OfficeConfiguration
{
    public OfficeProduct Product { get; }
    public OfficeArchitecture Architecture { get; }
    public OfficeChannel Channel { get; }
    public IReadOnlyList<string> Languages { get; }
    public string PrimaryLanguage => Languages[0];
    public Version? Version { get; }
    public IReadOnlyList<OfficeApplication> ExcludedApplications { get; }
    public bool IsVolumeProduct => Product.ToString().EndsWith("Volume", StringComparison.Ordinal);
    /// <remarks>Omitted languages mean en-us. Empty languages are invalid. A null build means unresolved, never a verified installed build.</remarks>
    public OfficeConfiguration(OfficeProduct product, OfficeArchitecture architecture = OfficeArchitecture.X64, OfficeChannel? channel = null,
        IEnumerable<string>? languages = null, Version? version = null, IEnumerable<OfficeApplication>? excludedApplications = null)
    {
        if (!Enum.IsDefined(typeof(OfficeProduct), product))
            throw new ArgumentOutOfRangeException(nameof(product));
        if (!Enum.IsDefined(typeof(OfficeArchitecture), architecture))
            throw new ArgumentOutOfRangeException(nameof(architecture));
        if (channel.HasValue && !Enum.IsDefined(typeof(OfficeChannel), channel.Value))
            throw new ArgumentOutOfRangeException(nameof(channel));
        Product = product;
        Architecture = architecture;
        var expected = GetDefaultChannel(product);
        Channel = channel ?? expected;
        if (IsVolumeProduct ? Channel != expected : Channel.ToString().StartsWith("Perpetual", StringComparison.Ordinal))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Product and channel do not match.");
        Languages = OfficeLanguages.Normalize(languages ?? new[] { "en-us" });
        if (Languages.Count == 0)
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "At least one language is required.");
        if (version != null && (version.Major != 16 || version.Minor != 0 || version.Build < 0 || version.Revision < 0))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Use an exact 16.0 build with four version components.");
        Version = version;
        var exclusions = (excludedApplications ?? Array.Empty<OfficeApplication>()).ToArray();
        if (exclusions.Any(value => !Enum.IsDefined(typeof(OfficeApplication), value)))
            throw new ArgumentOutOfRangeException(nameof(excludedApplications));
        ExcludedApplications = Array.AsReadOnly(exclusions.Distinct().OrderBy(value => value.ToString(), StringComparer.OrdinalIgnoreCase).ToArray());
    }
    public static OfficeChannel GetDefaultChannel(OfficeProduct product)
    {
        if (!Enum.IsDefined(typeof(OfficeProduct), product))
            throw new ArgumentOutOfRangeException(nameof(product));
        switch (product)
        {
            case OfficeProduct.Standard2019Volume:
            case OfficeProduct.ProPlus2019Volume:
                return OfficeChannel.PerpetualVL2019;
            case OfficeProduct.Standard2021Volume:
            case OfficeProduct.ProPlus2021Volume:
                return OfficeChannel.PerpetualVL2021;
            case OfficeProduct.Standard2024Volume:
            case OfficeProduct.ProPlus2024Volume:
                return OfficeChannel.PerpetualVL2024;
            default:
                return OfficeChannel.Current;
        }
    }
}
/// <summary>An explicit language-selection policy; installed-language preservation must be resolved from inventory.</summary>
public static class OfficeLanguages
{
    private static readonly HashSet<string> Supported = new HashSet<string>(new[]
    {
        "en-us", "de-de", "fr-fr", "es-es", "it-it", "nl-nl", "pt-br", "pt-pt", "ja-jp", "ko-kr", "zh-cn", "zh-tw", "pl-pl", "cs-cz",
        "da-dk", "fi-fi", "sv-se", "nb-no", "hu-hu", "tr-tr", "el-gr", "ro-ro", "sk-sk", "sl-si", "hr-hr", "bg-bg", "et-ee", "lv-lv", "lt-lt"
    }, StringComparer.OrdinalIgnoreCase);
    /// <summary>Normalizes the supported full-UI catalog, preserving first occurrence and primary-language order. Does not infer proofing or LIP languages.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> languages)
    {
        if (languages == null)
            throw new ArgumentNullException(nameof(languages));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var language in languages)
        {
            if (string.IsNullOrWhiteSpace(language))
                throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Empty identifiers are not allowed.");
            var value = language.Trim().ToLowerInvariant();
            if (!Supported.Contains(value))
                throw new OfficeException(OfficeFailureReason.Unsupported, "Language '" + value + "' is outside the supported full-UI language catalog.");
            if (seen.Add(value))
                result.Add(value);
        }
        return result.AsReadOnly();
    }
}
