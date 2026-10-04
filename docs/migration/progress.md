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
