using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AdNoctem.Substrate.Networking;

public enum NetworkAdapterKind { Unspecified, Ethernet, WiFi, Other }

/// <summary>An immutable interface address paired with its prefix, when known. Preserves IPv6 scope separately from its network.</summary>
public sealed class NetworkInterfaceAddress
{
    private readonly byte[] bytes;
    private readonly long scope;
    public IPAddress Address => bytes.Length == 16 ? new IPAddress((byte[])bytes.Clone(), scope) : new IPAddress((byte[])bytes.Clone());
    public int? PrefixLength { get; }
    public IPNetwork? Network => PrefixLength.HasValue ? new IPNetwork(new IPAddress(bytes), PrefixLength.Value) : null;
    public NetworkInterfaceAddress(IPAddress address, int? prefixLength = null)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));
        if (address.AddressFamily != AddressFamily.InterNetwork && address.AddressFamily != AddressFamily.InterNetworkV6)
            throw new ArgumentException("An IP address is required.", nameof(address));
        bytes = address.GetAddressBytes();
        scope = bytes.Length == 16 ? address.ScopeId : 0;
        if (prefixLength.HasValue && (prefixLength < 0 || prefixLength > bytes.Length * 8))
            throw new ArgumentOutOfRangeException(nameof(prefixLength));
        PrefixLength = prefixLength;
    }
}

/// <summary>A detached read-only snapshot. It owns no native handles and never changes the adapter it describes.</summary>
/// <remarks>Input collections and mutable address data are copied. Address getters return copies, preserving snapshot identity.
/// Gateway and DNS order follows the observed Windows configuration; it is not a reachability assessment.</remarks>
public sealed class NetworkAdapterSnapshot
{
    private readonly string[] gateways;
    private readonly string[] dnsServers;
    public string Name { get; }
    public uint InterfaceIndex { get; }
    public NetworkAdapterKind Kind { get; }
    public IReadOnlyList<NetworkInterfaceAddress> Addresses { get; }
    public IReadOnlyList<IPAddress> Gateways => Array.AsReadOnly(gateways.Select(IPAddressParser.Parse).ToArray());
    public IReadOnlyList<IPAddress> DnsServers => Array.AsReadOnly(dnsServers.Select(IPAddressParser.Parse).ToArray());
    public string? MACAddress { get; }

    public NetworkAdapterSnapshot(string name, uint interfaceIndex, NetworkAdapterKind kind, IEnumerable<NetworkInterfaceAddress> addresses,
        IEnumerable<IPAddress>? gateways = null, IEnumerable<IPAddress>? dnsServers = null, string? macAddress = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        if (!Enum.IsDefined(typeof(NetworkAdapterKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var copied = (addresses ?? throw new ArgumentNullException(nameof(addresses))).ToArray();
        if (copied.Any(value => value == null))
            throw new ArgumentException("Addresses cannot contain null.", nameof(addresses));
        InterfaceIndex = interfaceIndex;
        Kind = kind;
        Addresses = Array.AsReadOnly(copied);
        this.gateways = Copy(gateways);
        this.dnsServers = Copy(dnsServers);
        MACAddress = macAddress;
    }

    private static string[] Copy(IEnumerable<IPAddress>? values) => (values ?? Array.Empty<IPAddress>()).Select(value =>
        value == null ? throw new ArgumentException("Address collections cannot contain null.") : IPAddressParser.Parse(value.ToString()).ToString()).ToArray();
}
