using System;
using System.Linq;
using System.Net.Sockets;

namespace PSFoundation.Networking;

/// <summary>Pure validation of network values. Does not resolve names, inspect adapters or contact a remote machine.</summary>
public static class NetworkValidator
{
    /// <summary>Accepts strict dotted-decimal IPv4 notation and rejects ambiguous numeric shorthand.</summary>
    public static bool TestIPv4Address(string? address) => IPAddressParser.TryParse(address, out var parsed) && parsed!.AddressFamily == AddressFamily.InterNetwork;
    /// <summary>Tests whether the input is a supported IPv6 literal without performing DNS resolution.</summary>
    public static bool TestIPv6Address(string? address) => IPAddressParser.TryParse(address, out var parsed) && parsed!.AddressFamily == AddressFamily.InterNetworkV6;
    /// <summary>Tests address and prefix-length syntax without inspecting local routes.</summary>
    public static bool TestNetworkPrefix(string? prefix) => IPNetwork.TryParse(prefix, out _);
    /// <summary>Accepts only IPv4 masks with contiguous leading one bits.</summary>
    public static bool TestSubnetMask(string? mask)
    {
        if (!IPAddressParser.TryParse(mask, out var parsed))
            return false;
        try
        { _ = IPNetwork.FromSubnetMask(parsed!, parsed!); return true; }
        catch (ArgumentException) { return false; }
    }
    /// <summary>Tests supported six-octet MAC address notation; it does not establish an address's ownership or reachability.</summary>
    public static bool TestMACAddress(string? address)
    {
        if (address == null)
            return false;
        var parts = address.Split(address.IndexOf(':') >= 0 ? ':' : '-');
        return parts.Length == 6 && parts.All(part => part.Length == 2 && part.All(Uri.IsHexDigit));
    }
}
