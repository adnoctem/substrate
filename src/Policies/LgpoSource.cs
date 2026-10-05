using System;
using System.Linq;

namespace AdNoctem.Substrate.Policies;

/// <summary>Reviewed vendor download pins. A download must still be verified against its pin before extraction or execution.</summary>
public sealed class LgpoSource
{
    public string Name { get; }
    public Uri DownloadUri { get; }
    public string ExpectedBinaryPath { get; }
    /// <summary>SHA-256 of the complete ZIP, not its contained executable.</summary>
    public string TrustedSha256 { get; }
    public string TrustedBinarySha256 { get; }
    public Version BinaryVersion { get; }
    public DateTime VerifiedAtUtc { get; }
    /// <remarks>Custom pins must be obtained independently of the download being verified. This constructor does not establish trust.</remarks>
    public LgpoSource(string name, Uri downloadUri, string expectedBinaryPath, string trustedSha256, string trustedBinarySha256,
        Version binaryVersion, DateTime verifiedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A source name is required.", nameof(name));
        if (downloadUri == null || !downloadUri.IsAbsoluteUri || downloadUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(downloadUri.UserInfo))
            throw new ArgumentException("An absolute HTTPS URI without credentials is required.", nameof(downloadUri));
        if (string.IsNullOrEmpty(expectedBinaryPath) || expectedBinaryPath.IndexOfAny(new[] { '\\', ':', '\0' }) >= 0
            || expectedBinaryPath.Any(char.IsControl) || expectedBinaryPath.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
            throw new ArgumentException("A canonical relative ZIP entry path is required.", nameof(expectedBinaryPath));
        foreach (var hash in new[] { trustedSha256, trustedBinarySha256 })
            if (hash == null || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new ArgumentException("Independently trusted SHA-256 pins are required.");
        if (binaryVersion == null)
            throw new ArgumentNullException(nameof(binaryVersion));
        if (verifiedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Use a UTC verification date.", nameof(verifiedAtUtc));
        Name = name;
        DownloadUri = downloadUri;
        ExpectedBinaryPath = expectedBinaryPath;
        TrustedSha256 = trustedSha256;
        TrustedBinarySha256 = trustedBinarySha256;
        BinaryVersion = binaryVersion;
        VerifiedAtUtc = verifiedAtUtc;
    }

    // Supplied archive reviewed on 2026-10-04: exactly the executable and two PDFs; the contained executable matches the supplied
    // Microsoft-signed binary with Valid Authenticode status. See docs/migration/progress.md for provenance and future update requirements.
    public static LgpoSource Standalone { get; } = new LgpoSource("Security Compliance Toolkit - LGPO standalone",
        new Uri("https://download.microsoft.com/download/8/5/C/85C25433-A1B0-4FFA-9429-7E023E7DA8D8/LGPO.zip"), "LGPO_30/LGPO.exe",
        "CB7159D134A0A1E7B1ED2ADA9A3CE8CE8F4DE391D14403D55438AF824247CC55",
        "0C97F29543418B30340C4FF5D930D31E6196DD59C2CC74B6B890FA7B90C910C7",
        new Version(3, 0, 2004, 13001), new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc));
}
