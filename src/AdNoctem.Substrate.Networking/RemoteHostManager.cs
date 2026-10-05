using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using AdNoctem.Substrate.Interop;

namespace AdNoctem.Substrate.Networking;

public enum RemoteChannel { Dns, Icmp, WsMan, RemoteRegistry, Rpc, Rdp, CredSsp }
/// <summary>Evidence from one reachability mechanism. Successful transport access does not prove application health.</summary>
public sealed class RemoteChannelResult
{
    public RemoteChannel Channel { get; }
    public bool Reachable { get; }
    public string Detail { get; }
    public long? LatencyMilliseconds { get; }
    public string? ServiceState { get; }
    internal RemoteChannelResult(RemoteChannel channel, bool reachable, string detail, long? latency = null, string? state = null)
    { Channel = channel; Reachable = reachable; Detail = detail; LatencyMilliseconds = latency; ServiceState = state; }
}
/// <summary>Independent channel observations for a remote host, with no single implied availability verdict.</summary>
public sealed class RemoteHostReport
{
    public string ComputerName { get; }
    public IReadOnlyList<RemoteChannelResult> Channels { get; }
    internal RemoteHostReport(string computer, List<RemoteChannelResult> channels) { ComputerName = computer; Channels = channels.AsReadOnly(); }
}

