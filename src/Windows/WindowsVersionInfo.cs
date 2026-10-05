using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Windows;

/// <summary>Detached Windows version metadata. Missing optional registry fields remain null.</summary>
public sealed class WindowsVersionInfo
{
    public string? ProductName { get; }
    public string? EditionId { get; }
    public string? InstallationType { get; }
    public string? DisplayVersion { get; }
    public int BuildNumber { get; }
    public int UpdateBuildRevision { get; }
    public string? ReleaseId { get; }
    public string? BuildBranch { get; }
    public DateTimeOffset? InstalledAt { get; }
    public string? RegisteredOwner { get; }
    public bool InvalidInstallDate { get; }

    public WindowsVersionInfo(IEnumerable<RegistryValueEntry> values)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        var lookup = values.ToDictionary(entry => entry.Name, entry => entry.Value.Data, StringComparer.OrdinalIgnoreCase);
        object? Value(string name) => lookup.TryGetValue(name, out var value) ? value : null;
        string? Text(string name) => lookup.ContainsKey(name) ? Convert.ToString(Value(name), CultureInfo.InvariantCulture) : null;
        BuildNumber = Convert.ToInt32(Value("CurrentBuild"), CultureInfo.InvariantCulture);
        UpdateBuildRevision = Convert.ToInt32(Value("UBR"), CultureInfo.InvariantCulture);
        ProductName = NormalizeProductName(Text("ProductName"), BuildNumber);
        EditionId = Text("EditionID");
        InstallationType = Text("InstallationType");
        DisplayVersion = Text("DisplayVersion");
        ReleaseId = Text("ReleaseId");
        BuildBranch = Text("BuildBranch");
        RegisteredOwner = Text("RegisteredOwner");
        if (lookup.ContainsKey("InstallDate"))
        {
            try
            { InstalledAt = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(Value("InstallDate"), CultureInfo.InvariantCulture)); }
            catch (Exception error) when (error is FormatException || error is OverflowException || error is ArgumentOutOfRangeException || error is InvalidCastException)
            { InvalidInstallDate = true; }
        }
    }

    public static string? NormalizeProductName(string? name, int buildNumber) => buildNumber >= 22000 && name != null
        && name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase) ? "Windows 11" + name.Substring(10) : name;
}

/// <summary>Pure applicability checks. A null constraint is unrestricted; bounds are inclusive.</summary>
public static class HostValidator
{
    public static bool IsApplicable(int buildNumber, string? edition, bool is64BitOperatingSystem,
        int? minimumBuild = null, int? maximumBuild = null, IEnumerable<string>? editions = null, bool? require64Bit = null)
    {
        if (minimumBuild.HasValue && buildNumber < minimumBuild.Value || maximumBuild.HasValue && buildNumber > maximumBuild.Value)
            return false;
        var allowed = editions?.ToArray();
        return (allowed == null || allowed.Length == 0 || allowed.Contains(edition, StringComparer.OrdinalIgnoreCase))
            && (!require64Bit.HasValue || is64BitOperatingSystem == require64Bit.Value);
    }
}
