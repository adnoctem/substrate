using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PSFoundation.Networking.Tests;

public sealed class NetworkProbeTests
{
    [Fact]
    public async Task TcpProbeResolvesAHostnameAndCanReachItsIpv4Listener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accept = listener.AcceptTcpClientAsync();
            var result = await new NetworkProbeService().ProbeTcpAsync("localhost", port, TimeSpan.FromSeconds(5));
            Assert.True(result.IsConnected);
            Assert.Equal(IPAddress.Loopback, result.Address);
            Assert.Null(result.Error);
            Assert.Equal(port, result.Port);
            using (var connection = await NetworkWait.Complete(accept, TimeSpan.FromSeconds(5), CancellationToken.None))
            {
                var buffer = new byte[1];
                Assert.Equal(0, await NetworkWait.Complete(connection.GetStream().ReadAsync(buffer, 0, 1), TimeSpan.FromSeconds(5), CancellationToken.None));
            }
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void ResultCopiesMutableIpv6ScopeIdentifiers()
    {
        var address = new IPAddress(IPAddress.IPv6Loopback.GetAddressBytes(), 42);
        var result = new TcpProbeResult("synthetic", 80, NetworkProbeStatus.Reachable, address, TimeSpan.Zero, null);
        address.ScopeId = 1;
        Assert.Equal(42, result.Address!.ScopeId);
        result.Address.ScopeId = 2;
        Assert.Equal(42, result.Address.ScopeId);
    }

    [Fact]
    public async Task DnsResolvesLoopbackAndHonorsPreCancellation()
    {
        var manager = new NetworkProbeService();
        Assert.Contains(IPAddress.Loopback, await manager.ResolveHostAsync("127.0.0.1", TimeSpan.FromSeconds(5)));
        Assert.NotEmpty(manager.ResolveHost("localhost", TimeSpan.FromSeconds(5)));
        var cancelled = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.ResolveHostAsync("localhost", TimeSpan.FromSeconds(5), cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.ProbeTcpAsync("127.0.0.1", 1, TimeSpan.FromSeconds(5), cancelled));
    }

    [Fact]
    public async Task DeadlineAndCancellationAreDistinctAndLateFailuresCanComplete()
    {
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Assert.ThrowsAsync<TimeoutException>(() => NetworkWait.Complete(pending.Task, TimeSpan.FromMilliseconds(20), CancellationToken.None));
        pending.SetException(new SocketException((int)SocketError.HostNotFound));
        using (var stopping = new CancellationTokenSource())
        {
            var never = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var wait = NetworkWait.Complete(never.Task, TimeSpan.FromSeconds(30), stopping.Token);
            stopping.Cancel();
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
            Assert.Equal(stopping.Token, error.CancellationToken);
            never.SetResult(1);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task InvalidPortsAreRejectedBeforeNetworking(int port)
        => await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new NetworkProbeService().ProbeTcpAsync("127.0.0.1", port, TimeSpan.FromSeconds(5)));

    [Fact]
    public async Task InvalidHostsAndUnboundedTimeoutsAreRejected()
    {
        var manager = new NetworkProbeService();
        await Assert.ThrowsAsync<ArgumentException>(() => manager.ResolveHostAsync(" ", TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.ProbeTcpAsync("x\0y", 80, TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.ResolveHostAsync("localhost", TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.ProbeTcpAsync("localhost", 80, TimeSpan.MaxValue));
    }
}
