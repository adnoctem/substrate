using System.Management.Automation;
using AdNoctem.Substrate.Networking.Compatibility;

namespace AdNoctem.Substrate.PowerShell.Networking;

[Cmdlet(VerbsDiagnostic.Test, "IPv4Address")]
public sealed class TestIPv4AddressCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)] public string Address { get; set; } = "";
    protected override void ProcessRecord() => WriteObject(LegacyAddressValidator.IsIPv4(Address));
}

[Cmdlet(VerbsDiagnostic.Test, "IPv6Address")]
public sealed class TestIPv6AddressCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)] public string Address { get; set; } = "";
    protected override void ProcessRecord() => WriteObject(LegacyAddressValidator.IsIPv6(Address));
}
