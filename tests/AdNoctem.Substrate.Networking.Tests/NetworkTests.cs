using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AdNoctem.Substrate.Networking.Tests;

public sealed class NetworkTests
{
    [Theory]
    [InlineData("192.0.2.129/24", "192.0.2.0", "192.0.2.255", "255.255.255.0")]
    [InlineData("192.0.2.1/0", "0.0.0.0", "255.255.255.255", "0.0.0.0")]
    [InlineData("192.0.2.1/32", "192.0.2.1", "192.0.2.1", "255.255.255.255")]
    [InlineData("2001:db8:ffff::1/33", "2001:db8:8000::", "2001:db8:ffff:ffff:ffff:ffff:ffff:ffff", "ffff:ffff:8000::")]
    [InlineData("2001:db8::1/128", "2001:db8::1", "2001:db8::1", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
    public void ComputesNetworkEndpointsAndMasks(string cidr, string first, string last, string mask)
    {
        var network = IPNetwork.Parse(cidr);
        Assert.Equal(first, network.NetworkAddress.ToString());
        Assert.Equal(last, network.LastAddress.ToString());
        Assert.Equal(mask, network.SubnetMask.ToString());
        Assert.True(network.Contains(IPAddress.Parse(first)));
        Assert.True(network.Contains(IPAddress.Parse(last)));
    }

    [Theory]
    [InlineData("192.0.2.1/33")]
    [InlineData("192.0.2.1/-1")]
    [InlineData("192.0.2.1")]
    [InlineData("127.1/8")]
    [InlineData("010.0.0.1/8")]
    [InlineData("fe80::1%4/64")]
    [InlineData("2001:db8::1/129")]
    public void RejectsInvalidNetworks(string value) => Assert.False(IPNetwork.TryParse(value, out _));

    [Fact]
    public void DetectsOverlapWithoutCrossingAddressFamilies()
    {
        var network = IPNetwork.Parse("192.0.2.0/24");
        Assert.True(network.Contains(IPNetwork.Parse("192.0.2.128/25")));
        Assert.True(network.Overlaps(IPNetwork.Parse("192.0.0.0/16")));
        Assert.False(network.Overlaps(IPNetwork.Parse("192.0.3.0/24")));
        Assert.False(network.Contains(IPAddress.IPv6Loopback));
        Assert.Equal(network, IPNetwork.FromSubnetMask(IPAddress.Parse("192.0.2.5"), IPAddress.Parse("255.255.255.0")));
        Assert.Throws<ArgumentException>(() => IPNetwork.FromSubnetMask(IPAddress.Parse("192.0.2.5"), IPAddress.Parse("255.0.255.0")));
    }

    [Fact]
    public void MutableAddressInstancesCannotAlterNetworks()
    {
        var input = IPAddress.Parse("2001:db8::1");
        var network = new IPNetwork(input, 64);
        input.ScopeId = 3;
        var copy = network.NetworkAddress;
        copy.ScopeId = 4;
        Assert.Equal("2001:db8::/64", network.ToString());
        Assert.Equal("ff02::1:ff12:3456%3", IPAddressParser.GetSolicitedNodeMulticastAddress(IPAddress.Parse("fe80::12:3456%3")).ToString());
    }

    [Theory]
    [InlineData("+1.2.3.4")]
    [InlineData(" 1.2.3.4")]
    [InlineData("2001:::1")]
    [InlineData("[::1]")]
    [InlineData("::ffff:192.000.2.1")]
    public void PublicParserDoesNotInheritScriptValidationQuirks(string address) => Assert.False(IPAddressParser.TryParse(address, out _));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TcpProbeConnectsToExplicitLoopbackListenerAndCleansUp(bool ipv6)
    {
        var address = ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
        var listener = new TcpListener(address, 0);
        listener.Start();
        try
        {
            var accept = listener.AcceptTcpClientAsync();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var result = await new NetworkProbeService().ProbeTcpAsync(address.ToString(), port, TimeSpan.FromSeconds(5));
            Assert.Equal(NetworkProbeStatus.Reachable, result.Status);
            using (var accepted = await NetworkWait.Complete(accept, TimeSpan.FromSeconds(5), CancellationToken.None))
            {
                Assert.Equal(NetworkProbeStatus.Reachable, result.Status);
                Assert.Null(result.SocketError);
                var read = accepted.GetStream().ReadAsync(new byte[1], 0, 1);
                Assert.Same(read, await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5))));
                Assert.Equal(0, await read);
            }
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task RefusedConnectionsRetainTheNativeSocketFailure()
    {
        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)socket.LocalEndPoint!).Port;
            var result = await new NetworkProbeService().ProbeTcpAsync("127.0.0.1", port, TimeSpan.FromSeconds(5));
            Assert.Equal(NetworkProbeStatus.Failed, result.Status);
            Assert.Equal(SocketError.ConnectionRefused, result.SocketError);
        }
    }

    [Fact]
    public async Task CancellationIsDistinctFromUnreachability()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NetworkProbeService().ProbeTcpAsync("127.0.0.1", 1, TimeSpan.FromSeconds(1), new CancellationToken(true)));
    }
}
