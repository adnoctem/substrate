# v2 PowerShell compatibility notes

The aim is to move the implementation to C# while keeping existing PowerShell scripts working. We compare the new commands with the saved
v1.8.7 implementation to check that. This document explains the places where the rewrite needs special care or a decision from you.

## Accepted decision: handling extra arguments

**Selected: keep small PowerShell functions in one `compat.ps1` and let C# do the work.**

The maintainer selected option 1 below on 2026-10-04. The public C# networking API uses `NetworkManager` for reads and calculations and
`NetworkValidator` for validation. Other applications use these classes directly, without PowerShell. The explanation below records why we
retain a few script functions rather than turning every exported name into a C# cmdlet.

### What does "binding" mean?

It means how PowerShell connects the arguments you type to a command's parameters. For example, when you run
`Get-IPAddress -AddressFamily IPv4`, PowerShell must work out what `-AddressFamily` means and whether the supplied value is allowed.

PowerShell has simple script functions and more strictly checked commands. A command implemented directly in C# is called a **cmdlet** and
uses the stricter rules automatically. You still call it by its normal PowerShell name; callers do not write C#.

These 12 existing functions use the simpler rules. One consequence is that they can accept extra arguments without reporting a mistake.
Their implementations do not use those extra arguments. A C# cmdlet rejects an unknown parameter instead.

### A concrete example

`Show-Color` lists the available console colors. It has no parameter called `-Typo`:

```powershell
Show-Color -Typo
```

Today, that still lists the colors: the extra argument is ignored. If we expose it directly as a C# cmdlet, PowerShell reports that `-Typo`
is not a recognized parameter, and the command does not run. The normal call, `Show-Color`, should continue to work in either design.

C# cmdlets also automatically accept standard PowerShell options such as `-Verbose` and `-ErrorAction`. PowerShell calls these **common
parameters**. Adding support for `-Verbose` does not itself make a command print extra messages; the implementation must provide them.

### The two options

| Choice                           | What we would build                                                                                                             | Effect on existing callers                                                                  |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| **1. Preserve today's behavior** | Keep a small PowerShell function for each of these 12 names. It accepts arguments as before and calls C# to do the actual work. | Existing argument handling stays the same, including ignored extras.                        |
| **2. Use C# cmdlets directly**   | Implement each public command directly in C#, without that small PowerShell function in front.                                  | Unknown parameters become errors, and standard options such as `-Verbose` become available. |

The small function in option 1 is what the earlier document called a **wrapper**. It is an adapter for existing callers, not a second
implementation of the networking or user logic. Both options keep the command names and aim to preserve their documented arguments and
results. We must also test how objects passed through a PowerShell pipeline reach these commands before replacing them.

**Option 1 is accepted.** Its cost is retaining small PowerShell entry points. The networking functions keep their existing parameter
declarations and ignored extra arguments. All address calculations and adapter queries execute in C#.

The original v2 outline asked for both unchanged argument handling and every public command implemented directly in C#. These 12 commands
are where those two wishes conflict. This decision resolves that conflict; no further choice is pending here.

### Which commands are affected?

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

The alternative names (aliases) for the two network-prefix commands would follow the same choice.

**Implemented:** the ten networking getters and `Show-Color` now live in the staged `compat.ps1`. `Get-UserInfo` will join it when the
identity implementation is migrated. Windows PowerShell 5.1 remains supported. The two IP validation commands were already compiled; they
retain legacy acceptance rules while the public `NetworkValidator` uses strict address parsing.

### Show-Color: explicit parameters and completion

`Show-Color` intentionally gains standard PowerShell parameters, including `-Verbose`, `-ErrorAction` and `-InformationAction`. PowerShell
can now complete those names. Its declared `ArgumentList` parameter captures unused remaining arguments, so `Show-Color -Typo` still lists
the palette. Standard parameters now have their usual effect; no color filter is introduced. `-Verbose` alone does not add messages.

C# provides the palette; `compat.ps1` writes it to the host. This keeps console presentation at the PowerShell boundary. Tests explicitly
check the new parameter declarations and completion, rather than requiring `Show-Color` to have v1's empty parameter list.

### Networking: improved C# rules and preserved script behavior

