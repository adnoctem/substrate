using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Office;

/// <summary>Observed product state. Null fields mean unverified; an empty observed collection is distinct from null.</summary>
public sealed class OfficeProductState
{
    public string ProductId { get; }
    public OfficeArchitecture? Architecture { get; }
    public OfficeVersionEvidence InstalledVersion { get; }
    public Version? Version => InstalledVersion.Version;
    public OfficeChannel? Channel { get; }
    public IReadOnlyList<string>? Languages { get; }
    public string? PrimaryLanguage { get; }
    public IReadOnlyList<string> RegisteredLanguages { get; }
    public IReadOnlyList<string>? ExcludedApplications { get; }
    public IReadOnlyList<string> Evidence { get; }

    internal OfficeProductState(
        string productId,
        OfficeArchitecture? architecture,
        OfficeVersionEvidence installedVersion,
        OfficeChannel? channel,
        IEnumerable<string>? languages,
        string? primaryLanguage,
        IEnumerable<string> registeredLanguages,
        IEnumerable<string>? excludedApplications,
        IEnumerable<string> evidence
    )
    {
        ProductId = productId;
        Architecture = architecture;
        InstalledVersion = installedVersion;
        Channel = channel;
        Languages = languages == null ? null : Array.AsReadOnly(languages.ToArray());
        PrimaryLanguage = primaryLanguage;
        RegisteredLanguages = Array.AsReadOnly(registeredLanguages.ToArray());
        ExcludedApplications =
            excludedApplications == null ? null : Array.AsReadOnly(excludedApplications.ToArray());
        Evidence = Array.AsReadOnly(evidence.ToArray());
    }
}

public enum OfficeMsiResourceKind
{
    ProductOrComponent,
    Proofing,
    LanguageInterfacePack,
    LanguageResource,
}

public enum OfficeRelatedRole
{
    ClickToRunInfrastructure,
    AddIn,
    PatchRegistration,
}

/// <summary>An observed MSI Office registration. Reading it does not trigger Windows Installer consistency checks.</summary>
public sealed class OfficeMsiRegistration
{
    public string ProductCode { get; }
    public string? Name { get; }
    public string? Version { get; }
    public RegistryView RegistryView { get; }
    public string? LanguageId { get; }
    public OfficeMsiResourceKind ResourceKind { get; }

    internal OfficeMsiRegistration(
        string code,
        OfficeRegistryRecord record,
        string? language,
        OfficeMsiResourceKind kind
    )
    {
        ProductCode = code;
        Name = record.GetString("DisplayName");
        Version = record.GetString("DisplayVersion");
        RegistryView = record.View;
        LanguageId = language;
        ResourceKind = kind;
    }
}

/// <summary>A related Office component classified separately from the main suite.</summary>
public sealed class OfficeRelatedComponent
{
    public string ProductCode { get; }
    public string? Name { get; }
    public string? Version { get; }
    public RegistryView RegistryView { get; }
    public OfficeRelatedRole Role { get; }
    public string? ParentProductCode { get; }
    public bool? SystemComponent { get; }

    internal OfficeRelatedComponent(
        string code,
        OfficeRegistryRecord record,
        OfficeRelatedRole role,
        string? parent = null,
        bool? system = null
    )
    {
        ProductCode = code;
        Name = record.GetString("DisplayName");
        Version = record.GetString("DisplayVersion");
        RegistryView = record.View;
        Role = role;
        ParentProductCode = parent;
        SystemComponent = system;
    }
}

/// <summary>Office installation observations across registration sources, preserving ambiguities and inventory diagnostics.</summary>
public sealed class OfficeInventory
{
    public string MachineId { get; }
    public IReadOnlyList<OfficeProductState> Products { get; }
    public IReadOnlyList<OfficeMsiRegistration> Msi { get; }
    public IReadOnlyList<OfficeRelatedComponent> RelatedComponents { get; }
    public IReadOnlyList<string> Unknowns { get; }
    public IReadOnlyList<string> VerificationLimitations { get; }
    public IReadOnlyList<OfficeRegistryRecord> RegistryRecords { get; }
    public IReadOnlyList<OfficeRegistryReadError> ReadErrors { get; }
    public IReadOnlyList<OfficeAppPathEvidence> AppPaths { get; }

    internal OfficeInventory(
        string machineId,
        IEnumerable<OfficeProductState> products,
        IEnumerable<OfficeMsiRegistration> msi,
        IEnumerable<OfficeRelatedComponent> related,
        IEnumerable<string> unknowns,
        IEnumerable<string> limitations,
        IEnumerable<OfficeRegistryRecord> records,
        IEnumerable<OfficeAppPathEvidence> appPaths,
        IEnumerable<OfficeRegistryReadError> errors
    )
    {
        MachineId = machineId;
        Products = Array.AsReadOnly(products.ToArray());
        Msi = Array.AsReadOnly(msi.ToArray());
        RelatedComponents = Array.AsReadOnly(related.ToArray());
        Unknowns = Array.AsReadOnly(unknowns.ToArray());
        VerificationLimitations = Array.AsReadOnly(limitations.ToArray());
        RegistryRecords = Array.AsReadOnly(records.ToArray());
        ReadErrors = Array.AsReadOnly(errors.ToArray());
        AppPaths = Array.AsReadOnly(appPaths.ToArray());
    }
}
