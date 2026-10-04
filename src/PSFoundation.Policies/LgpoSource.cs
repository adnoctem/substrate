using System;

namespace PSFoundation.Policies;

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
    private LgpoSource(string name, Uri downloadUri, string expectedBinaryPath, string trustedSha256, string trustedBinarySha256,
        Version binaryVersion, DateTime verifiedAtUtc)
    {
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
