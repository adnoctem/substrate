using System;
using System.Linq;

namespace AdNoctem.Substrate.Networking.Compatibility;

// Preserve the script validators' acceptance quirks separately from the public address parser.
internal static class LegacyAddressValidator
{
    public static bool IsIPv4(string address)
    {
        if (string.IsNullOrEmpty(address))
            return false;

        var octets = address.Split('.');

        return octets.Length == 4
            && octets.All(o =>
                o.Length != 0
                && !(o.Length > 1 && o[0] == '0')
                && int.TryParse(o, out var value)
                && value >= 0
                && value <= 255
            );
    }

    public static bool IsIPv6(string address)
    {
        if (string.IsNullOrEmpty(address))
            return false;

        var lastColon = address.LastIndexOf(':');

        if (lastColon >= 0 && address.Substring(lastColon + 1).Contains('.'))
        {
            if (!IsIPv4(address.Substring(lastColon + 1)))
                return false;

            address = address.Substring(0, lastColon + 1) + "0";
        }

        var count = 0;
        var position = 0;

        while ((position = address.IndexOf("::", position, StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += 2;
        }

        if (count > 1)
            return false;

        var groups =
            count == 1
                ? address.Split(':').Where(g => g.Length != 0).ToArray()
                : address.Split(':');

        if (count == 1 ? groups.Length > 7 : groups.Length != 8)
            return false;

        return groups.All(g =>
            g.Length >= 1 && g.Length <= 4 && g.All(c => "0123456789abcdefABCDEF".IndexOf(c) >= 0)
        );
    }
}
