using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Office;

public enum OfficeVersionSource
{
    ClickToRunInventory,
    ActiveProductResources,
}

/// <summary>Resolved version and source authority, retaining ambiguity when evidence cannot identify one version.</summary>
public sealed class OfficeVersionEvidence
{
    public Version? Version { get; }
    public OfficeVersionSource? Source { get; }
    public string Evidence { get; }
    public string? Issue { get; }

    internal OfficeVersionEvidence(
        Version? version,
        OfficeVersionSource? source,
        string evidence,
        string? issue = null
    )
    {
        Version = version;
        Source = source;
        Evidence = evidence;
        Issue = issue;
    }
}

/// <summary>Resolves installed builds in authority order. Absence allows fallback; malformed or contradictory stronger evidence stops it.</summary>
public sealed class OfficeVersionResolver
{
    internal static bool IsMatch(string? value, string pattern) =>
        value != null
        && Regex.IsMatch(
            value,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)
        );

    internal static string[] SplitIds(string? value, params char[] separators) =>
        (value ?? "")
            .Split(separators)
            .Select(item => item.Trim())
            .Where(item => item.Length != 0)
            .ToArray();

    public OfficeVersionEvidence Resolve(
        IEnumerable<OfficeRegistryRecord> records,
        RegistryView view,
        string productId,
        IEnumerable<string> configuredProductIds
    )
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));

        if (productId == null)
            throw new ArgumentNullException(nameof(productId));

        if (configuredProductIds == null)
            throw new ArgumentNullException(nameof(configuredProductIds));

        var rows = records.Where(record => record.View == view).ToArray();
        OfficeRegistryRecord[] At(string path) =>
            rows.Where(record =>
                    string.Equals(record.Path.SubKey, path, StringComparison.OrdinalIgnoreCase)
                )
                .ToArray();
        OfficeVersionEvidence Unknown(string message, string? issue = null) =>
            new OfficeVersionEvidence(null, null, message, issue);
        var installed = At(OfficeRegistryReader.InstalledPath);

        if (installed.Length != 0)
        {
            if (installed.Length != 1)
                return Unknown(
                    "Installed version unknown: duplicate " + view + " inventory records"
                );

            var ids = SplitIds(installed[0].GetString("OfficeProductReleaseIds"), ',', ';');

            if (
                !ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(
                        configuredProductIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase
                    )
            )
                return Unknown(
                    "Installed version unknown: "
                        + view
                        + " inventory product identity differs from configuration",
                    "ConflictingInstalledProductIdentity"
                );

            if (installed[0].Values.ContainsKey("OfficePackageVersion"))
            {
                var text = installed[0].GetString("OfficePackageVersion");

                if (
                    !IsMatch(text, @"^\d+\.\d+\.\d+\.\d+$")
                    || !Version.TryParse(text, out var version)
                )
                    return Unknown(
                        "Installed version unknown: malformed "
                            + view
                            + " inventory OfficePackageVersion"
                    );

                return new OfficeVersionEvidence(
                    version,
                    OfficeVersionSource.ClickToRunInventory,
                    "Installed version from "
                        + view
                        + ": "
                        + OfficeRegistryReader.InstalledPath
                        + ":OfficePackageVersion"
                );
            }
        }

        var unavailable = Unknown(
            "Installed version unknown: no complete active product resource registration"
        );
        var active = At(OfficeRegistryReader.ResourceRoot);

        if (
            active.Length != 1
            || !IsMatch(productId, "^[a-z0-9]+$")
            || !IsMatch(
                active[0].GetString("ActiveConfiguration"),
                "^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$"
            )
        )
            return unavailable;

        var productPath =
            OfficeRegistryReader.ResourceRoot
            + "\\"
            + active[0].GetString("ActiveConfiguration")
            + "\\"
            + productId
            + ".16";
        var products = At(productPath);

        if (products.Length != 1)
            return unavailable;

        var cultures = products[0].SubKeys;

        if (
            cultures.Count < 2
            || !cultures.Contains("x-none", StringComparer.OrdinalIgnoreCase)
            || cultures.Any(culture =>
                !culture.Equals("x-none", StringComparison.OrdinalIgnoreCase)
                && !IsMatch(culture, "^[a-z]{2,3}-[a-z]{2,4}$")
            )
        )
            return unavailable;

        var versions = new HashSet<Version>();

        foreach (var culture in cultures)
        {
            var path = productPath + "\\" + culture;
            var leaves = At(path);

            if (
                leaves.Length != 1
                || !IsMatch(leaves[0].GetString("Version"), @"^\d+\.\d+\.\d+\.\d+$")
                || !Version.TryParse(leaves[0].GetString("Version"), out var version)
            )
                return Unknown(
                    "Installed version unknown: missing, duplicate or malformed Version at "
                        + view
                        + ": "
                        + path
                );

            versions.Add(version);
        }

        if (versions.Count != 1)
            return Unknown(
                "Installed version unknown: conflicting active resource versions at "
                    + view
                    + ": "
                    + productPath
            );

        return new OfficeVersionEvidence(
            versions.Single(),
            OfficeVersionSource.ActiveProductResources,
            "Installed version derived from agreeing Version values at "
                + view
                + ": "
                + productPath
                + " resources ["
                + string.Join(",", cultures)
                + "]"
        );
    }
}