The reusable API validates contiguous subnet masks and handles IPv6 prefixes that do not fall on byte boundaries. Legacy script behavior
remains isolated in internal compatibility code: v1 accepts some invalid masks and can throw on an IPv6 prefix such as `/65`. The wrappers
retain those outcomes for now. Address selection also retains the legacy distinction between the preferred IPv6 address and the first
address with a prefix. Callers of the public C# API can select a specific address from the snapshot and calculate its prefix directly.

Native provider failures remain errors in the new implementation. Successful local reads and synthetic address/error cases are compared
against v1 in both hosts; unavailable or failing Windows network providers still need dedicated integration validation before release.

## Technical notes for implementation and review

The LGPO slice also uses `compat.ps1` for `Invoke-LGPO` and `Test-LGPOInstalled`. This preserves `ValidateScript`, confirmation and
PowerShell provider/empty-path behavior while C# handles filesystem checks and process execution. The application wrapper retains the v1
wait-for-completion policy; stopping a running policy process could leave partial changes. Ordinary C# callers choose their own explicit
timeout and interruption policy.

LGPO process arguments are now quoted independently, so paths containing spaces reach the executable intact. Output text, exit codes,
timestamps, host-specific empty-output values and launch errors are compared against v1. Transient `Get-Content` provider metadata on v1's
output strings is not reproduced: results contain the captured text, without references to deleted temporary log files. The comparison probe
strips those provider annotations before serialization to avoid recursively serializing provider state.

The frozen v1 LGPO source digest is `PLACEHOLDER_REPLACE_ON_FIRST_VENDORING`. Following the maintainer's supplied-package review on
2026-10-04, the C# catalog and staged `Resolve-LGPOSource` now expose the reviewed ZIP hash and updated `LastVerified` date. These two
values intentionally differ from the baseline; the comparison probe asserts their new values explicitly before comparing the remaining
contract. The executable has a separate pin in the C# catalog. The frozen v1 source remains unchanged. `Install-LGPO` and
`Test-LGPOSourceAvailability` now use C#. Installation selects the exact catalog entry and verifies both archive and binary pins before
atomic publication. Availability remains a reachability check only. Transport failure details come from HttpClient rather than
Invoke-WebRequest. The installation command retains existing-file and WhatIf behavior, but fixes v1's unconditional administrator check: an
explicitly chosen writable destination now works without elevation, as its original help promised. Filesystem permissions still apply.

Windows inventory preserves existing object shapes and invariant display strings. `Get-SystemPaths` now rejects `.`/`..` and trailing-dot
product names instead of allowing traversal or ambiguous Windows normalization. The C# path API has the same restriction. The old
install-date conversion accidentally included the local timezone offset at the Unix epoch; that historical result is retained only in
PowerShell output. `WindowsVersionInfo.InstalledAt` contains the correct UTC instant. Native inventory and DNS calls now have bounded
operation timeouts and stop handling. Their failures are not proof that a machine has no disks or no domain name.

These notes record details for the implementation and tests; they are not additional choices you need to make now.

Win32 inventory keeps the existing filters, property shapes and suppressed registry-read failures at the PowerShell boundary. The typed
library requires a string `DisplayName`; malformed names and unsupported registry representations are explicit read failures. The adapter
omits those registrations instead of exposing malformed names or partially read records. C# callers can request partial inventory and
inspect its errors, or use the default fail-on-error behavior. Ordinary optional metadata with an unexpected kind is retained as raw data;
typed projections return null. The original provider paths and view labels remain in PowerShell output, while the library uses explicit
native registry views. Neither inventory path queries `Win32_Product` or triggers installer repair.

The list above comes from the saved command descriptions in `tests/fixtures/Api/core.json` and `desktop.json`, captured from v1.8.7 commit
`d2d1498275806684b44169504146302d54b7a084`. Those descriptions show that the 12 functions do not expose common parameters.

Our comparisons already account for these specific differences between script functions and C# cmdlets:

- PowerShell reports a different command type: `Function` becomes `Cmdlet`. Tests still check that each migrated name refers to exactly one
  command, with the expected parameters, results and help.
- Some internal parameter annotations differ between PowerShell and C#. Tests ignore those implementation details, but still compare
  parameter behavior. In particular, `Add-OperationResult.Results` has a conversion helper that preserves the caller's original collection;
  tests cover empty, populated and fixed-size collections.
- An error identifier can end with the name of the script function or C# class that produced it. Tests compare the identifier before that
  suffix, plus the exception type, message, category, affected object and whether execution stops. Callers that match the entire identifier
  still need review before release.
