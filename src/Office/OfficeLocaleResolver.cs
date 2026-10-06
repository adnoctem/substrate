using System;
using System.Collections.Generic;
using System.Linq;

namespace AdNoctem.Substrate.Office;

/// <summary>Resolved installed-language evidence and any issues that prevent reliable language preservation.</summary>
public sealed class OfficeLocaleSelection
{
    public IReadOnlyList<string> Languages { get; }
    public string PrimaryLanguage => Languages[0];
    public OfficeLocaleSource Source { get; }
    public IReadOnlyList<string> Evidence { get; }

    internal OfficeLocaleSelection(
        IEnumerable<string> languages,
        OfficeLocaleSource source,
        IEnumerable<string> evidence
    )
    {
        Languages = OfficeLanguages.Normalize(languages);
        Source = source;
        Evidence = Array.AsReadOnly(evidence.ToArray());
    }
}

/// <summary>Resolves language preservation from Office observations without silently selecting a replacement locale.</summary>
public sealed class OfficeLocaleResolver
{
    /// <summary>Resolves preservable installed Office languages from inventory, retaining ambiguity rather than inventing a language choice.</summary>
    public OfficeLocaleSelection FromInstalledOffice(OfficeInventory inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));

        var products = inventory.Products;
        var primaries = products
            .Select(product => product.PrimaryLanguage)
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (
            products.Count == 0
            || inventory.Unknowns.Count != 0
            || inventory.Msi.Count != 0
            || primaries.Length != 1
            || products.Any(product =>
                product.Languages == null
                || product.Languages.Count == 0
                || string.IsNullOrEmpty(product.PrimaryLanguage)
                || !product.Languages.Contains(
                    product.PrimaryLanguage!,
                    StringComparer.OrdinalIgnoreCase
                )
            )
        )
            throw new OfficeException(
                OfficeFailureReason.LocaleDiscoveryFailed,
                "Installed Office language preservation requires fully observed Click-to-Run products with one agreed primary language and no MSI or unknown inventory. Supply Language explicitly."
            );

        var primary = primaries[0]!;
        var others = products
            .SelectMany(product => product.Languages!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(language =>
                !string.Equals(language, primary, StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase);

        return new OfficeLocaleSelection(
            new[] { primary }.Concat(others),
            OfficeLocaleSource.InstalledOffice,
            products.SelectMany(product => product.Evidence)
        );
    }

    public OfficeLocaleSelection FromOperatingSystem() =>
        new OfficeLocaleSelection(
            new[] { new OfficeRegistryReader().ReadOperatingSystemLanguage() },
            OfficeLocaleSource.OperatingSystem,
            new[]
            {
                "HKLM SYSTEM CurrentControlSet Control Nls Language:InstallLanguage (machine installation UI language)",
            }
        );
}
