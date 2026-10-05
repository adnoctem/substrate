using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace AdNoctem.Substrate.Networking.Compatibility;

internal sealed class LegacyNetworkAdapter
{
    internal Func<string> ReadName = () => "";
    internal Func<string, string?[]> ReadValues = _ => Array.Empty<string?>();
    internal Func<string?> ReadMACAddress = () => null;
    private string?[] Addresses => ReadValues("IPAddress");
    private string?[] Subnets => ReadValues("IPSubnet");
    private string?[] Gateways => ReadValues("DefaultIPGateway");
    private string?[] DnsServers => ReadValues("DNSServerSearchOrder");
    internal readonly List<string> Messages = new List<string>();
    internal string? Failure;
    internal bool Required;

    private object? Missing(string detail)
    {
        var message = detail + " on adapter '" + ReadName() + "'.";
        if (Required)
            Failure = message;
        else
            Messages.Add(message);
        return null;
    }
    private string? Address(bool ipv6)
    {
        var matching = Addresses.Where(value => value != null && value.Contains(":") == ipv6).ToArray();
        var address = ipv6 ? matching.FirstOrDefault(value => !value!.StartsWith("fe80", StringComparison.OrdinalIgnoreCase)) ?? matching.FirstOrDefault() : matching.FirstOrDefault();
        return address ?? (string?)Missing("No " + (ipv6 ? "IPv6" : "IPv4") + " address found");
    }
    private string? Mask()
    {
        for (var index = 0; index < Addresses.Length; index++)
            if (!string.IsNullOrEmpty(Addresses[index]) && !Addresses[index]!.Contains(":") && index < Subnets.Length)
                return Subnets[index] ?? (string?)Missing("No IPv4 subnet mask found");
        return (string?)Missing("No IPv4 subnet mask found");
    }
    internal object? Get(string operation, bool ipv6)
    {
        switch (operation)
        {
            case "Get-IPAddress":
                return Address(ipv6);
            case "Get-SubnetMask":
                return Mask();
            case "Get-MACAddress":
                return ReadMACAddress() ?? Missing("No MAC address found");
            case "Get-DefaultGateway":
                return Gateways.FirstOrDefault(value => value != null && value.Contains(":") == ipv6)
                    ?? Missing("No " + (ipv6 ? "IPv6" : "IPv4") + " default gateway found");
            case "Get-DNSServer":
                var servers = DnsServers.Where(value => value != null && value.Contains(":") == ipv6).ToArray();
                return servers.Length == 0 ? Missing("No " + (ipv6 ? "IPv6" : "IPv4") + " DNS servers found") : servers;
            case "Get-MulticastAddress":
                var ip6 = Address(true);
                return ip6 == null ? null : new IPAddress(new NetworkManager().GetMulticastAddress(IPAddress.Parse(ip6)).GetAddressBytes()).ToString();
            case "Get-NetworkPrefix":
            case "Get-NetworkPrefixCIDR":
                if (ipv6)
                    return IPv6Prefix(operation == "Get-NetworkPrefixCIDR");
                break;
            case "Get-BroadcastAddress":
                break;
            default:
                throw new ArgumentException("Unsupported compatibility operation.", nameof(operation));
        }
        var address = Address(false);
        if (Failure != null)
            return null;
        var mask = Mask();
        if (address == null || mask == null || Failure != null)
        {
            if (operation == "Get-NetworkPrefixCIDR" && Failure == null)
                _ = Mask();
            return null;
        }
        var bytes = IPAddress.Parse(address).GetAddressBytes();
        var maskBytes = IPAddress.Parse(mask).GetAddressBytes();
        for (var index = 0; index < 4; index++)
            bytes[index] = operation == "Get-BroadcastAddress" ? (byte)(bytes[index] | (~maskBytes[index] & 255)) : (byte)(bytes[index] & maskBytes[index]);
        var result = new IPAddress(bytes.Take(4).ToArray()).ToString();
        return operation == "Get-NetworkPrefixCIDR" ? result + "/" + maskBytes.Sum(value => Convert.ToString(value, 2).Count(bit => bit == '1')).ToString(CultureInfo.InvariantCulture) : result;
    }
    private object? IPv6Prefix(bool cidr)
    {
        for (var index = 0; index < Addresses.Length; index++)
        {
            if (Addresses[index]?.Contains(":") != true || index >= Subnets.Length || string.IsNullOrWhiteSpace(Subnets[index]))
                continue;
            var subnet = Subnets[index]!;
            int length;
            if (System.Text.RegularExpressions.Regex.IsMatch(subnet, @"^\d+$"))
                length = int.Parse(subnet, CultureInfo.InvariantCulture);
            else
            {
                length = 0;
                foreach (var value in IPAddress.Parse(subnet).GetAddressBytes())
                {
                    if (value == 255)
                    { length += 8; continue; }
                    for (var bit = 7; bit >= 0 && ((value >> bit) & 1) != 0; bit--)
                        length++;
                    break;
                }
            }
            var bytes = IPAddress.Parse(Addresses[index]!).GetAddressBytes();
            // v1 casts a shifted 0xff to byte before applying partial-byte masks. Preserve its overflow at the PowerShell boundary only.
            if (length > 0 && length < 128 && length % 8 != 0)
                throw new LegacyNetworkMaskException(255 << (8 - length % 8));
            for (var bit = Math.Max(0, length); bit < 128; bit++)
                bytes[bit / 8] &= (byte)~(1 << (7 - bit % 8));
            var result = new IPAddress(bytes).ToString();
            return cidr ? result + "/" + length.ToString(CultureInfo.InvariantCulture) : result;
        }
        return Missing("No IPv6 address with a valid prefix length found");
    }
}

internal sealed class LegacyNetworkMaskException : Exception
{
    internal int Value { get; }
    internal LegacyNetworkMaskException(int value) { Value = value; }
}
