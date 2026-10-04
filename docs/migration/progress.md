# v2 implementation checkpoint

## Baseline and branch

- Frozen source: v1.8.7, `d2d1498275806684b44169504146302d54b7a084`.
- Main CI/release freeze: `ba47a1e1ca6997012f244c7994193358f3f0e90b`; local only, not pushed.
- Development branch: `MVProwess/v2`. The supplied `v2.md` is carried on this branch only.
- Scope: 188 public functions, four aliases, 19 source domains. This is an incremental rewrite, not a completed v2 release.

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

The next shared-foundation slice brings the staged package to 28 compiled commands, with 160 public functions still using the legacy
implementation. All public commands from `common.ps1`, `registry.ps1` and `errors.ps1` are compiled; those scripts are excluded from
staging. The package retains 192 exports. See [the foundation checkpoint](foundations.md) and the generated
[command inventory](commands.json). Regenerate the inventory through `baseline -Inventory` after a cutover. It records each pinned public
function, its source, direct command dependencies and current migration status.

C# formatting now runs through `format -Managed` and pre-commit. Build warnings are errors, CI restores enforce lock files, and the feature
workflow builds the staged module before running C# and PowerShell package tests. Full module migration, analyzer policy beyond compiler
warnings, runtime-only machine checks, actual elevated hive validation and the release matrix remain pending.

## Local automation

The ignored `.codex/config.toml` retains workspace-write and on-request approvals. `.codex/rules/repository-build.rules` authorizes the
absolute launcher in this checkout for build, test, format, lint and baseline commands, plus Git staging and local commits anchored to this
checkout with `git -C`. The CLI rule checker confirmed these match and push, release and another checkout's commit do not. Use these exact
command forms for the scoped rules to match. Project rules require a trusted project and load at Codex startup. After the maintainer
restarted Codex, the absolute launcher ran the registry builds, formatting, lint and test commands successfully.

## Next work

The maintainer has authorized continuation beyond registry. Continue with policy codecs and verified tools, native inventory, then dependent
system/security, package/update, Office and interop workflows. Preserve Office safety and recovery contracts. Audit native alternatives to
PSWindowsUpdate, WinGet, PowerShell module management and other provider dependencies before cutting those commands over. Keep winkit
unchanged. Publishing remains disabled and requires a separate v2 release decision. No complete v2 or native Office validation is claimed.
