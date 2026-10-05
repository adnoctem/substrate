using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Networking.Platform;

namespace AdNoctem.Substrate.Networking;

/// <summary>Reads network configuration and computes addresses. No prompts, logging, elevation or configuration changes are performed.</summary>
/// <remarks>Snapshot calculations perform no additional network I/O. Adapter inventory is Windows-specific; pure address calculations are reusable independently.</remarks>
/// <example><code>
/// var manager = new NetworkManager();
/// var network = manager.GetNetworkPrefix(IPAddress.Parse("192.0.2.129"), 25);
/// // Canonical prefix: 192.0.2.128/25
/// </code></example>
public sealed class NetworkManager
{
    /// <summary>Reads detached adapter, address, route, and DNS observations using Windows CIM.</summary>
    public IReadOnlyList<NetworkAdapterSnapshot> GetAdapters(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using (var data = CimNetworkReader.Read(timeout, cancellationToken))
            return Array.AsReadOnly(data.Adapters.Select(data.ToSnapshot).ToArray());
    }

    /// <summary>Selects the adapter for the lowest-metric eligible default route, or returns null if none matches.</summary>
    /// <remarks>Uses the lowest route metric among nonzero default next hops for the requested family. It does not claim Internet reachability.
    /// A kind filter applies to that selected adapter; it does not silently choose another route. Missing routes return null; provider failures throw.</remarks>
    public NetworkAdapterSnapshot? GetDefaultNetworkAdapter(AddressFamily family, TimeSpan timeout, NetworkAdapterKind? kind = null, CancellationToken cancellationToken = default)
    {
        CheckFamily(family);
        if (kind.HasValue && !Enum.IsDefined(typeof(NetworkAdapterKind), kind.Value))
            throw new ArgumentOutOfRangeException(nameof(kind));
        using (var data = CimNetworkReader.Read(timeout, cancellationToken))
        {
            var selected = data.SelectDefault(family);
            if (selected == null)
                return null;
            var result = data.ToSnapshot(selected);
            return kind.HasValue && result.Kind != kind.Value ? null : result;
        }
    }

    /// <summary>Reads adapter observations on a worker thread.</summary>
    /// <remarks>CIM reads are synchronous native operations with explicit operation timeouts and cancellation. Async inventory offloads them
    /// to the thread pool; cancellation depends on the Windows provider honoring its cancellation request.</remarks>
    public Task<IReadOnlyList<NetworkAdapterSnapshot>> GetAdaptersAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetAdapters(timeout, cancellationToken), cancellationToken);
    /// <summary>Selects a default adapter on a worker thread using the same route and kind-filter rules.</summary>
    public Task<NetworkAdapterSnapshot?> GetDefaultNetworkAdapterAsync(AddressFamily family, TimeSpan timeout, NetworkAdapterKind? kind = null, CancellationToken cancellationToken = default)
        => Task.Run(() => GetDefaultNetworkAdapter(family, timeout, kind, cancellationToken), cancellationToken);

    /// <summary>Selects an address of the requested family; IPv6 prefers a non-link-local address. Returns null when absent.</summary>
    public IPAddress? GetIPAddress(NetworkAdapterSnapshot adapter, AddressFamily family)
    {
        Check(adapter, family);
        var addresses = adapter.Addresses.Select(item => item.Address).Where(value => value.AddressFamily == family).ToArray();
        return family == AddressFamily.InterNetworkV6 ? addresses.FirstOrDefault(value => !value.IsIPv6LinkLocal) ?? addresses.FirstOrDefault() : addresses.FirstOrDefault();
    }
    /// <summary>Returns the mask of the first observed IPv4 prefix, or null when prefix information is absent.</summary>
    public IPAddress? GetSubnetMask(NetworkAdapterSnapshot adapter) => GetNetworkPrefix(adapter, AddressFamily.InterNetwork)?.SubnetMask;
    /// <summary>Returns the first observed gateway of the requested family, without probing reachability.</summary>
    public IPAddress? GetDefaultGateway(NetworkAdapterSnapshot adapter, AddressFamily family)
    { Check(adapter, family); return adapter.Gateways.FirstOrDefault(value => value.AddressFamily == family); }
    /// <summary>Returns observed DNS server addresses of the requested family, preserving their order.</summary>
    public IReadOnlyList<IPAddress> GetDNSServers(NetworkAdapterSnapshot adapter, AddressFamily family)
    { Check(adapter, family); return Array.AsReadOnly(adapter.DnsServers.Where(value => value.AddressFamily == family).ToArray()); }
    public string? GetMACAddress(NetworkAdapterSnapshot adapter) => (adapter ?? throw new ArgumentNullException(nameof(adapter))).MACAddress;
    /// <summary>Builds or selects a canonical network prefix; adapter overloads return null when no matching prefix was observed.</summary>
    public IPNetwork? GetNetworkPrefix(NetworkAdapterSnapshot adapter, AddressFamily family)
    { Check(adapter, family); return adapter.Addresses.FirstOrDefault(value => value.Address.AddressFamily == family && value.PrefixLength.HasValue)?.Network; }
    /// <summary>Formats the selected adapter prefix as canonical CIDR, or returns null when unavailable.</summary>
    public string? GetNetworkPrefixCIDR(NetworkAdapterSnapshot adapter, AddressFamily family) => GetNetworkPrefix(adapter, family)?.ToString();
    /// <summary>Returns the IPv4 range's upper endpoint; this does not establish that the address is a usable broadcast destination.</summary>
    public IPAddress? GetBroadcastAddress(NetworkAdapterSnapshot adapter) => GetNetworkPrefix(adapter, AddressFamily.InterNetwork)?.LastAddress;
    /// <summary>Computes the IPv6 solicited-node multicast address from the supplied or selected adapter address.</summary>
    public IPAddress? GetMulticastAddress(NetworkAdapterSnapshot adapter)
    { var address = GetIPAddress(adapter, AddressFamily.InterNetworkV6); return address == null ? null : GetMulticastAddress(address); }

    /// <summary>Builds or selects a canonical network prefix; adapter overloads return null when no matching prefix was observed.</summary>
    public IPNetwork GetNetworkPrefix(IPAddress address, int prefixLength) => new IPNetwork(address, prefixLength);
    /// <summary>Builds or selects a canonical network prefix; adapter overloads return null when no matching prefix was observed.</summary>
    public IPNetwork GetNetworkPrefix(IPAddress address, IPAddress subnetMask) => IPNetwork.FromSubnetMask(address, subnetMask);
    /// <summary>Returns the IPv4 range's upper endpoint; this does not establish that the address is a usable broadcast destination.</summary>
    /// <remarks>Returns the upper IPv4 range endpoint. /31 and /32 do not imply a usable broadcast destination.</remarks>
    public IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));
        if (address.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Broadcast calculation requires IPv4.", nameof(address));
        return IPNetwork.FromSubnetMask(address, subnetMask).LastAddress;
    }
    /// <summary>Computes the IPv6 solicited-node multicast address from the supplied or selected adapter address.</summary>
    public IPAddress GetMulticastAddress(IPAddress address) => IPAddressParser.GetSolicitedNodeMulticastAddress(address);

    private static void Check(NetworkAdapterSnapshot adapter, AddressFamily family)
    { if (adapter == null) throw new ArgumentNullException(nameof(adapter)); CheckFamily(family); }
    internal static void CheckFamily(AddressFamily family)
    { if (family != AddressFamily.InterNetwork && family != AddressFamily.InterNetworkV6) throw new ArgumentOutOfRangeException(nameof(family)); }
}
