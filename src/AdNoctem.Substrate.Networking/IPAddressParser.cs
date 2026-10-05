using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AdNoctem.Substrate.Networking;

/// <summary>Strict IP literal parsing and address calculations without DNS or network access.</summary>
public static class IPAddressParser
{
    /// <summary>Parses address literals only. IPv4 requires four decimal octets without leading zeros; host names, brackets and whitespace are rejected.</summary>
    public static bool TryParse(string? text, out IPAddress? address)
    {
        address = null;
        if (text == null || text.Length == 0 || text.Any(char.IsWhiteSpace) || text.IndexOfAny(new[] { '[', ']', '/' }) >= 0)
            return false;
        if (text.IndexOf(':') < 0)
        {
            var parts = text.Split('.');
            if (parts.Length != 4 || parts.Any(p => p.Length == 0 || p.Length > 1 && p[0] == '0'
                || !byte.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                return false;
        }
        else if (text.IndexOf('.') >= 0)
        {
            var suffix = text.Substring(text.LastIndexOf(':') + 1);
            if (!TryParse(suffix, out var embedded) || embedded!.AddressFamily != AddressFamily.InterNetwork)
                return false;
        }
        return IPAddress.TryParse(text, out address);
    }

    public static IPAddress Parse(string text)
    {
        if (!TryParse(text, out var address))
            throw new FormatException("An IPv4 or IPv6 address literal is required.");
        return address!;
    }

    public static IPAddress GetSolicitedNodeMulticastAddress(IPAddress address)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            throw new ArgumentException("An IPv6 address is required.", nameof(address));
        var bytes = address.GetAddressBytes();
        var multicast = new byte[] { 0xff, 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0xff, bytes[13], bytes[14], bytes[15] };
        return new IPAddress(multicast, address.ScopeId);
    }
}
