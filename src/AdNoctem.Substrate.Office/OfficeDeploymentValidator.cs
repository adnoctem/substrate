using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdNoctem.Substrate.Office;

/// <summary>A detached observation for offline assessment. Null means unverified, including collections.</summary>
public sealed class OfficeObservedConfiguration
{
    public string ProductId { get; }
    public string? Architecture { get; }
    public string? Channel { get; }
    public string? Version { get; }
    public string? PrimaryLanguage { get; }
    public IReadOnlyList<string>? Languages { get; }
    public IReadOnlyList<string>? ExcludedApplications { get; }
    public OfficeObservedConfiguration(string productId, string? architecture, string? channel, string? version, string? primaryLanguage,
        IEnumerable<string>? languages, IEnumerable<string>? excludedApplications)
    {
        ProductId = productId ?? throw new ArgumentNullException(nameof(productId));
        Architecture = architecture;
        Channel = channel;
        Version = version;
        PrimaryLanguage = primaryLanguage;
        Languages = languages == null ? null : Array.AsReadOnly(languages.ToArray());
        ExcludedApplications = excludedApplications == null ? null : Array.AsReadOnly(excludedApplications.ToArray());
    }
}
/// <summary>Differences and unknown observations relative to a requested configuration; unknowns prevent a compliant verdict.</summary>
public sealed class OfficeDeploymentAssessment
{
    public bool Compliant => Discrepancies.Count == 0 && Unknowns.Count == 0;
    public IReadOnlyList<string> Discrepancies { get; }
    public IReadOnlyList<string> Unknowns { get; }
    internal OfficeDeploymentAssessment(List<string> discrepancies, List<string> unknowns) { Discrepancies = discrepancies.AsReadOnly(); Unknowns = unknowns.AsReadOnly(); }
}
/// <summary>Compares observed state with an explicit target. Activation and previous successful execution are not installation evidence.</summary>
public sealed class OfficeDeploymentValidator
{
    public OfficeDeploymentAssessment Evaluate(OfficeConfiguration target, OfficeInventory inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        return Evaluate(target, inventory.Products.Select(product => new OfficeObservedConfiguration(product.ProductId,
            product.Architecture.HasValue ? ((int)product.Architecture.Value).ToString(CultureInfo.InvariantCulture) : null,
            product.Channel?.ToString(), product.Version?.ToString(), product.PrimaryLanguage, product.Languages, product.ExcludedApplications)), inventory.Msi.Count != 0, inventory.Unknowns);
    }
    /// <remarks>Caller-supplied observations are suitable for assessment only. Execution must rediscover state.</remarks>
    public OfficeDeploymentAssessment Evaluate(OfficeConfiguration target, IEnumerable<OfficeObservedConfiguration> products, bool hasMsiProducts, IEnumerable<string>? unknowns = null)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        var rows = (products ?? throw new ArgumentNullException(nameof(products))).ToArray();
        if (rows.Any(row => row == null))
            throw new ArgumentException("Observations cannot contain null.", nameof(products));
        var differences = new List<string>();
        var missing = new List<string>(unknowns ?? Array.Empty<string>());
        var selected = rows.Where(product => Same(product.ProductId, target.Product.ToString())).ToArray();
        if (selected.Length != 1)
            differences.Add("ProductId");
        else
        {
            var actual = selected[0];
            void Compare(string field, string? expected, string? observed)
            {
                if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(observed))
                    missing.Add(field);
                else if (!Same(expected, observed))
                    differences.Add(field);
            }
            void CompareList(string field, IEnumerable<string> expected, IReadOnlyList<string>? observed)
            {
                if (observed == null)
                    missing.Add(field);
                else if (!expected.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).SequenceEqual(observed.OrderBy(value => value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
                    differences.Add(field);
            }
            Compare("Architecture", ((int)target.Architecture).ToString(CultureInfo.InvariantCulture), actual.Architecture);
            Compare("Channel", target.Channel.ToString(), actual.Channel);
            Compare("Version", target.Version?.ToString(), actual.Version);
            Compare("PrimaryLanguage", target.PrimaryLanguage, actual.PrimaryLanguage);
            CompareList("Languages", target.Languages, actual.Languages);
            CompareList("ExcludeApp", target.ExcludedApplications.Select(value => value.ToString()), actual.ExcludedApplications);
        }
        if (hasMsiProducts || rows.Any(product => !Same(product.ProductId, target.Product.ToString())))
            differences.Add("OtherProducts");
        return new OfficeDeploymentAssessment(differences, missing);
    }
    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
