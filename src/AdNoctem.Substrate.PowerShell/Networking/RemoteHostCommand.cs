using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Networking;
using AdNoctem.Substrate.PowerShell.Infrastructure;
using AdNoctem.Substrate.PowerShell.Windows;

namespace AdNoctem.Substrate.PowerShell.Networking;

[Cmdlet(VerbsDiagnostic.Test, "RemoteHostReachability"), OutputType(typeof(PSObject))]
public sealed class TestRemoteHostReachabilityCommand : SystemCommand
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0), Alias("Computer", "Host", "Server")]
    public string[] ComputerName { get; set; } = Array.Empty<string>();
    [Parameter, Credential] public PSCredential? Credential { get; set; }
    [Parameter] public SwitchParameter IncludeRdp { get; set; }
    [Parameter] public SwitchParameter CredSSP { get; set; }
    [Parameter, ValidateRange(1, 120)] public int TimeoutSeconds { get; set; } = 5;
    protected override void ProcessRecord()
    {
        foreach (var name in ComputerName)
        {
            if (CredSSP && Credential == null)
                WriteWarning("-CredSSP was supplied without -Credential; skipping the CredSSP authentication probe for '" + name + "'.");
            var report = new RemoteHostManager().Probe(name, TimeSpan.FromSeconds(TimeoutSeconds), IncludeRdp, CredSSP ? Credential?.GetNetworkCredential() : null, Cancellation);
            bool Reachable(RemoteChannel channel) => report.Channels.Any(item => item.Channel == channel && item.Reachable);
            var output = SystemOutput.Object("ComputerName", name, "DNSResolved", Reachable(RemoteChannel.Dns), "ICMP", Reachable(RemoteChannel.Icmp), "WSMAN", Reachable(RemoteChannel.WsMan),
                "RemoteRegistry", Reachable(RemoteChannel.RemoteRegistry), "WMIRPC", Reachable(RemoteChannel.Rpc));
            if (IncludeRdp)
                output.Properties.Add(new PSNoteProperty("RDP", Reachable(RemoteChannel.Rdp)));
            if (CredSSP && Credential != null)
                output.Properties.Add(new PSNoteProperty("CredSSP", Reachable(RemoteChannel.CredSsp)));
            output.Properties.Add(new PSNoteProperty("Channels", report.Channels.Select(item =>
            {
                var source = item.Channel == RemoteChannel.Dns ? "DNS" : item.Channel == RemoteChannel.Icmp ? "ICMP" : item.Channel == RemoteChannel.WsMan ? "WSMan" : item.Channel == RemoteChannel.Rpc ? "WMI/RPC" : item.Channel == RemoteChannel.Rdp ? "RDP" : item.Channel == RemoteChannel.CredSsp ? "CredSSP" : "RemoteRegistry";
                var action = item.Channel == RemoteChannel.Dns ? "Resolve" : item.Channel == RemoteChannel.Icmp ? "Ping" : item.Channel == RemoteChannel.WsMan ? "Query" : item.Channel == RemoteChannel.CredSsp ? "Authenticate" : "Probe";
                var fields = new Dictionary<string, object?> { ["Detail"] = item.Detail };
                if (item.LatencyMilliseconds.HasValue)
                    fields["LatencyMs"] = item.LatencyMilliseconds.Value;
                if (item.Channel == RemoteChannel.RemoteRegistry)
                    fields["ServiceState"] = item.ServiceState;
                return OperationOutput.Create(name, source, action, item.Reachable ? "Reachable" : "Unreachable", fields);
            }).ToArray()));
            WriteObject(output);
        }
    }
}
