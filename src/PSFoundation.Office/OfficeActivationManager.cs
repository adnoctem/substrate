using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Windows;

namespace PSFoundation.Office;

public enum OfficeActivationState { Unknown, UserActivationRequired, NotVerified, Licensed }
/// <summary>Read-only SKU-specific activation assessment. Product keys are neither returned nor logged.</summary>
public sealed class OfficeActivationManager
{
    public OfficeActivationState GetStatus(OfficeProduct product, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(OfficeProduct), product))
            throw new ArgumentOutOfRangeException(nameof(product));
        cancellationToken.ThrowIfCancellationRequested();
        if (product == OfficeProduct.O365ProPlusRetail || product == OfficeProduct.O365BusinessRetail)
            return OfficeActivationState.UserActivationRequired;
        var id = product.ToString();
        var family = id.StartsWith("Standard", StringComparison.Ordinal) ? "Standard" : "ProPlus";
        var year = id.Substring(family.Length, 4);
        // Filter key presence in the provider; never copy even a partial key into managed result objects.
        var rows = new CimManager().Query("root/cimv2", "SELECT Name, LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationID='0ff1ce15-a989-479d-af46-f275c6370663' AND PartialProductKey IS NOT NULL AND PartialProductKey <> ''", timeout, cancellationToken);
        return rows.Any(row => OfficeVersionResolver.IsMatch(row.GetValue("Name") as string, @"\bOffice\s*\d+,\s*Office\d+" + family + year + "VL_")
            && Convert.ToString(row.GetValue("LicenseStatus"), CultureInfo.InvariantCulture) == "1") ? OfficeActivationState.Licensed : OfficeActivationState.NotVerified;
    }
    public Task<OfficeActivationState> GetStatusAsync(OfficeProduct product, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetStatus(product, timeout, cancellationToken), cancellationToken);
}
