# Using the networking library

This guide illustrates common workflows. The generated [API catalog](API.md) links to the complete member reference.

`AdNoctem.Substrate.Networking` provides address validation, network calculations, local Windows adapter inventory and TCP probes for other
applications. It does not load PowerShell, display prompts, choose a log destination or change network configuration.

## Entry points

| Type                            | Purpose                                                                                                                                                                              |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `NetworkManager`                | Read adapters and default routes; obtain IP addresses, subnet masks, gateways, DNS servers and MAC addresses; calculate prefixes, broadcasts and solicited-node multicast addresses. |
| `NetworkValidator`              | Check IPv4, IPv6, network-prefix, contiguous subnet-mask and six-byte MAC-address text without network access.                                                                       |
| `NetworkAdapterSnapshot`        | An immutable adapter description containing addresses with optional prefix lengths, gateways, DNS servers and media kind.                                                            |
| `IPAddressParser` / `IPNetwork` | Parse strict address literals and represent an address range with its prefix length.                                                                                                 |
| `NetworkProbeService`           | Perform an asynchronous TCP connection probe with an explicit timeout and cancellation token.                                                                                        |

## Read once, calculate from the snapshot

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using AdNoctem.Substrate.Networking;

var manager = new NetworkManager();
var adapter = await manager.GetDefaultNetworkAdapterAsync(
    AddressFamily.InterNetwork, TimeSpan.FromSeconds(10),
    cancellationToken: CancellationToken.None);

if (adapter != null)
{
    IPAddress? address = manager.GetIPAddress(adapter, AddressFamily.InterNetwork);
    IPNetwork? network = manager.GetNetworkPrefix(adapter, AddressFamily.InterNetwork);
    IPAddress? broadcast = manager.GetBroadcastAddress(adapter);
    var dns = manager.GetDNSServers(adapter, AddressFamily.InterNetwork);
}

bool valid = NetworkValidator.TestIPv4Address("192.0.2.129");
var calculated = manager.GetNetworkPrefix(IPAddress.Parse("192.0.2.129"), 25);
// calculated.ToString(): "192.0.2.128/25"
```

The snapshot overloads do no further I/O. Missing values return `null` or an empty collection; invalid arguments and provider failures
throw. Snapshots copy input collections and mutable address data. No native handles escape from the public inventory API.

`GetIPAddress` prefers a non-link-local IPv6 address, falling back to link-local. `GetNetworkPrefix` uses the first address of the requested
family with a known prefix length, which can be a different address. For an exact association, select an entry from `adapter.Addresses` and
use its `Network` property. DNS and gateway order follows the Windows configuration.

Default-adapter selection uses the lowest **route metric** among default routes with a nonzero next hop for the requested address family. It
does not add the interface metric or prove Internet reachability. An optional media-kind filter checks that selected adapter; it does not
silently select a different route. `Unspecified` media does not establish that an adapter is a VPN.

Pure calculations reject noncontiguous subnet masks and correctly support IPv6 prefixes such as `/65`. IPv6 solicited-node multicast
calculation preserves the source scope ID. IPv4 broadcast calculation returns the upper range endpoint; `/31` and `/32` do not imply a
usable broadcast destination. MAC validation accepts six hexadecimal byte pairs separated consistently by colons or hyphens.

## Platform, cancellation and ownership

Local inventory uses Windows CIM directly through
[Microsoft.Management.Infrastructure](https://www.nuget.org/packages/Microsoft.Management.Infrastructure/3.0.0) and
[`CimSession.QueryInstances`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.management.infrastructure.cimsession.queryinstances?view=powershellsdk-7.6.0).
The networking library targets `net48` and `netstandard2.0`; local inventory is Windows-only. Pure address operations do not require CIM
queries. Current runtime validation covers this workstation's .NET Framework 4.8.1 and .NET 10, plus Windows PowerShell 5.1 and
PowerShell 7. Other Windows versions, architectures and runtime-only installations remain part of the v2 release matrix.

Inventory requires a positive timeout of at most `Int32.MaxValue` milliseconds. It applies to each native operation, not to the combined
inventory as a whole. Async inventory offloads synchronous CIM work to the thread pool. Cancellation is forwarded to the provider and
checked while collecting results; an in-flight native operation can only stop when the provider honors that request. Sessions, operation
options and collected native instances are disposed on success or failure.

## PowerShell compatibility

The staged module has one `compat.ps1`. Ten networking getters preserve their existing names, parameters, aliases and unused extra
arguments, then call the C# adapter. Legacy quirks live in internal compatibility classes; the public C# API is free to validate inputs
strictly. The two existing IP validation cmdlets likewise preserve legacy acceptance rules.

`Get-DefaultNetworkAdapter` still returns the existing PowerShell object shape, including detached CIM objects for `NetAdapter` and
`CimConfig`. Callers own these returned native objects; internal calculations dispose copies they create. The wrapper loads the Windows
NetAdapter module for its CIM object type extensions, while C# performs the queries and calculations.

`Show-Color` also lives in `compat.ps1`: C# supplies the palette and PowerShell writes colored host output. It explicitly declares unused
remaining arguments and supports standard PowerShell flags, allowing parameter completion.

`Get-UserInfo` uses the Windows identity API; `Test-RemoteHostReachability` uses the networking remoting API. The reusable TCP probe does
not imply credentials, remoting setup or remote administration. The repository's `docs/migration/compatibility-exceptions.md` records the
exact exceptions and remaining provider-failure validation.
