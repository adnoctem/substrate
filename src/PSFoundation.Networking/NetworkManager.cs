using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Networking.Platform;

namespace PSFoundation.Networking;

/// <summary>Reads network configuration and computes addresses. No prompts, logging, elevation or configuration changes are performed.</summary>
public sealed class NetworkManager
{
    public IReadOnlyList<NetworkAdapterSnapshot> GetAdapters(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using (var data = CimNetworkReader.Read(timeout, cancellationToken))
            return Array.AsReadOnly(data.Adapters.Select(data.ToSnapshot).ToArray());
    }

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

    /// <remarks>CIM reads are synchronous native operations with explicit operation timeouts and cancellation. Async inventory offloads them
    /// to the thread pool; cancellation depends on the Windows provider honoring its cancellation request.</remarks>
    public Task<IReadOnlyList<NetworkAdapterSnapshot>> GetAdaptersAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetAdapters(timeout, cancellationToken), cancellationToken);
    public Task<NetworkAdapterSnapshot?> GetDefaultNetworkAdapterAsync(AddressFamily family, TimeSpan timeout, NetworkAdapterKind? kind = null, CancellationToken cancellationToken = default)
        => Task.Run(() => GetDefaultNetworkAdapter(family, timeout, kind, cancellationToken), cancellationToken);

    public IPAddress? GetIPAddress(NetworkAdapterSnapshot adapter, AddressFamily family)
    {
        Check(adapter, family);
        var addresses = adapter.Addresses.Select(item => item.Address).Where(value => value.AddressFamily == family).ToArray();
        return family == AddressFamily.InterNetworkV6 ? addresses.FirstOrDefault(value => !value.IsIPv6LinkLocal) ?? addresses.FirstOrDefault() : addresses.FirstOrDefault();
    }
    public IPAddress? GetSubnetMask(NetworkAdapterSnapshot adapter) => GetNetworkPrefix(adapter, AddressFamily.InterNetwork)?.SubnetMask;
    public IPAddress? GetDefaultGateway(NetworkAdapterSnapshot adapter, AddressFamily family)
    { Check(adapter, family); return adapter.Gateways.FirstOrDefault(value => value.AddressFamily == family); }
    public IReadOnlyList<IPAddress> GetDNSServers(NetworkAdapterSnapshot adapter, AddressFamily family)
    { Check(adapter, family); return Array.AsReadOnly(adapter.DnsServers.Where(value => value.AddressFamily == family).ToArray()); }
    public string? GetMACAddress(NetworkAdapterSnapshot adapter) => (adapter ?? throw new ArgumentNullException(nameof(adapter))).MACAddress;
    public IPNetwork? GetNetworkPrefix(NetworkAdapterSnapshot adapter, AddressFamily family)
    { Check(adapter, family); return adapter.Addresses.FirstOrDefault(value => value.Address.AddressFamily == family && value.PrefixLength.HasValue)?.Network; }
    public string? GetNetworkPrefixCIDR(NetworkAdapterSnapshot adapter, AddressFamily family) => GetNetworkPrefix(adapter, family)?.ToString();
    public IPAddress? GetBroadcastAddress(NetworkAdapterSnapshot adapter) => GetNetworkPrefix(adapter, AddressFamily.InterNetwork)?.LastAddress;
    public IPAddress? GetMulticastAddress(NetworkAdapterSnapshot adapter)
    { var address = GetIPAddress(adapter, AddressFamily.InterNetworkV6); return address == null ? null : GetMulticastAddress(address); }

    public IPNetwork GetNetworkPrefix(IPAddress address, int prefixLength) => new IPNetwork(address, prefixLength);
    public IPNetwork GetNetworkPrefix(IPAddress address, IPAddress subnetMask) => IPNetwork.FromSubnetMask(address, subnetMask);
    /// <remarks>Returns the upper IPv4 range endpoint. /31 and /32 do not imply a usable broadcast destination.</remarks>
    public IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));
        if (address.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Broadcast calculation requires IPv4.", nameof(address));
        return IPNetwork.FromSubnetMask(address, subnetMask).LastAddress;
    }
    public IPAddress GetMulticastAddress(IPAddress address) => IPAddressParser.GetSolicitedNodeMulticastAddress(address);

    private static void Check(NetworkAdapterSnapshot adapter, AddressFamily family)
    { if (adapter == null) throw new ArgumentNullException(nameof(adapter)); CheckFamily(family); }
    internal static void CheckFamily(AddressFamily family)
    { if (family != AddressFamily.InterNetwork && family != AddressFamily.InterNetworkV6) throw new ArgumentOutOfRangeException(nameof(family)); }
}