/// <summary>Independent reachability evidence. Optional authentication is explicit; a successful port probe never proves application health.</summary>
public sealed class RemoteHostManager
{
    /// <summary>Collects DNS, ICMP, WSMan, registry-service and RPC evidence, with explicit optional RDP and CredSSP probes.</summary>
    /// <remarks>The timeout applies to individual probes; total duration can exceed it. Supplying CredSSP credentials explicitly authorizes that authentication attempt.</remarks>
    public RemoteHostReport Probe(string computerName, TimeSpan timeout, bool includeRdp = false, NetworkCredential? credSspCredential = null, CancellationToken cancellationToken = default)
        => ProbeAsync(computerName, timeout, includeRdp, credSspCredential, cancellationToken).GetAwaiter().GetResult();
    /// <inheritdoc cref="Probe"/>
    public async Task<RemoteHostReport> ProbeAsync(string computerName, TimeSpan timeout, bool includeRdp = false, NetworkCredential? credSspCredential = null, CancellationToken cancellationToken = default)
    {
        NetworkWait.CheckTimeout(timeout);
        if (string.IsNullOrWhiteSpace(computerName) || computerName.IndexOf('\0') >= 0)
            throw new ArgumentException("A computer name is required.", nameof(computerName));
        var results = new List<RemoteChannelResult>();
        var network = new NetworkProbeService();
        var watch = Stopwatch.StartNew();
        try
        {
            var addresses = await network.ResolveHostAsync(computerName, timeout, cancellationToken).ConfigureAwait(false);
            results.Add(new RemoteChannelResult(RemoteChannel.Dns, addresses.Count != 0, addresses.Count == 0 ? "DNS lookup failed or timed out" : "Resolved to " + addresses[0] + " (+" + (addresses.Count - 1) + " more)", watch.ElapsedMilliseconds));
        }
        catch (Exception error) when (!(error is OperationCanceledException)) { results.Add(new RemoteChannelResult(RemoteChannel.Dns, false, "DNS lookup failed: " + error.Message, watch.ElapsedMilliseconds)); }
        watch.Restart();
        using (var ping = new Ping())
        {
            try
            {
                var reply = await NetworkWait.Complete(ping.SendPingAsync(computerName, checked((int)timeout.TotalMilliseconds)), timeout, cancellationToken).ConfigureAwait(false);
                results.Add(new RemoteChannelResult(RemoteChannel.Icmp, reply.Status == IPStatus.Success, reply.Status == IPStatus.Success ? "ICMP reply received" : "ICMP timed out or unreachable", reply.Status == IPStatus.Success ? reply.RoundtripTime : watch.ElapsedMilliseconds));
            }
            catch (Exception error) when (!(error is OperationCanceledException)) { results.Add(new RemoteChannelResult(RemoteChannel.Icmp, false, "ICMP failed: " + error.Message, watch.ElapsedMilliseconds)); }
        }
        results.Add(await Task.Run(() => WsMan(computerName, timeout, null, cancellationToken), cancellationToken).ConfigureAwait(false));
        results.Add(await Task.Run(() => RegistryService(computerName, timeout, cancellationToken), cancellationToken).ConfigureAwait(false));
        await Port(RemoteChannel.Rpc, 135).ConfigureAwait(false);
        if (includeRdp)
            await Port(RemoteChannel.Rdp, 3389).ConfigureAwait(false);
        if (credSspCredential != null)
            results.Add(await Task.Run(() => WsMan(computerName, timeout, credSspCredential, cancellationToken), cancellationToken).ConfigureAwait(false));
        return new RemoteHostReport(computerName, results);

        async Task Port(RemoteChannel channel, int port)
        {
            var result = await network.ProbeTcpAsync(computerName, port, timeout, cancellationToken).ConfigureAwait(false);
            results.Add(new RemoteChannelResult(channel, result.IsConnected, result.IsConnected ? "TCP port " + port + " is reachable" : result.Status == NetworkProbeStatus.TimedOut ? "TCP connection timed out" : "TCP connection failed: " + result.Error?.Message, (long)result.Duration.TotalMilliseconds));
        }
    }
    private static RemoteChannelResult WsMan(string computer, TimeSpan timeout, NetworkCredential? credential, CancellationToken cancellation)
    {
        var channel = credential == null ? RemoteChannel.WsMan : RemoteChannel.CredSsp;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            using (var automation = AutomationObject.Create("WSMan.Automation"))
            using (var options = automation.CallObject("CreateConnectionOptions"))
            {
                var flags = Convert.ToInt32(automation.Call(credential == null ? "SessionFlagUseNoAuthentication" : "SessionFlagUseCredSsp"));
                if (credential != null)
                {
                    options.Set("UserName", string.IsNullOrEmpty(credential.Domain) ? credential.UserName : credential.Domain + "\\" + credential.UserName);
                    options.Set("Password", credential.Password);
                    flags |= Convert.ToInt32(automation.Call("SessionFlagCredUsernamePassword"));
                }
                using (var session = automation.CallObject("CreateSession", computer, flags, options.Value))
                {
                    session.Set("Timeout", checked((int)timeout.TotalMilliseconds));
                    var identity = session.Call("Identify", 0) as string;
                    cancellation.ThrowIfCancellationRequested();
                    return new RemoteChannelResult(channel, !string.IsNullOrEmpty(identity), credential == null ? "WSMan identity query succeeded" : "CredSSP authentication succeeded");
                }
            }
        }
        catch (Exception error) when (!(error is OperationCanceledException)) { return new RemoteChannelResult(channel, false, (credential == null ? "WSMan failed: " : "CredSSP failed: ") + error.Message); }
    }
    private static RemoteChannelResult RegistryService(string computer, TimeSpan timeout, CancellationToken cancellation)
    {
        try
        {
            using (var session = CimSession.Create(computer))
            using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellation })
                foreach (var instance in session.QueryInstances(@"root\cimv2", "WQL", "SELECT State FROM Win32_Service WHERE Name='RemoteRegistry'", options))
                    using (instance)
                    {
                        var state = instance.CimInstanceProperties["State"]?.Value as string;
                        return new RemoteChannelResult(RemoteChannel.RemoteRegistry, true, "RemoteRegistry service state: " + state, state: state);
                    }
            return new RemoteChannelResult(RemoteChannel.RemoteRegistry, false, "RemoteRegistry service not found on target");
        }
        catch (Exception error) when (!(error is OperationCanceledException)) { return new RemoteChannelResult(RemoteChannel.RemoteRegistry, false, "CIM probe failed: " + error.Message); }
    }
}