- `Test-IPv4Address` and `Test-IPv6Address` can report a different confirmation setting in their command descriptions (`Medium` versus
  `None`). Neither command supports confirmation prompts. Tests allow only this specific description difference for these two commands.

The policy-file reader has a separate existing quirk: depending on the runtime's text-comparison rules, v1 can accept a string missing its
required end marker (a NUL character). The PowerShell command retains that behavior for compatibility; the reusable C# parser checks the
marker strictly. The [policy codec checkpoint](policy-codec.md) records the details. Changing the PowerShell behavior would be a separate
future decision.

Win32 execution now supports local EXE and MSI installers explicitly, without shell associations. MSI uses the system `msiexec.exe`;
uninstall parsing converts a leading `/I`, including an attached product code, to `/X`. A missing quiet uninstall string requires `-Force`
before using the interactive fallback. V1 accidentally allowed that fallback with `-Quiet` alone. Native launch failures retain exceptions
but no longer claim `Start-Process` error identities. Results describe the launched process; a bootstrapper that exits before its children
finish needs a product-specific completion check. `-NoWait` reports only successful launch, never installation completion. These paths do
not download or establish trust in caller-selected installers. The library does not elevate, and it does not interrupt running installers by
default. Callers can explicitly select an interrupting process policy, accepting the risk of partial installation state.

Printer and scanner output retains the existing normalized fields, using native CIM and WIA instead of PowerShell providers. The typed API
retains printer fallback errors; the PowerShell boundary reports them through verbose output. Scanner discovery owns a separate STA worker
and releases its COM references before completing. Cancellation cannot interrupt an in-flight WIA driver call. Restart Manager results now
check process start time before resolving a process name, preventing attribution to a reused PID. The old import-time file-lock `Add-Type`
binding is removed from the staged module.

Pending-reboot discovery also recognizes CBS and Windows Update subkeys, and invokes the SCCM static method directly. This can find pending
reboots that v1 missed. Typed results distinguish clear, pending, unavailable and failed probes. The existing PowerShell result remains the
two-field `PendingReboot`/`Indicators` object; callers needing completeness evidence should use the typed result.

Service startup changes resolve wildcard patterns before applying protection and exclusion rules. V1 compared those rules only against the
original pattern, allowing a wildcard to bypass a protected service. Results and confirmation now identify each resolved service; the
module's configurable protected-name list is retained. Scheduled-task exclusions also apply to resolved names, and mutation uses the
captured exact folder/name identities. Service and task discovery failures propagate rather than being reported as missing objects. Native
provider errors replace PowerShell provider diagnostics; successful single-target result fields and WhatIf behavior remain compatible. These
operations are not transactions: a later failure or cancellation does not roll back earlier changes.

Runtime inventory preserves the historical PowerShell product labels and raw CLI output. The reusable Framework API instead reports the
known minimum version established by Microsoft's release thresholds. CLI discovery at the PowerShell boundary now resolves applications
only; aliases and functions named `dotnet` are not executed. Each CLI query lists the architecture visible to that executable.

Event queries now pass result limits to the reader, use structured selectors for large ID lists, and resolve known IDs to configured
channels when `-LogName` is omitted. Unknown IDs require an explicit channel. Sysmon channel checks use the requested remote machine. Field
filtering uses the parsed event data directly, fixing calls to dictionary methods that the old ordered data did not support. PowerShell
retains its per-channel grouping and result limit before semantic filtering; the reusable catalog also supports grouping by provider.
Missing event properties become null during enrichment. Event configuration accepts literal data only and does not evaluate commands or
expressions.

Native log export now fails on provider errors rather than relying on a process wrapper's truth value. Existing destinations are preserved
unless C# callers explicitly request overwrite. Export includes all matching events; a query's read limit and ordering do not limit EVTX
exports. Export cancellation is checked around the synchronous provider call. C# readers support cancellation during reads; the PowerShell
compatibility iterator releases its reader when enumeration stops, but cannot interrupt an in-flight native read. Returned raw records must
be disposed by the consumer. Remote transport and lazy message rendering remain part of the pending integration matrix.

