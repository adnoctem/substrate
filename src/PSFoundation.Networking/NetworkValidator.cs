using System;
using System.Linq;
using System.Net.Sockets;

namespace PSFoundation.Networking;

/// <summary>Pure validation of network values. Does not resolve names, inspect adapters or contact a remote machine.</summary>
public static class NetworkValidator
{
    public static bool TestIPv4Address(string? address) => IPAddressParser.TryParse(address, out var parsed) && parsed!.AddressFamily == AddressFamily.InterNetwork;
    public static bool TestIPv6Address(string? address) => IPAddressParser.TryParse(address, out var parsed) && parsed!.AddressFamily == AddressFamily.InterNetworkV6;
    public static bool TestNetworkPrefix(string? prefix) => IPNetwork.TryParse(prefix, out _);
    public static bool TestSubnetMask(string? mask)
    {
        if (!IPAddressParser.TryParse(mask, out var parsed))
            return false;
        try
        { _ = IPNetwork.FromSubnetMask(parsed!, parsed!); return true; }
        catch (ArgumentException) { return false; }
    }
    public static bool TestMACAddress(string? address)
    {
        if (address == null)
            return false;
        var parts = address.Split(address.IndexOf(':') >= 0 ? ':' : '-');
        return parts.Length == 6 && parts.All(part => part.Length == 2 && part.All(Uri.IsHexDigit));
    }
}
