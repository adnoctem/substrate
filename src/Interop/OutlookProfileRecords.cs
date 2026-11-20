using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Interop;

/// <summary>
/// The delivery store each mail account of a MAPI profile records for itself. Outlook 2007 cannot report an account's
/// delivery store, but its profile keeps it as the account's "Delivery Store EntryID", which is that store's StoreID.
/// </summary>
internal sealed class OutlookProfileRecords
{
    /// <summary>Profiles of Outlook 2007 and 2010, which are the clients without Account.DeliveryStore.</summary>
    internal const string ProfilesKey =
        @"Software\Microsoft\Windows NT\CurrentVersion\Windows Messaging Subsystem\Profiles";

    /// <summary>Test seam: an HKCU-relative key that replaces <see cref="ProfilesKey"/> for synthetic profiles.</summary>
    internal const string ProfilesKeyVariable = "SUBSTRATE_OUTLOOK_PROFILES_KEY";

    private const string AccountsSubkey = "9375CFF0413111d3B88A00104B2A6676";

    private readonly Dictionary<string, string?> _deliveryByName;

    /// <summary>Why the records could not be read, or empty when they were.</summary>
    internal string Evidence { get; }

    private OutlookProfileRecords(Dictionary<string, string?> deliveryByName, string evidence)
    {
        _deliveryByName = deliveryByName;
        Evidence = evidence;
    }

    internal static OutlookProfileRecords Empty(string evidence) =>
        new OutlookProfileRecords(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            evidence
        );

    /// <summary>
    /// The recorded delivery store entry ID as uppercase hex, or null when the profile has no record for the account
    /// or more than one account record carries the same name.
    /// </summary>
    internal string? DeliveryEntryId(string accountName)
    {
        if (string.IsNullOrEmpty(accountName))
            return null;

        return _deliveryByName.TryGetValue(accountName, out var entryId) ? entryId : null;
    }

    internal static OutlookProfileRecords Read(string profileName)
    {
        if (string.IsNullOrEmpty(profileName))
            return Empty("The current profile name is unavailable.");

        var root = Environment.GetEnvironmentVariable(ProfilesKeyVariable);

        if (string.IsNullOrEmpty(root))
            root = ProfilesKey;

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var accounts = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                root + @"\" + profileName + @"\" + AccountsSubkey
            );

            if (accounts == null)
                return Empty("The profile '" + profileName + "' has no account records.");

            foreach (var child in accounts.GetSubKeyNames())
            {
                using var key = accounts.OpenSubKey(child);

                if (key == null)
                    continue;

                var name = Text(key.GetValue("Account Name"));

                if (
                    string.IsNullOrEmpty(name)
                    || !(key.GetValue("Delivery Store EntryID") is byte[] entryId)
                )
                    continue;

                // A name recorded twice cannot say which record belongs to which account.
                result[name] = result.ContainsKey(name) ? null : Hex(entryId);
            }
        }
        catch (Exception error)
            when (error is System.Security.SecurityException
                || error is UnauthorizedAccessException
                || error is System.IO.IOException
            )
        {
            return Empty("The profile account records are unreadable: " + error.Message);
        }

        return new OutlookProfileRecords(result, "");
    }

    /// <summary>Profile strings are stored as null-terminated Unicode REG_BINARY or as REG_SZ.</summary>
    private static string Text(object? value)
    {
        if (value is string text)
            return text.Trim('\0');

        if (value is byte[] bytes && bytes.Length > 0)
            return Encoding.Unicode.GetString(bytes).Trim('\0');

        return "";
    }

    private static string Hex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);

        foreach (var value in bytes)
            builder.Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));

        return builder.ToString();
    }
}
