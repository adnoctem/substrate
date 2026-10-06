using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Networking.Platform;
using Microsoft.Management.Infrastructure;
using Xunit;

namespace AdNoctem.Substrate.Networking.Tests;

public sealed class NetworkManagerTests
{
    [Fact]
    public void DefaultRouteSelectionUsesTheRequestedFamilyAndIgnoresZeroNextHops()
    {
        CimInstance Route(string prefix, string nextHop, uint index, uint metric)
        {
            var row = new CimInstance("MSFT_NetRoute");
            row.CimInstanceProperties.Add(
                CimProperty.Create("DestinationPrefix", prefix, CimFlags.None)
            );
            row.CimInstanceProperties.Add(CimProperty.Create("NextHop", nextHop, CimFlags.None));
            row.CimInstanceProperties.Add(
                CimProperty.Create("InterfaceIndex", index, CimFlags.None)
            );
            row.CimInstanceProperties.Add(CimProperty.Create("RouteMetric", metric, CimFlags.None));

            return row;
        }

        using (var data = new CimNetworkReader())
        {
            Assert.Null(data.SelectRoute(AddressFamily.InterNetwork));
            data.Routes.Add(Route("0.0.0.0/0", "0.0.0.0", 1, 0));
            data.Routes.Add(Route("0.0.0.0/0", "192.0.2.1", 2, 50));
            data.Routes.Add(Route("0.0.0.0/0", "198.51.100.1", 3, 10));
            data.Routes.Add(Route("::/0", "fe80::1", 4, 1));
            Assert.Equal(
                3u,
                CimNetworkReader.Number(
                    data.SelectRoute(AddressFamily.InterNetwork)!,
                    "InterfaceIndex"
                )
            );
            Assert.Equal(
                4u,
                CimNetworkReader.Number(
                    data.SelectRoute(AddressFamily.InterNetworkV6)!,
                    "InterfaceIndex"
                )
            );
        }
    }

    [Fact]
    public void ComputesAddressesFromAnImmutableDualStackSnapshot()
    {
        var scoped = IPAddress.Parse("fe80::1234%7");
        var input = new[]
        {
            new NetworkInterfaceAddress(IPAddress.Parse("192.0.2.129"), 25),
            new NetworkInterfaceAddress(scoped, 64),
            new NetworkInterfaceAddress(IPAddress.Parse("2001:db8::1234"), 64),
        };
        var dns = IPAddress.Parse("fe80::53%7");
        var adapter = new NetworkAdapterSnapshot(
            "Synthetic",
            7,
            NetworkAdapterKind.Ethernet,
            input,
            new[] { IPAddress.Parse("192.0.2.254") },
            new[] { dns },
            "00-11-22-33-44-55"
        );
        input[0] = new NetworkInterfaceAddress(IPAddress.Loopback);
        scoped.ScopeId = 8;
        dns.ScopeId = 8;
        var manager = new NetworkManager();
        Assert.Equal(
            "192.0.2.129",
            manager.GetIPAddress(adapter, AddressFamily.InterNetwork)!.ToString()
        );
        Assert.Equal(
            "2001:db8::1234",
            manager.GetIPAddress(adapter, AddressFamily.InterNetworkV6)!.ToString()
        );
        Assert.Equal("255.255.255.128", manager.GetSubnetMask(adapter)!.ToString());
        Assert.Equal(
            "192.0.2.128/25",
            manager.GetNetworkPrefixCIDR(adapter, AddressFamily.InterNetwork)
        );
        Assert.Equal("192.0.2.255", manager.GetBroadcastAddress(adapter)!.ToString());
        Assert.Equal("ff02::1:ff00:1234", manager.GetMulticastAddress(adapter)!.ToString());
        Assert.Equal(
            "192.0.2.254",
            manager.GetDefaultGateway(adapter, AddressFamily.InterNetwork)!.ToString()
        );
        Assert.Equal("00-11-22-33-44-55", manager.GetMACAddress(adapter));
        Assert.Equal(7, manager.GetDNSServers(adapter, AddressFamily.InterNetworkV6)[0].ScopeId);
        adapter.DnsServers[0].ScopeId = 9;
        Assert.Equal(7, adapter.DnsServers[0].ScopeId);
        Assert.Equal(7, adapter.Addresses[1].Address.ScopeId);
    }

    [Fact]
    public void MissingConfigurationIsDistinctFromInvalidInput()
    {
        var manager = new NetworkManager();
        var adapter = new NetworkAdapterSnapshot(
            "Empty",
            1,
            NetworkAdapterKind.Unspecified,
            Array.Empty<NetworkInterfaceAddress>()
        );
        Assert.Null(manager.GetIPAddress(adapter, AddressFamily.InterNetwork));
        Assert.Null(manager.GetSubnetMask(adapter));
        Assert.Empty(manager.GetDNSServers(adapter, AddressFamily.InterNetwork));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.GetIPAddress(adapter, AddressFamily.Unspecified)
        );
        Assert.Throws<ArgumentException>(() =>
            manager.GetNetworkPrefix(IPAddress.Loopback, IPAddress.Parse("255.0.255.0"))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetworkInterfaceAddress(IPAddress.Loopback, 33)
        );
        Assert.Equal(
            "ff02::1:ff00:1%4",
            manager.GetMulticastAddress(IPAddress.Parse("fe80::1%4")).ToString()
        );
        Assert.Equal(
            "2001:db8:0:0:8000::/65",
            manager
                .GetNetworkPrefix(IPAddress.Parse("2001:db8::ffff:abcd:1234:ffff"), 65)
                .ToString()
        );
        Assert.True(NetworkValidator.TestSubnetMask("255.255.255.128"));
        Assert.False(NetworkValidator.TestSubnetMask("255.0.255.0"));
        Assert.True(NetworkValidator.TestNetworkPrefix("2001:db8::/65"));
        Assert.False(NetworkValidator.TestNetworkPrefix("192.0.2.0/33"));
        Assert.True(NetworkValidator.TestMACAddress("00:11:22:33:44:FF"));
        Assert.False(NetworkValidator.TestMACAddress("00:11:22:33:44:GG"));
    }

    [Theory]
    [InlineData("192.0.2.1", true, false)]
    [InlineData("192.000.2.1", false, false)]
    [InlineData("::ffff:192.0.2.1", false, true)]
    [InlineData("1:::2", false, false)]
    [InlineData(null, false, false)]
    public void ValidatorsUseStrictLibraryRules(string? address, bool ipv4, bool ipv6)
    {
        Assert.Equal(ipv4, NetworkValidator.TestIPv4Address(address));
        Assert.Equal(ipv6, NetworkValidator.TestIPv6Address(address));
    }

    [Fact]
    public async Task ReadsLocalAdaptersWithoutPowerShellAndHonorsPreCancellation()
    {
        var manager = new NetworkManager();
        var adapters = await manager.GetAdaptersAsync(TimeSpan.FromSeconds(10));
        Assert.All(adapters, adapter => Assert.NotNull(adapter.Name));
        var selected = manager.GetDefaultNetworkAdapter(
            AddressFamily.InterNetwork,
            TimeSpan.FromSeconds(10)
        );

        if (selected != null)
            Assert.Contains(adapters, adapter => adapter.InterfaceIndex == selected.InterfaceIndex);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            manager.GetAdaptersAsync(TimeSpan.FromSeconds(10), new CancellationToken(true))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.GetAdapters(TimeSpan.Zero));
    }
}
