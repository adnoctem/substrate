using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Networking;

public enum NetworkProbeStatus
{
    Reachable,
    TimedOut,
    Failed,
}

/// <summary>Evidence of a TCP connection attempt. A reachable port does not establish application identity, health or authorization.</summary>
public sealed class TcpProbeResult
{
    private readonly IPAddress? address;
    public string Host { get; }
    public int Port { get; }
    public NetworkProbeStatus Status { get; }
    public bool IsConnected => Status == NetworkProbeStatus.Reachable;
    public IPAddress? Address => address == null ? null : Copy(address);
    public TimeSpan Duration { get; }
    public SocketError? SocketError => Error?.SocketErrorCode;

    /// <summary>The last native socket failure for an unsuccessful probe, if any. Successful probes have no error.</summary>
    public SocketException? Error { get; }

    internal TcpProbeResult(
        string host,
        int port,
        NetworkProbeStatus status,
        IPAddress? address,
        TimeSpan duration,
        SocketException? error
    )
    {
        Host = host;
        Port = port;
        Status = status;
        this.address = address == null ? null : Copy(address);
        Duration = duration;
        Error = error;
    }

    private static IPAddress Copy(IPAddress value) =>
        value.AddressFamily == AddressFamily.InterNetworkV6
            ? new IPAddress(value.GetAddressBytes(), value.ScopeId)
            : new IPAddress(value.GetAddressBytes());
}

/// <summary>Bounded DNS resolution and TCP connection observations with explicit cancellation.</summary>
public sealed class NetworkProbeService
{
    public IReadOnlyList<IPAddress> ResolveHost(
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => ResolveHostAsync(host, timeout, cancellationToken).GetAwaiter().GetResult();

    /// <summary>Resolves a host with a bounded caller wait. Missing names preserve their native SocketException; timeout is distinct.</summary>
    /// <remarks>The legacy runtime DNS operation cannot be interrupted. Cancellation/timeout ends the caller's wait;
    /// a pending resolver completes in the background and its failure is observed. No connection is started after cancellation.</remarks>
    public async Task<IReadOnlyList<IPAddress>> ResolveHostAsync(
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        CheckHost(host);
        NetworkWait.CheckTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var addresses = await NetworkWait
            .Complete(Dns.GetHostAddressesAsync(host), timeout, cancellationToken)
            .ConfigureAwait(false);

        return Array.AsReadOnly(addresses);
    }

    public TcpProbeResult ProbeTcp(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => ProbeTcpAsync(host, port, timeout, cancellationToken).GetAwaiter().GetResult();

    /// <summary>Resolves and attempts the returned IP addresses in order under one shared deadline, then closes its socket.
    /// Cancellation throws; refusal, resolution failure and timeout are typed results. No data or credentials are sent.</summary>
    public async Task<TcpProbeResult> ProbeTcpAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        CheckHost(host);

        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        NetworkWait.CheckTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        SocketException? lastError = null;
        IPAddress? lastAddress = null;
        TcpProbeResult Result(NetworkProbeStatus status) =>
            new TcpProbeResult(host, port, status, lastAddress, clock.Elapsed, lastError);

        try
        {
            var addresses = IPAddress.TryParse(host, out var literal)
                ? new[] { literal }
                : await ResolveHostAsync(host, Remaining(), cancellationToken)
                    .ConfigureAwait(false);

            foreach (
                var address in addresses.Where(value =>
                    value.AddressFamily == AddressFamily.InterNetwork
                    || value.AddressFamily == AddressFamily.InterNetworkV6
                )
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = Remaining();
                lastAddress = address;

                // Resolve before creating the socket, so a late DNS completion cannot initiate another connection after disposal.
                using (var client = new TcpClient(address.AddressFamily))
                {
                    try
                    {
                        await NetworkWait
                            .Complete(
                                client.ConnectAsync(address, port),
                                remaining,
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                        lastError = null;

                        return Result(NetworkProbeStatus.Reachable);
                    }
                    catch (SocketException error)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lastError = error;
                    }
                }
            }

            lastError = lastError ?? new SocketException((int)SocketError.HostNotFound);

            return Result(NetworkProbeStatus.Failed);
        }
        catch (TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Result(NetworkProbeStatus.TimedOut);
        }
        catch (SocketException error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastError = error;

            return Result(NetworkProbeStatus.Failed);
        }

        TimeSpan Remaining()
        {
            var remaining = timeout - clock.Elapsed;

            if (remaining < TimeSpan.FromMilliseconds(1))
                throw new TimeoutException("The network operation exceeded its deadline.");

            return remaining;
        }
    }

    private static void CheckHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.IndexOf('\0') >= 0)
            throw new ArgumentException("A host name or IP address is required.", nameof(host));
    }
}

internal static class NetworkWait
{
    internal static void CheckTimeout(TimeSpan timeout)
    {
        if (timeout.TotalMilliseconds < 1 || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Use a finite timeout of at least one millisecond."
            );
    }

    internal static async Task Complete(
        Task operation,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        // Observe faults even if the caller stops waiting. Use the default scheduler and do not capture the socket.
        _ = operation.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var delay = Task.Delay(timeout, deadline.Token);
            var completed = await Task.WhenAny(operation, delay).ConfigureAwait(false);
            deadline.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            if (completed != operation)
                throw new TimeoutException("The network operation exceeded its deadline.");

            await operation.ConfigureAwait(false);
        }
    }

    internal static async Task<T> Complete<T>(
        Task<T> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        await Complete((Task)operation, timeout, cancellationToken).ConfigureAwait(false);

        return await operation.ConfigureAwait(false);
    }
}
