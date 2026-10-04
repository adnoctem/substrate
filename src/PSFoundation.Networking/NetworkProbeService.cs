using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PSFoundation.Networking;

public enum NetworkProbeStatus { Reachable, TimedOut, Failed }

public sealed class TcpProbeResult
{
    public string Host { get; }
    public int Port { get; }
    public NetworkProbeStatus Status { get; }
    public TimeSpan Duration { get; }
    public SocketError? SocketError { get; }
    internal TcpProbeResult(string host, int port, NetworkProbeStatus status, TimeSpan duration, SocketError? socketError = null)
    { Host = host; Port = port; Status = status; Duration = duration; SocketError = socketError; }
}

/// <summary>Explicit, bounded connectivity checks. A successful TCP connection does not prove application health or authorization.</summary>
public sealed class NetworkProbeService
{
    public TcpProbeResult ProbeTcp(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
        => ProbeTcpAsync(host, port, timeout, cancellationToken).GetAwaiter().GetResult();

    /// <remarks>Disposes the socket on timeout/cancellation. DNS resolution may finish later on older runtimes; its task is observed without retaining the socket.
    /// DNS failures and connection failures retain the native socket error. Cancellation throws instead of being reported as unreachable.</remarks>
    public async Task<TcpProbeResult> ProbeTcpAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("A host is required.", nameof(host));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        var elapsed = Stopwatch.StartNew();
        using (var client = new TcpClient(Socket.OSSupportsIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork))
        using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            Task? connect = null;
            try
            {
                if (Socket.OSSupportsIPv6)
                    client.Client.DualMode = true;
                connect = client.ConnectAsync(host, port);
                var delay = Task.Delay(timeout, delayCancellation.Token);
                if (await Task.WhenAny(connect, delay).ConfigureAwait(false) != connect)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new TcpProbeResult(host, port, NetworkProbeStatus.TimedOut, elapsed.Elapsed);
                }
                delayCancellation.Cancel();
                await connect.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return new TcpProbeResult(host, port, NetworkProbeStatus.Reachable, elapsed.Elapsed);
            }
            catch (SocketException error)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new TcpProbeResult(host, port, NetworkProbeStatus.Failed, elapsed.Elapsed, error.SocketErrorCode);
            }
            finally
            {
                delayCancellation.Cancel();
                client.Dispose();
                if (connect != null)
                    _ = connect.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }
}
