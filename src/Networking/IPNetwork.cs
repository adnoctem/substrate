using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AdNoctem.Substrate.Networking;

/// <summary>An immutable normalized IPv4 or IPv6 network. Address properties return copies because IPAddress exposes mutable state.</summary>
public sealed class IPNetwork : IEquatable<IPNetwork>
{
    private readonly byte[] network;
    public AddressFamily AddressFamily =>
        network.Length == 4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
    public int PrefixLength { get; }
    public IPAddress NetworkAddress => new IPAddress((byte[])network.Clone());
    public IPAddress LastAddress
    {
        get
        {
            var result = (byte[])network.Clone();

            for (var bit = PrefixLength; bit < result.Length * 8; bit++)
                result[bit / 8] |= (byte)(1 << (7 - bit % 8));

            return new IPAddress(result);
        }
    }
    public IPAddress SubnetMask
    {
        get
        {
            var result = new byte[network.Length];

            for (var bit = 0; bit < PrefixLength; bit++)
                result[bit / 8] |= (byte)(1 << (7 - bit % 8));

            return new IPAddress(result);
        }
    }

    /// <remarks>Host bits are cleared. Scoped IPv6 addresses are rejected: an interface scope is not part of a network prefix.
    /// LastAddress is a range endpoint; only IPv4 defines it as a broadcast address, and /31 and /32 have special host semantics.</remarks>
    public IPNetwork(IPAddress address, int prefixLength)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        if (
            address.AddressFamily != AddressFamily.InterNetwork
            && address.AddressFamily != AddressFamily.InterNetworkV6
        )
            throw new ArgumentException("An IPv4 or IPv6 address is required.", nameof(address));

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
            throw new ArgumentException(
                "Network prefixes cannot carry an interface scope.",
                nameof(address)
            );

        network = address.GetAddressBytes();

        if (prefixLength < 0 || prefixLength > network.Length * 8)
            throw new ArgumentOutOfRangeException(nameof(prefixLength));

        PrefixLength = prefixLength;

        for (var bit = PrefixLength; bit < network.Length * 8; bit++)
            network[bit / 8] &= (byte)~(1 << (7 - bit % 8));
    }

    public static IPNetwork Parse(string cidr)
    {
        if (!TryParse(cidr, out var network))
            throw new FormatException(
                "Expected an IPv4 or IPv6 address followed by a valid CIDR prefix length."
            );

        return network!;
    }

    public static bool TryParse(string? cidr, out IPNetwork? network)
    {
        network = null;

        if (cidr == null)
            return false;

        var parts = cidr.Split('/');

        if (
            parts.Length != 2
            || !IPAddressParser.TryParse(parts[0], out var address)
            || address!.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var prefix
            )
            || prefix < 0
            || prefix > address.GetAddressBytes().Length * 8
        )
            return false;

        network = new IPNetwork(address, prefix);

        return true;
    }

    /// <summary>Converts a contiguous mask; masks with holes are rejected instead of silently counting their set bits.</summary>
    public static IPNetwork FromSubnetMask(IPAddress address, IPAddress subnetMask)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        if (subnetMask == null)
            throw new ArgumentNullException(nameof(subnetMask));

        if (address.AddressFamily != subnetMask.AddressFamily)
            throw new ArgumentException(
                "Address and mask families must match.",
                nameof(subnetMask)
            );

        var prefix = 0;
        var ended = false;

        foreach (var value in subnetMask.GetAddressBytes())
            for (var bit = 7; bit >= 0; bit--)
            {
                if ((value & (1 << bit)) == 0)
                    ended = true;
                else if (ended)
                    throw new ArgumentException(
                        "Subnet masks must be contiguous.",
                        nameof(subnetMask)
                    );
                else
                    prefix++;
            }

        return new IPNetwork(address, prefix);
    }

    public bool Contains(IPAddress address)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        var bytes = address.GetAddressBytes();

        if (bytes.Length != network.Length)
            return false;

        for (var bit = 0; bit < PrefixLength; bit++)
            if (
                (bytes[bit / 8] & (1 << (7 - bit % 8))) != (network[bit / 8] & (1 << (7 - bit % 8)))
            )
                return false;

        return true;
    }

    public bool Contains(IPNetwork other) =>
        other != null && PrefixLength <= other.PrefixLength && Contains(other.NetworkAddress);

    public bool Overlaps(IPNetwork other) =>
        other != null && (Contains(other) || other.Contains(this));

    public bool Equals(IPNetwork? other) =>
        other != null && PrefixLength == other.PrefixLength && network.SequenceEqual(other.network);

    public override bool Equals(object? obj) => obj is IPNetwork other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = PrefixLength;

            foreach (var value in network)
                hash = hash * 31 + value;

            return hash;
        }
    }

    public override string ToString() =>
        NetworkAddress + "/" + PrefixLength.ToString(CultureInfo.InvariantCulture);
}