Defender and WMI inspection now use detached property records at the PowerShell boundary. Business fields and arrays remain, but live
`CimInstance` handles and provider metadata/methods are no longer attached. Defender's TXT/JSON formatting remains in `compat.ps1`; raw CIM
serialization metadata may differ. Detection description URLs are resolved by joining `ThreatID` to the threat inventory, fixing the old
assumption that detections always carry a threat name. Unresolved names yield a null URL. Native provider failures retain their own errors;
Defender configuration still honors confirmation at the PowerShell boundary and never bypasses provider protection policies.

WMI inventory exposes all permanent consumer types in C#; the existing PowerShell result retains its three original collections and only
command-line consumers. Partial-read failures appear in the typed result and verbose PowerShell output. Scheduled-task action inventory
retains non-executable actions with null executable/argument fields instead of failing on missing properties.

File searches use literal filesystem paths and do not traverse directory reparse points. C# results report skipped points and read failures;
the legacy presentation wrapper reports them through verbose output. This avoids junction loops and makes partial scans inspectable. The
exclusive write-time window, sort order, selected file fields and each PowerShell edition's mode text are retained.

Offline-join package files now replace existing content atomically instead of writing over an untruncated stream. This removes stale
trailing bytes left by a shorter replacement. New files retain the established BOM/text/terminator format. Native provisioning now frees its
returned buffer explicitly and restores the caller's prior impersonation context. The PowerShell default reuse flag, confirmation level,
credential binding and structured result fields remain. Cancellation before the native call prevents provisioning; cancellation during an
accepted call does not hide its outcome or imply rollback of an AD account change.

Office tool assessment now uses native embedded Authenticode verification, including revocation checks. Catalog-only signatures are not
accepted as ODT evidence; missing revocation evidence remains a trust failure. The status/reason fields and supported Office identities
remain, while native I/O error messages can differ from PowerShell provider messages. Source availability is bounded to 60 seconds at the
PowerShell boundary and remains a reachability check only.

Office protected-path checks now reject null DACLs, untrusted delete-child/generic write grants, unsupported conditional ACEs and device
namespaces. These cases previously could pass incomplete checks. Descriptor diagnostics retain their field names and stages; write-grant
details remain in the diagnostic object. Existing directories are validated without rewriting their ownership or access rules.

Office execution now enforces its documented wait-for-completion policy instead of inheriting process-tree termination from the generic
process wrapper. Cancellation after launch does not kill ODT. Output capture is limited to one MiB per stream; C# results expose truncation
flags, while the existing PowerShell process report retains its fields. Acquisition adds bounded downloads, validates visible HTTPS
redirects and publishes by rename without overwrite. Work-directory cleanup checks links before descending and deletes nonrecursively.
Compiled Office errors retain reason data but may have cmdlet-specific error identifiers and native I/O wording.

Office media parsing now requires schema-appropriate JSON types, rejects duplicate fields and bounds manifest size/depth. Media failure
diagnostics retain stage/path/reason information; failures originating in C# have no PowerShell script path or line. Fresh manifests use
canonical enum names and schema field order. Existing manifest JSON objects and their PowerShell fingerprints are preserved. Configuration
execution snapshots the input XML instead of temporarily modifying it, and refuses DTDs or embedded PIDKEY attributes; product keys must be
supplied through SecureString. It bounds key input length and clears its owned character buffers after writing.

Office host readiness now blocks when reboot probes fail, rather than treating incomplete evidence as a clear state. Journal writes validate
the existing journal's protection as well as its parent and use bounded JSON parsing before atomic publication. Deployment lock disposal
releases ownership; C# callers must acquire and dispose it on the same thread.

Office orchestration and journal reading now run entirely in C#. Imported records require data-only bounded JSON and reject duplicate fields
and serializer type metadata. Fingerprint validation recognizes the existing Windows PowerShell HTML escaping and PowerShell 7 standard
escaping; fresh native records use standard JSON. Recovery diagnostics keep structured stage/category information but have no script source
line. Removal verification now names selected products that remain and MSI registrations that changed instead of returning an unexplained
noncompliant assessment. Durable reports retain nested evidence rather than truncating it at the former JSONL depth of eight.

No-op execution rechecks inventory before reporting success. Explicit application closure matches PID, name and start time again before
termination. Cancellation between phases returns observed progress after cleanup; it never implies installer rollback. If final log writing
fails, execution attempts to update the journal with that failure rather than leaving a completed result. PowerShell confirmation remains in
the public wrapper; C# has no prompt or elevation policy. Full native installation/recovery and live Outlook validation remain pending.
