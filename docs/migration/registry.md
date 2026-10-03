# Registry migration checkpoint

The first registry implementation replaces 19 public functions with compiled cmdlets on `MVProwess/v2`. The initial `New-OperationResult`
adapter is also packaged, for 20 compiled commands and 192 total exports including aliases. This is a registry checkpoint, not completion of
the module-wide rewrite.

The 2026-10-04 refinement adds a public `RegistryManager` and focused services for direct use from other applications. See the
[C# registry API](../registry-api.md) and [architecture decision](../decisions/reusable-library-api.md). Legacy classes listed below now
live in an internal `Compatibility` namespace; `RegistryPath` and `RegistryReader` there are named `LegacyRegistryPath` and
`LegacyRegistryReader`. They are not the new public C# contract. The former `RegistryTool` is now the internal `RegistryCommandRunner`, and
the legacy report/export helper is `LegacyRegistryFileService`. PowerShell still exports the same commands.

## Command map

Every adapter is a separate `*Command.cs` file under `src/PSFoundation.PowerShell/Registry`. Names follow the public command without its
hyphen, for example `SetRegistryValueCommand`. Public parameter declarations and legacy object/stream mapping remain in this assembly.

| v1 command                      | Original file | C# implementation                                |
| ------------------------------- | ------------- | ------------------------------------------------ |
| ConvertTo-RegistryProviderPath  | registry.ps1  | LegacyRegistryPath                               |
| Resolve-RegistryPath            | registry.ps1  | LegacyRegistryReader, RegistryManager            |
| Get-RegistryKey                 | registry.ps1  | RegistryStore                                    |
| Set-RegistryKey                 | registry.ps1  | RegistryOperations, RegistryStore                |
| Remove-RegistryKey              | registry.ps1  | RegistryOperations, RegistryStore                |
| Get-RegistryValue               | registry.ps1  | RegistryStore                                    |
| Set-RegistryValue               | registry.ps1  | RegistryOperations, RegistryStore                |
| Remove-RegistryValue            | registry.ps1  | RegistryOperations, RegistryStore                |
| Test-RegistryPath               | registry.ps1  | RegistryStore                                    |
| Test-RegistryValue              | registry.ps1  | RegistryStore                                    |
| Get-RegistryValueKind           | registry.ps1  | RegistryStore                                    |
| Compare-RegistrySettingState    | registry.ps1  | RegistryStateService                             |
| Restore-RegistrySettingState    | registry.ps1  | RegistryStateService                             |
| Mount-DefaultUserHive           | registry.ps1  | DefaultUserHiveService, RegistryCommandRunner    |
| Dismount-DefaultUserHive        | registry.ps1  | DefaultUserHiveService, RegistryCommandRunner    |
| Export-RegistryKey              | registry.ps1  | LegacyRegistryFileService, RegistryCommandRunner |
| Search-RegistryKey              | registry.ps1  | LegacyRegistryFileService, RegistryCommandRunner |
| Export-RegistrySettingState     | common.ps1    | RegistryStore, RegistryState                     |
| ConvertTo-RegistrySettingResult | common.ps1    | RegistrySettingAudit                             |

The private snapshot/equality/file helpers have been replaced by these services. The staged package omits `registry.ps1` completely and
removes the two migrated setting helpers from staged `common.ps1`. Source scripts remain available for legacy tests and incremental
migration. Remaining domains do not call the removed private registry helpers. The explicit cutover list is `tools/compiled-commands.psd1`.

## Boundaries and safety

- `PSFoundation.Registry` contains no PowerShell dependency. Its tests consume services directly from C# and check assembly references.
- `RegistryManager.OpenKey` (and the compatibility reader) returns a caller-owned live registry handle. Other store operations dispose their
  own handles, including temporary base keys. Snapshots read raw ExpandString data and retain the explicit registry view.
- Restoration compares expected state, obtains confirmation, checks again immediately before writing, and verifies afterward. It preserves
  unrelated values and keys. The recheck narrows races; it is not an atomic registry transaction.
- Registry provider wildcard expansion and legacy PowerShell conversion rules live at the adapter boundary. Native registry access, mutation
  decisions, snapshots, restoration, file publication and hive lifecycle use C# services.
- Export/search run the Windows system `reg.exe` through separately quoted arguments, drain both streams, check completion, and publish
  through a sibling temporary file. Failures preserve the previous destination. Cancellation is checked before publication.
- Hive operations preserve mount guards, confirmation, logging and the one-retry unload policy. Tests inject the process and hive probes;
  they never mount, unload or modify the real default-user profile.
- Generated MAML help travels with both assembly sets. A malformed leading `.NET` directive in the old Get-RegistryValueKind prose is
  repaired during help generation so that its documentation is readable.

## Compatibility evidence and deliberate implementation differences

The reference is v1.8.7 at `d2d1498275806684b44169504146302d54b7a084`, materialized and hash-checked by the `baseline` launcher command.
Fresh PowerShell processes run the same probe against v1 and the staged v2 package. Each run starts from a fresh disposable HKCU subtree and
removes it afterward. These tests cover binding, typed array cardinality, nulls, status/property order, verbose/information streams, errors
and termination, registry views, snapshot JSON round trips, expected-state conflicts, WhatIf, wildcard paths/names, real export and search,
failed file publication, and unchanged registry configuration calling patterns used by winkit.

The narrow comparison allowances are:

1. Functions become cmdlets, with the corresponding implementing assembly/type and loss of a script block. Every migrated export has one
   intentional compiled owner.
2. The internal script binder `ArgumentTypeConverterAttribute` disappears and C# `NullableAttribute` metadata can appear. Parameter types,
   names, positions, sets, aliases, validation attributes, mandatory flags and pipeline flags are compared independently.
3. PowerShell appends the implementing cmdlet class to fully qualified error identifiers instead of the old script function context. The
   behavior comparison checks the identifier before this context suffix, together with message, exception type, category, target and
   termination. Consumers matching the entire context suffix need review before the v2 release; this is not a blanket error allowance.

Existing quirks are retained: unsigned provider reads of negative DWORDs, case-insensitive simple setter comparisons, typed arrays as one
pipeline object, strict-mode failures for incomplete settings, and the search report's exit-code footer and host-specific UTF-8 BOM. The
detailed snapshot API remains case-sensitive and type-aware even though the older setter has different comparison rules.

## Verification and remaining platform gates

Run through the launcher:

```powershell
.\PSFoundation.ps1 baseline
.\PSFoundation.ps1 build -Format Zip
.\PSFoundation.ps1 format -Managed -Check
.\PSFoundation.ps1 format -Check
.\PSFoundation.ps1 lint
.\PSFoundation.ps1 test -Managed
.\PSFoundation.ps1 test -Path tests/PowerShell
.\PSFoundation.ps1 test
pre-commit run --all-files
```

The current environment is x64 Windows with PowerShell 5.1 and 7.6.6, .NET Framework 4.8.1 and .NET 10. The binaries target net48 and
netstandard2.0; the package selects Desktop/Core dependencies. C# tests run against net48 and net10.0-windows. Native registry tests
exercise both Registry32 and Registry64 views. The local reports are under `build/test-results/registry` and `build/test-results/managed`.

On 2026-10-03, 86 C# test executions passed (41 registry and two core cases per framework), and all 79 v1/v2 behavior scenarios matched in
each PowerShell host. The full Pester suite passed with 911 tests and six existing skips on PowerShell 7, and 914 tests and three existing
skips on Windows PowerShell 5.1. Compilation and both PowerShell lint/format checks passed. C# whitespace formatting passes; the SDK
formatter reports a workspace metadata-reference warning for the multi-target project while loading its formatting workspace.

Actual elevated load/unload of an expendable hive, interactive confirmation host behavior, a clean runtime-only machine, native Framework
4.8, and other processor architectures remain release validation gates. No Office deployment, real default-user hive mutation, or full
winkit script execution is claimed by this checkpoint. GitHub CI is wired to build and test this slice but is not run remotely here.

Implementation stops at this registry boundary. The next domain requires a separate continuation from the maintainer.

The expanded C# suite on 2026-10-04 passes 68 registry and two core tests per target (140 executions). Added native coverage includes
copy/move/rename, `.reg` import/export, explicit view routing, permissions and access denial, notifications and cancellation. Synthetic
failure tests cover hive ownership and file publication. Advanced live hive/remote/privilege validation remains outstanding as detailed in
the API guide. The original 79-scenario-per-host PowerShell comparison remains unchanged and passes after the compatibility separation.
