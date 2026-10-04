# v2 compatibility ledger

The pinned v1 contract remains the oracle. Implementation allowances must be narrow, linked to evidence, and accounted for before release.

## Exercised implementation allowances

- Functions become cmdlets. Registry and foundation fresh-host tests verify one exported owner, binding, output and help.
- The script binder's implementation attribute and C# nullable annotations are ignored, while parameter behavior and validation remain
  compared. `Add-OperationResult.Results` uses an internal conversion attribute to preserve collection identity; its behavior is compared
  against v1 with empty, populated and fixed-size collections.
- Error identifiers are compared before the implementing-command context suffix. Exception type, message, category, target and termination
  remain compared. Full suffix matching by consumers remains a release review item.
- `Test-IPv4Address` and `Test-IPv6Address` can report inactive `ConfirmImpact=Medium` as script functions versus `None` as compiled
  cmdlets. Only these names and values are normalized, and only with `SupportsShouldProcess=false`.

The policy codec also characterizes an existing globalization quirk, preserved only at the PowerShell boundary. See the
[policy codec checkpoint](policy-codec.md); the reusable C# parser validates terminators ordinally.

## Decision needed: basic function binding

The frozen metadata identifies 12 public functions without PowerShell common parameters:

- `Get-BroadcastAddress`
- `Get-DefaultGateway`
- `Get-DefaultNetworkAdapter`
- `Get-DNSServer`
- `Get-IPAddress`
- `Get-MACAddress`
- `Get-MulticastAddress`
- `Get-NetworkPrefix`
- `Get-NetworkPrefixCIDR`
- `Get-SubnetMask`
- `Get-UserInfo`
- `Show-Color`

`v2.md` asks for both compiled public cmdlets and preservation of binding. For these functions, those requirements conflict: compiled
cmdlets automatically expose common parameters and reject unknown parameters; basic functions accept unused arguments. Pipeline binding also
needs characterization before cutover. The two networking alias pairs resolve to functions in this list.

Two concrete resolutions are available:

1. Preserve exact public binding with minimal PowerShell parameter-forwarding wrappers, and keep all substantive implementation in C#. This
   relaxes the requirement that public commands be compiled for these 12 commands only.
2. Expose standard compiled cmdlets, retaining documented named/positional arguments, outputs and domain behavior. This deliberately changes
   common-parameter and unknown-argument handling, and requires an approved compatibility exception plus explicit tests and release notes.

No resolution has been applied. The 12 functions remain legacy implementations in the staged package while independent domains continue. The
source evidence is `tests/fixtures/Api/core.json` and `desktop.json`, captured from `d2d1498275806684b44169504146302d54b7a084`. This is a
public binding decision, not a restriction on designing reusable C# networking or user services.
