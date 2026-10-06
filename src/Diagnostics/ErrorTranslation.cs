using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdNoctem.Substrate.Diagnostics;

public enum ErrorDomain
{
    Appx,
    Winget,
    Dism,
    Msi,
}

/// <summary>Known diagnostic guidance. Recognizing a code never authorizes suppressing a failure or retrying an operation.</summary>
public sealed class ErrorTranslation
{
    public uint Code { get; }
    public ErrorDomain Domain { get; }
    public bool Benign { get; }
    public string Detail { get; }
    public string DisplayCode =>
        Domain == ErrorDomain.Msi
            ? Code.ToString(CultureInfo.InvariantCulture)
            : "0x" + Code.ToString("X8", CultureInfo.InvariantCulture);

    internal ErrorTranslation(uint code, ErrorDomain domain, bool benign, string detail)
    {
        Code = code;
        Domain = domain;
        Benign = benign;
        Detail = detail;
    }
}

/// <summary>Maps known native error codes to curated guidance. Unknown or ambiguous codes remain untranslated.</summary>
public sealed class ErrorTranslator
{
    private static readonly Regex TextCodes = new Regex(
        @"(?i)(?<![\w])(?:0x[0-9a-f]{8}|-\d{10}|[23]\d{9})(?![\w])",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );
    private static readonly ErrorTranslation[] Entries =
    {
        new ErrorTranslation(
            0x80073CF0,
            ErrorDomain.Appx,
            false,
            "The package could not be opened. Check its path, access, signature, and the AppxPackagingOM log."
        ),
        new ErrorTranslation(
            0x80073CF3,
            ErrorDomain.Appx,
            false,
            "Dependency or conflict validation failed. Check dependencies, architecture, and AppXDeployment-Server logs."
        ),
        new ErrorTranslation(
            0x80073CF9,
            ErrorDomain.Appx,
            false,
            "Package installation failed. Inspect AppXDeployment-Server logs for the specific cause."
        ),
        new ErrorTranslation(
            0x80073CFB,
            ErrorDomain.Appx,
            false,
            "A package is already installed and blocks reinstalling this package. Verify identity and version before deciding to skip."
        ),
        new ErrorTranslation(
            0x80073D02,
            ErrorDomain.Appx,
            false,
            "Resources needed by the package are in use. Close affected applications and retry when the locks are released."
        ),
        new ErrorTranslation(
            0x80073D06,
            ErrorDomain.Appx,
            false,
            "A newer package version is installed. Skip only if that version satisfies the requested state; downgrades remain failures."
        ),
        new ErrorTranslation(
            0x8A150011,
            ErrorDomain.Winget,
            false,
            "The installer hash differs from the manifest. Refresh the source and investigate the mismatch; do not bypass verification."
        ),
        new ErrorTranslation(
            0x8A15002B,
            ErrorDomain.Winget,
            false,
            "No applicable update was found. Verify the installed version and requested target before deciding to skip."
        ),
        new ErrorTranslation(
            0x8A15002C,
            ErrorDomain.Winget,
            false,
            "One or more upgrades failed. Review the individual package results and WinGet logs."
        ),
        new ErrorTranslation(
            0x800F081F,
            ErrorDomain.Dism,
            false,
            "Required source files were not found. Provide a repair source matching the target Windows image and inspect DISM/CBS logs."
        ),
        new ErrorTranslation(
            0x800F0906,
            ErrorDomain.Dism,
            false,
            "Required source files could not be downloaded. Check connectivity and servicing-source policy, or provide a matching local source."
        ),
        new ErrorTranslation(0, ErrorDomain.Msi, true, "The installer completed successfully."),
        new ErrorTranslation(
            1602,
            ErrorDomain.Msi,
            false,
            "The user cancelled installation. Retry only when installation is still intended."
        ),
        new ErrorTranslation(
            1603,
            ErrorDomain.Msi,
            false,
            "Installation failed. Enable verbose MSI logging and inspect the failure before retrying."
        ),
        new ErrorTranslation(
            1618,
            ErrorDomain.Msi,
            false,
            "Another installation is running. Wait for it to finish before retrying."
        ),
        new ErrorTranslation(
            1638,
            ErrorDomain.Msi,
            false,
            "Another version of the product is installed. Check the requested version and upgrade path."
        ),
        new ErrorTranslation(
            1641,
            ErrorDomain.Msi,
            true,
            "Installation succeeded and initiated a restart. Resume dependent work after restart."
        ),
        new ErrorTranslation(
            3010,
            ErrorDomain.Msi,
            true,
            "Installation succeeded; a restart is required to complete it. Schedule a restart before dependent work."
        ),
    };

    public ErrorTranslation? Translate(uint code, ErrorDomain domain)
    {
        ValidateDomain(domain);

        return Entries.SingleOrDefault(e => e.Code == code && e.Domain == domain);
    }

    /// <remarks>Recognized outer-to-inner HRESULTs take priority. Multiple distinct recognized text codes are ambiguous and return null.
    /// Exception traversal is bounded at 32 levels. This method never logs the exception or its potentially sensitive message.</remarks>
    public ErrorTranslation? Translate(
        Exception exception,
        ErrorDomain? domain = null,
        string? additionalDetails = null
    )
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        if (domain.HasValue)
            ValidateDomain(domain.Value);

        var messages = new List<string>();

        if (additionalDetails != null)
            messages.Add(additionalDetails);

        var entries = Entries.Where(e => !domain.HasValue || e.Domain == domain.Value).ToArray();
        Exception? current = exception;

        for (
            var depth = 0;
            current != null && depth < 32;
            depth++, current = current.InnerException
        )
        {
            var matches = entries.Where(e => e.Code == unchecked((uint)current.HResult)).ToArray();

            if (matches.Length == 1)
                return matches[0];

            if (matches.Length > 1)
                return null;

            messages.Add(current.Message);
        }

        var codes = new HashSet<uint>();

        foreach (var message in messages)
        foreach (Match match in TextCodes.Matches(message))
            if (TryParseCode(match.Value, out var code))
                codes.Add(code);

        var hits = entries.Where(e => codes.Contains(e.Code)).ToArray();

        return hits.Length == 1 ? hits[0] : null;
    }

    public static bool TryParseCode(string text, out uint code)
    {
        code = 0;

        if (text == null)
            return false;

        text = text.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return text.Length >= 3
                && text.Length <= 10
                && uint.TryParse(
                    text.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out code
                );

        var digits = text.StartsWith("-", StringComparison.Ordinal) ? text.Substring(1) : text;

        if (digits.Length == 0 || digits.Length > 10 || digits.Any(c => c < '0' || c > '9'))
            return false;

        if (
            !long.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value
            )
            || value < int.MinValue
            || value > uint.MaxValue
        )
            return false;

        code = unchecked((uint)value);

        return true;
    }

    private static void ValidateDomain(ErrorDomain domain)
    {
        if (!Enum.IsDefined(typeof(ErrorDomain), domain))
            throw new ArgumentOutOfRangeException(nameof(domain));
    }
}
