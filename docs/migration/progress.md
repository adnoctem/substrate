# v2 implementation checkpoint

## Baseline and branch

- Frozen source: v1.8.7, `d2d1498275806684b44169504146302d54b7a084`.
- Main CI/release freeze: `ba47a1e1ca6997012f244c7994193358f3f0e90b`; local only, not pushed.
- Development branch: `MVProwess/v2`. The supplied `v2.md` is carried on this branch only.
- Scope: 188 public functions, four aliases, 19 source domains. This is an incremental rewrite, not a completed v2 release.

## Accepted compatibility design

The maintainer selected small PowerShell wrappers in one `compat.ps1`, with C# doing the actual work. `NetworkManager` and
`NetworkValidator` provide the reusable networking API. `Show-Color` intentionally adds explicit argument capture and standard PowerShell
flags for completion. See
[the accepted decision and precise compatibility exceptions](compatibility-exceptions.md#accepted-decision-handling-extra-arguments). There
is no pending choice on this topic.

## Build and execution proof

The .NET 10.0.401 SDK is pinned. Build, restore, binaries, NuGet packages, staged modules and test reports use `build/`; release archives
continue using `dist/`. Run all commands through `PSFoundation.ps1`:

```powershell
.\PSFoundation.ps1 baseline -WriteFixtures
.\PSFoundation.ps1 build -Format Zip
.\PSFoundation.ps1 test -Managed
.\PSFoundation.ps1 test -Path tests/PowerShell
```

Verified on 2026-10-03:

- Both real hosts captured all 192 exported command/alias contracts from the pinned v1 source.
- Restore and Release build succeed with zero warnings/errors, including repeated builds.
- The C# suites pass 43 tests per target: 41 registry and two core tests on `net48` and `net10.0-windows` (86 executions).
- Fresh-host import, registry parameter/help and v1/v2 behavior comparisons pass on Windows PowerShell 5.1 and PowerShell 7.6.6. Tests
  verify exports, compiled ownership, live registry handles, result mapping, streams, mutation boundaries and errors.
- The registry probe compares 79 scenarios per host against the pinned v1 source.
- Full Pester runs pass: 911 passed / six skipped on PowerShell 7; 914 passed / three skipped on Windows PowerShell 5.1. Neither run has
  failures. The additional modern-host skips are the existing unavailable WinRT metadata cases.
- The net48 target executes on this workstation's installed .NET Framework 4.8.1. Native Framework 4.8-machine validation remains pending.

The first shared netstandard package failed actual registry access under PowerShell 5.1 because it loaded a generic platform dependency.
Registry and adapter projects now also target net48. Staging selects Desktop/Core assembly sets and uses Windows runtime assets for Core. No
PowerShell runtime/reference DLL is shipped. This proves the initial host boundary only; the final architecture/runtime matrix still needs
review as further dependencies are migrated. The current packaging RID is win-x64; other architectures remain unvalidated.

## Current implementation scope

As of 2026-10-04, the registry domain also has a public C# API for other applications, led by `RegistryManager`, with typed values and
paths, snapshot plans, search, copy/move/rename, file/hive services, permissions and change notifications. Legacy PowerShell behavior is
isolated in internal compatibility classes. The managed suite passes 68 registry and two core tests per target (140 executions), and the
existing PowerShell package contract/behavior tests pass in both hosts. See [the API guide](../registry-api.md) and
[the agreed library boundaries](../decisions/reusable-library-api.md). Host independence does not claim cross-platform registry support.

The full Pester suites were rerun on this iteration: 911 passed / six existing skips in PowerShell 7 and 914 passed / three existing skips
in Windows PowerShell 5.1, with no failures. Both hosts passed formatting and lint. The final C# regression covers preserving partial-write
evidence when an unsupported native value appears during restoration.

The foundation, registry-policy, networking, Windows inventory and LGPO slices bring the staged package to 49 compiled commands and 15
C#-backed compatibility functions, with 124 public functions still using legacy implementations. All public commands from `common.ps1`,
`registry.ps1`, `errors.ps1`, `log.ps1` and `policies.ps1` are migrated; those scripts are excluded from staging. The package retains 192
exports. See [the foundation checkpoint](foundations.md), [the policy codec checkpoint](policy-codec.md),
[the networking API guide](../networking-api.md) and the generated [command inventory](commands.json). Regenerate the inventory through
`baseline -Inventory` after a cutover. It records each pinned public function, its source, direct command dependencies and current migration
status, distinguishing compiled cmdlets, compatibility functions and legacy functions.

The policy checkpoint passes 136 managed tests per target (272 executions). Full Pester results are 915 passed / six existing skips in
PowerShell 7 and 918 passed / three existing skips in Windows PowerShell 5.1. Both hosts compare the staged command contracts and behavior
with the pinned baseline; see the policy checkpoint for the final boundary refinements and validation scope.

The networking checkpoint passes 145 managed tests per target (290 executions), including native adapter reads without PowerShell. The
fresh-host package suite passes all 12 tests; networking compares 297 observations per host with v1, including address calculations, missing
values, errors, aliases, extra arguments and returned CIM object types. `Show-Color` also has explicit parameter/completion checks. The
current build reports zero warnings and errors. These checks cover local inventory on the development workstation; provider failures, other
Windows versions and the wider release matrix still require integration validation.

Full Pester validation for this checkpoint passes 917 tests / six existing skips in PowerShell 7 and 920 tests / three existing skips in
Windows PowerShell 5.1, with no failures. The full documentation-comment pass remains deferred until the internal APIs stabilize.

C# formatting now runs through `format -Managed` and pre-commit. Build warnings are errors, CI restores enforce lock files, and the feature
workflow builds the staged module before running C# and PowerShell package tests. Full module migration, analyzer policy beyond compiler
warnings, runtime-only machine checks, actual elevated hive validation and the release matrix remain pending.

## LGPO and verified downloads

The LGPO and download slice adds `PSFoundation.Policies` with explicit request preparation and policy execution, plus verified HTTPS
downloads in `PSFoundation.Networking`. The managed suites pass 158 tests per target (316 executions). Tests use a locally built process
fixture and simulated HTTP responses; they do not apply real policies or download/execute vendor tools. `Resolve-LGPOSource` is compiled;
`Invoke-LGPO` and `Test-LGPOInstalled` use the shared compatibility script. The public integration is named `LgpoTool`: Tool is reserved for
separately maintained vendor applications, including the future `OdtTool`. PSFoundation-owned policy file/snapshot operations can use
`PolicyManager`. Installer and availability command migration is completed in the subsequent checkpoint below; the source pin is recorded
below. See [the compatibility notes](compatibility-exceptions.md) for the deliberate source-metadata update.

### Reviewed LGPO package, 2026-10-04

The maintainer supplied the original LGPO ZIP and extracted files. The ZIP contains exactly `LGPO_30/LGPO.exe`, `LGPO_30/LGPO.pdf` and
`LGPO_30/Microsoft Security Compliance Toolkit - Standalone Use Terms.pdf`. The executable inside the archive matches the separately
extracted executable byte-for-byte by SHA-256. No vendor executable was run and no policy was applied.

- ZIP: 531635 bytes; SHA-256 `CB7159D134A0A1E7B1ED2ADA9A3CE8CE8F4DE391D14403D55438AF824247CC55`.
- Executable: 481144 bytes; SHA-256 `0C97F29543418B30340C4FF5D930D31E6196DD59C2CC74B6B890FA7B90C910C7`.
- File version: `3.0.2004.13001`.
- Windows Authenticode result: `Valid`, signed by Microsoft Corporation, with a Microsoft Time-Stamp Service countersignature.
- Signer certificate thumbprint: `62009AAABDAE749FD47D19150958329BF6FF4B34` (review evidence, not a permanent certificate-rotation policy).

The supplied ZIP's recorded download URL matches the existing Microsoft catalog URL. Microsoft's
[Security Compliance Toolkit download page](https://www.microsoft.com/en-us/download/details.aspx?id=55319) also lists the LGPO archive at
519.2 KB. This review pins the supplied bytes; it does not claim that a fresh download was compared. Future downloads must match the ZIP
pin. The C# source catalog also records the executable pin, file version and verification date; the existing PowerShell result shape stays
unchanged, with `Sha256` referring to the ZIP.

Documentation generation is deferred to the final pass: C# comments through DocFX, PowerShell metadata and Markdown help through PlatyPS.
Further handwritten Markdown is for architecture, tutorials, troubleshooting and migration evidence, not API references. The existing help
packaging remains until that final pipeline is established.

## Windows inventory and tool installation checkpoint

The next implementation chunk migrates another 20 public commands. `PSFoundation.Windows` provides `SystemManager`, `IdentityManager`,
`HostValidator` and detached snapshots. Version reads use `RegistryManager`; memory and uptime use native Windows calls; disk inventory uses
native CIM associations. No PowerShell is launched or embedded to perform these operations. Applications receive typed numbers, timestamps
and failure evidence; the PowerShell adapter retains the established property names and invariant formatting. Async inventory supports
cancellation and explicit operation timeouts. DNS cancellation ends the caller's wait, while the underlying runtime resolver can finish in
the background; native account translation cannot be interrupted after it starts.

`ApplicationPaths` constructs product paths without creating directories. `ComManager` exposes explicit single-reference release and
explicit collection, with no automatic process-wide cleanup or apartment assumptions. The adapter retains the best-effort COM teardown
behavior. Identity reads dispose their native token handles. `RobocopyExitResult` exposes the vendor exit flags and success meaning without
running Robocopy.

`LgpoTool` now has separate availability and installation operations. It borrows an HTTP client, verifies the reviewed archive pin, selects
the exact catalog entry, verifies the executable pin and atomically publishes the executable. `VerifiedArchiveService` rejects ambiguous and
noncanonical ZIP paths, bounds compressed and extracted sizes, and leaves an existing destination intact on failure. Neither operation
executes vendor content or changes policy. Custom source metadata requires explicitly supplied trusted pins. Scheduled vendor update PRs
remain deferred as described below.

Managed validation passes 194 tests per target (388 executions), including real local read-only inventory and identity checks plus synthetic
HTTP/ZIP failure cases. No live LGPO download, policy application, printer changes or Office deployment is performed. The current host
matrix remains Windows x64 with Windows PowerShell 5.1 and PowerShell 7; a full C# rewrite and the v2 release are not complete.

Final checkpoint validation passes the full Pester suites: 919 passed / six existing skips under PowerShell 7 and 922 passed / three
existing skips under Windows PowerShell 5.1. Each includes the 14 fresh-host package tests covering both engines. All 192 exports remain;
parameter contracts, packaged help, stable system values and dynamic result shapes match the baseline, subject to the documented
compatibility exceptions. Build reports zero warnings/errors, and all pre-commit hooks pass.

Fresh-host compatibility probes now run in a Windows job with a 512 MiB aggregate commit limit, a two-minute timeout, bounded output capture
and termination of child processes when the job closes. Synthetic regression tests verify memory enforcement, timeout termination, quoting
and output bounds. This replaces unbounded child execution after a stalled report serializer exhausted workstation memory. Verbose records
are reduced to their message text before JSON serialization; PowerShell invocation internals are not serialized as test evidence.

## Package inventory and network probe checkpoint

`PSFoundation.Packages` introduces `Win32ProgramManager` for local uninstall registrations. It uses `RegistryManager`, explicit registry
views and immutable metadata. Applications can supply their own roots, read an exact registration, or request inventory with cancellation.
Reads fail on access or malformed-data errors by default; explicitly requested partial inventory retains the failed locations and
exceptions. Async traversal offloads synchronous registry reads and checks cancellation between calls. Inventory never invokes Windows
Installer, loads another user's profile or runs uninstall strings. A registration is not proof that an application is usable.

`Get-Win32Program`, `Find-Win32Program` and `Get-InstalledProgramCount` now use compiled adapters. These preserve the PowerShell filters,
hidden-component behavior, exact-path output, provider paths, unsigned registry numbers and optional fields. The library retains raw strings
and registry kinds; environment expansion and PowerShell truth conversion remain in the adapter. The migration now owns 52 compiled commands
and 15 C#-backed compatibility functions: 67 of 188 public functions, with all 192 exports retained.

`NetworkProbeService` now exposes bounded DNS resolution and resolves names before creating TCP sockets. All address attempts share one
deadline; cancellation prevents a late DNS completion from initiating a connection. Native errors and the selected address are retained, and
sockets close deterministically. Older runtimes cannot cancel an in-flight DNS lookup, so cancellation ends the caller's wait and late
faults are observed. The multi-channel remote-host PowerShell command, including WSMan and CredSSP, remains pending.

The synthetic memory-limit fixture now initializes console output before allocation and releases buffers before reporting the expected
out-of-memory result. This avoids a second allocation failure while reporting the first. Its allocation ceiling and the runner's job limits
are unchanged.

Win32 execution, AppX/WinGet, remaining native system/security operations and Office remain subsequent migration work. No installer,
uninstaller, downloaded executable or live Office operation was run for this checkpoint.

Validation passes 209 managed tests per target (418 executions), including synthetic registry data, partial failures, cancellation and local
TCP/DNS probes. The full Pester suites pass 921 tests / six existing skips in PowerShell 7 and 924 tests / three existing skips in Windows
PowerShell 5.1. Each includes 16 guarded fresh-host compatibility tests, comparing parameters, help and behavior against the frozen v1
baseline. Build succeeds with zero warnings/errors. These checks validate this workstation and do not replace the wider release matrix.

## Package execution and native maintenance checkpoint

The next batch moves ten more public functions onto C#: Win32 installation/uninstallation, all four device commands, pending-reboot and
file-lock discovery, service startup changes and scheduled-task enable/disable. The staged package now contains 60 compiled commands and 17
C#-backed compatibility functions: 77 of 188 public functions, with all 192 exports retained. `devices.ps1` and the import-time Restart
Manager compilation are retired from the staged package; frozen v1 sources remain available as comparison fixtures.

`Win32ProgramManager` separates command selection, request preparation, execution and detached launch. `DeviceManager`, `RebootManager`,
`FileLockManager`, `ServiceManager` and `ScheduledTaskManager` provide reusable typed results and native operations, without PowerShell
invocation inside the libraries. Service control also exposes explicit start/stop/pause/resume requests. Native resources remain owned by
each operation; async APIs and cancellation limitations are documented where providers cannot be interrupted. PowerShell retains previews,
confirmation, legacy result fields and its configurable service protection list.

The focused workflow additions cover quoted synthetic installer execution, MSI preparation, quiet-command selection, real temporary-file
locks, native inventory and cancellation before mutation. Existing guarded compatibility probes now also exercise the new command outputs,
service protection and WhatIf paths under both PowerShell engines. No real application installation/uninstallation, printer change, service
change, scheduled-task change or Office operation was performed. Physical scanner availability and privileged provider mutations still need
their appropriate integration environments. The remaining AppX/WinGet, security, update and Office work continues after this checkpoint.

## Runtime inventory and event logs checkpoint

The staged module now owns 63 compiled commands and 30 C#-backed compatibility functions: 93 of 188 public functions, with all 192 exports
retained. `DotNetManager` reads Framework registrations through `RegistryManager`; `DotNetTool` queries an explicitly selected local CLI.
The reusable results preserve raw registry values, release evidence, prerelease versions, unknown CLI lines and native process outcomes.

`PSFoundation.Security` adds native event readers, detached snapshots, EVTX export, structured queries, bounded XML parsing, semantic event
definitions and reusable field filters. The default catalog is embedded for callers outside PowerShell. Caller-supplied JSON catalogs are
supported; PowerShell configuration remains a safely parsed data-only hashtable. All seven event-family commands now delegate their queries
and filtering to C#. Structured queries split large ID lists into bounded selectors. Readers and rejected records close deterministically;
raw records returned through the compatibility API remain caller-owned, matching `Get-WinEvent`.

Validation passes 218 managed tests per target (436 executions), adding only four workflow tests to the preceding checkpoint. Native read
and export tests use the local System channel, delete their temporary EVTX files, and never clear logs. Guarded PowerShell probes cover both
engines, the default catalog, configuration/enrichment compatibility, native queries and runtime inventory. Remote event sessions and the
wider host matrix still require integration validation. No Defender configuration, firewall rule or production installation was changed.

## Security inspection checkpoint

The next batch reaches 100 of 188 public functions: 67 compiled commands and 33 C#-backed compatibility functions. All 192 exports remain.
`DefenderManager` owns native threat/detection/status inventory and explicit exclusion add/remove operations. `WmiPersistenceManager` reads
filters, consumers and bindings without modifying subscriptions. `CimManager` supplies reusable queries with detached, read-only values;
caller-supplied sessions remain borrowed. Scheduled-task inventory now includes action data, including non-executable COM actions.

`FileInventoryManager` supports write-time windows, recursion, hidden files, cancellation and explicitly requested partial results. Read
failures and skipped directory reparse points remain visible to C# callers. PowerShell retains its formatting, logging and host-specific
file mode strings. Threat description links no longer require `System.Web` to have been loaded, and detection links resolve through threat
IDs.

The batch adds two managed workflow tests, bringing the suite to 220 per target (440 executions). Existing guarded probes cover the seven
newly migrated commands, parameter contracts, read-only task/WMI inspection, filesystem search and Defender WhatIf. Native Defender changes
were validated for cancellation before invocation; actual exclusion changes require a disposable integration machine and were not executed.
Remote CIM sessions, Defender provider mutations and diverse WMI consumer registrations remain integration work.

## Offline domain provisioning checkpoint

The staged module now owns 69 compiled commands and 33 C#-backed compatibility functions: 102 of 188 public functions, retaining all 192
exports.

`OfflineDomainJoinManager` now owns the native provisioning call, optional credential context and package file writing. Typed requests can
also include certificate templates and machine policies. C# defaults to no account reuse; the PowerShell adapter explicitly retains its
existing reuse option. The library never joins the local machine or prompts for credentials. Native buffers and credential tokens are
released deterministically, and impersonation restores the previous identity rather than discarding a caller's existing context.

`New-OfflineDomainJoinBlob` and `New-DjoinFile` are compiled adapters. The staged package no longer compiles the old provisioning binding at
import. Package writes retain the UTF-16LE BOM/terminator format and now replace existing content atomically, fixing stale trailing bytes
when overwriting a longer package. The caller remains responsible for protecting the credential-bearing package and its destination
directory.

The batch adds one synthetic provisioning workflow test: 221 managed tests per target (442 executions). Both PowerShell hosts verify preview
results, package bytes, overwrite behavior and parameter/help contracts. No AD account is created or reused, and no domain join is
attempted. Live provisioning, optional certificate/policy inclusion and credential impersonation require a disposable domain integration
environment.

## Office observation checkpoint

The Office library adds typed configuration, inventory, language preservation, compliance and activation assessment. Registry discovery uses
`RegistryManager` and selects allowed value names before reading data. Installed builds follow documented inventory first, then agreeing
active product resource versions; malformed stronger evidence stops fallback. App Paths retain present, missing and uncertain states,
without launching applications or following network paths. MSI products, related components and orphaned patches retain their existing
classifications.

The package now owns 72 compiled commands and 34 C#-backed compatibility functions: 106 of 188 public functions, with all 192 exports.
`New-OfficeDeploymentConfiguration`, `Get-OfficeInventory` and `Get-OfficeActivationStatus` are compiled adapters. `Test-OfficeDeployment`
retains legacy schema normalization in the compatibility wrapper and delegates comparison to C#. The core also supports detached evidence
analysis, explicit locale selection, async inventory/activation and cancellation. These APIs grant no installation authority.

Two managed workflow tests bring the suite to 223 per target (446 executions). Guarded PowerShell comparisons cover configuration and
inventory report shapes, version evidence precedence, incomplete/conflicting evidence, compliance, parameter/help contracts and local
read-only inventory. Native volume activation, broad Office/OS/architecture combinations and real deployment/recovery remain integration
work. No Office installation, activation or removal was attempted. Deployment planning, media, execution and recovery are still being
migrated; the complete Office workflow is not yet native.

## Office planning checkpoint

`OfficeDeploymentPlanner` now evaluates typed requests and detached observations without I/O. It preserves explicit removal/MSI consent,
maintenance restrictions, recovery requirements, language transitions and missing-media blockers. Caller-supplied media outcomes support
offline planning only; they are never execution authority. `OdtConfigurationBuilder` supplies separate operations for download, install,
migration, removal, update, languages, application selection, update configuration and application preferences. Documents retain disabled
CDN fallback and forced shutdown; install/migration alone request volume activation.

`Get-OfficeDeploymentPlan` now delegates decisions to C#, while the compatibility boundary retains schema checks, media discovery and the
existing PowerShell JSON fingerprint format. Private XML generation also delegates to C#. This reaches 107 of 188 public functions: 72
compiled commands and 35 C#-backed wrappers. One additional managed workflow brings the suite to 224 per target (448 executions). Both hosts
compare all nine plan operations, ten XML operations, exact version text and parameter/help contracts against v1. Execution, media and
recovery are still being migrated; no real Office changes were performed.

## Office trust checkpoint

`AuthenticodeVerifier` uses Windows trust policy for embedded signatures, releases verification state and certificate handles, and returns
detached public certificate evidence. Online and cache-only revocation modes both fail closed; there is no no-check option. The supplied
LGPO executable was verified read-only in both PowerShell hosts, including its Microsoft signer and timestamp, without executing it.
Catalog-only verification remains outside this API. Native trust calls cannot be interrupted; cancellation is observed before/after them.

`FileSecurityManager` reads/writes explicit security sections and creates directories with their descriptor applied at creation.
`OfficePathGuard` replaces the staged private path/ACL helpers and retains diagnostic stages. It additionally rejects null DACLs, untrusted
delete-child/generic write grants, unsupported conditional ACEs and device namespaces. No existing deployment directory is automatically
repaired, and path observations are not a transaction against concurrent filesystem changes.

`OdtTool` now owns reviewed source metadata, bounded HEAD checks and signature/identity/version assessment. Three more public commands are
compiled, reaching 110 of 188 functions: 75 compiled and 35 C#-backed wrappers. Two functional tests cover native descriptor handling,
unsigned tool rejection, descriptor policy and offline HTTP probing, bringing the managed suite to 226 per target (452 executions). Office
report and command-contract comparisons remain guarded in both hosts. No downloaded tool or Office installer was executed; acquisition,
execution and recovery integration are still pending.

## Office tool execution checkpoint

`OdtTool` adds explicit sync/async download, configure, customize and help operations. Every invocation verifies the tool again and holds
read leases on its executable and configuration until completion. Once launched, ODT is allowed to finish even if cancellation is requested,
because its work may involve shared services. Capture is bounded and truncation remains visible in the typed process result.

Acquisition validates Microsoft HTTPS redirects, caps extractor downloads at 64 MiB and verifies the extractor before executing it. It
verifies extracted setup.exe, publishes without overwrite and rechecks the published tool. Working directories receive protected ACLs at
creation; cleanup checks each directory before enumeration and uses nonrecursive deletion. Existing tools are checked and never silently
replaced. Supplied HTTP transports must disable automatic redirects for each hop to be visible to validation.

The package now owns 77 compiled commands and 35 C#-backed wrappers: 112 of 188 public functions. The same managed trust workflow also
checks unsigned invocation rejection across all modes, cancellation before work, preservation of an existing untrusted tool and redirect
rejection. Fresh PowerShell probes cover acquisition previews, WhatIf and the private execution boundary. Real Microsoft extraction and
Office process integration remain pending; ordinary tests do not execute downloaded tools or install Office.

## Local automation

The ignored `.codex/config.toml` retains workspace-write and on-request approvals. `.codex/rules/repository-build.rules` authorizes the
absolute launcher in this checkout for build, test, format, lint and baseline commands, plus Git staging and local commits anchored to this
checkout with `git -C`. The CLI rule checker confirmed these match and push, release and another checkout's commit do not. Use these exact
command forms for the scoped rules to match. Project rules require a trusted project and load at Codex startup. After the maintainer
restarted Codex, the absolute launcher ran the registry builds, formatting, lint and test commands successfully.

## Next work

After the C# migration, upgrade developer tooling and the `PSFoundation.ps1` interface, including scheduled vendor-tool update PRs alongside
the existing dependency workflow. This is deferred work, not an active CI automation:

- Check official LGPO/ODT download locations for availability and release changes; distinguish URL reachability from content trust.
- Validate HTTPS source/redirect policy, archive paths and size limits, and Microsoft Authenticode publisher/chain/timestamp before
  proposing new archive and executable pins. Inspect candidates without running them or applying policies.
- Open an automated PR containing old/new URLs, hashes, versions, signature evidence and the test results. Reuse or update an existing PR
  for the same candidate. Changed hashes must never be silently accepted or auto-merged; failed verification must fail the check.
- Test download integrity failures, package layout, source metadata and supported PowerShell/runtime packaging in CI. Keep scheduled
  upstream checks separate from deterministic offline tests; never fetch real vendor tools during ordinary unit tests.

The maintainer has authorized continuation beyond registry. Continue with verified tools, native inventory, then dependent system/security,
package/update, Office and interop workflows. Preserve Office safety and recovery contracts. Audit native alternatives to PSWindowsUpdate,
WinGet, PowerShell module management and other provider dependencies before cutting those commands over. Keep winkit unchanged. Publishing
remains disabled and requires a separate v2 release decision. No complete v2 or native Office validation is claimed.
