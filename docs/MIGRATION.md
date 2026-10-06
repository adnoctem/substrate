# From PSFoundation to substrate

Substrate is the independent successor to PSFoundation's C# rewrite. PSFoundation grew from shared Windows administration functions used by
[winkit](https://github.com/adnoctem/winkit). Its 1.8.7 release remains the compatibility baseline for existing PowerShell consumers. The
rewrite began as PSFoundation v2 and became substrate once the reusable libraries and complete module implementation were in place.

The original migration outline, incremental checkpoints and decision records are preserved in Git history. This document records the
resulting design and its rationale. Current operating contracts belong in source comments and command help; remaining work belongs in
[TODO.md](TODO.md).

## Why rewrite in C#?

The goal was to make the administration logic usable by applications without requiring a PowerShell host, while retaining a familiar
PowerShell interface. The rewrite separates domain operations from command binding, presentation and application policy.

- **Direct reuse:** applications call typed domain libraries without importing a module or creating a runspace. PowerShell is one adapter over those libraries.
- **Clearer contracts:** typed values, immutable snapshots and explicit ownership distinguish absence from failure, and completed work from
  uncertain or partial outcomes. Registry restoration and Office deployment expose reviewable plans where the workflow needs them.
- **Controlled native lifetimes:** owned handles, COM references, processes and temporary resources have deterministic cleanup. Long
  operations expose cancellation with documented native limits; cancellation does not promise rollback.
- **Shared foundations:** registry parsing and access, process execution, file publication, trust checks and diagnostics serve multiple
  domains. Office uses the shared registry foundation instead of maintaining another parser.
- **Focused verification:** managed tests exercise domain behavior directly; fresh PowerShell hosts check the adapter against an immutable
  historical implementation. Tests can inject native boundaries without treating a mock installer as evidence of a successful real
  deployment.
- **One source for reference documentation:** C# XML comments feed DocFX; reviewed PowerShell Markdown and command metadata feed both the
  website and packaged Get-Help. Handwritten pages explain workflows and architecture without duplicating a member reference.

Host independence does not imply operating-system independence. Windows capabilities still require Windows, appropriate permissions and
supported native components. The module supports Windows PowerShell 5.1 and PowerShell 7 on x64 Windows; .NET Standard compatibility does
not make registry, CIM or Office operations portable to other operating systems.

## Decisions made during the rewrite

The original aim of compiling every exported name conflicted with some existing PowerShell binding behavior. Simple functions can ignore
extra arguments that compiled cmdlets reject. The accepted design retains one small `compat.ps1` for those boundaries and other
host-specific presentation or confirmation. Domain logic remains in C#.

The adapter preserves 188 commands and four aliases, with ownership recorded in `tools/compiled-commands.psd1` and
`tools/compatibility-commands.psd1`. Public C# APIs follow coherent domain operations rather than reproducing every PowerShell quirk. For
example, the reusable networking API validates strict address literals and contiguous masks, while compatibility commands retain legacy
acceptance rules. Show-Color gains common parameters and completion while continuing to accept unused arguments.

Managers own domain behavior; tools integrate external executables such as LGPO and ODT. Discovery, preparation, validation and execution
remain explicit. Libraries do not prompt, silently elevate, choose credentials, configure global logging or decide application policy.
Native providers replace several PowerShell module dependencies, including Windows Update and package operations, with provider-specific
errors and explicitly documented limits.

Compatibility is tested rather than assumed to mean identical implementation details. A function can become a cmdlet, internal binder
annotations can differ, and an error identifier's implementing-context suffix can change. Safety corrections also remain deliberate: service
exclusions apply to resolved names, missing native evidence is not success, and downloaded executable trust is checked before execution.
Command help records caller-visible differences and resource ownership.

Projects use short domain paths such as `src/IO/IO.csproj`. `Directory.Build.props` derives fully qualified assembly, root namespace and
package identities from the stable prefix and project name. Explicit C# namespaces do not depend on checkout names, remotes or network
access. Build orchestration uses `dotnet msbuild tools/tasks.proj`; the old PowerShell launcher and duplicate v1 source tree are retired.

## Independent identity, compatible state

The successor repository is [adnoctem/substrate](https://github.com/adnoctem/substrate). Libraries use `AdNoctem.Substrate.*`; the module is
`AdNoctem.Substrate.PowerShell`, with GUID `81c6e99d-6a1d-4fc0-b688-2b36c361df05` and an independent initial version of 1.0.0. PSFoundation
release tags were not imported. Ancestor history remains available; the predecessor repository is not replaced or rewritten by this
migration.

Importing the successor requires the new module identity. Command-name compatibility does not automatically migrate a consumer's dependency
declaration. The reusable domain libraries have separate NuGet packages; the PowerShell module is distributed through PSGallery.
Both use the same release version. Winkit's adoption remains a separate follow-up.

Some PSFoundation names intentionally remain because they identify persisted state or public compatibility objects:

- `PSFoundation.RegistryPolicy.Entry` and `PSFoundation.RegistryPolicy.RawEntry`, plus Outlook context type tags.
- The `Global\PSFoundation.OfficeDeployment` mutex and `PSFoundation-Office` storage.
- Existing log/tool defaults, Office journal schemas and fingerprints.

Changing those names would require a deliberate state migration. Their presence is not an incomplete namespace rename.

## Evidence and its limits

Comparison tests materialize PSFoundation 1.8.7 commit `d2d1498275806684b44169504146302d54b7a084` under `build/baseline`. Hash verification
normalizes historical archive line endings independently of checkout settings. CI therefore retains full Git history. An optional dependency
inventory is generated under `build/baseline`; it is not a second maintained command catalog.

Managed tests and packaged-module checks cover the implemented contracts on both runtime targets and both PowerShell hosts. Verification
also builds documentation and packages the module. These checks are distinct from live administrative acceptance and from release
publication. Ordinary pushes test the repository; publishing requires the separately gated release workflow.

Historical PSFoundation Office pilots informed the design. An Enterprise 2007 x86 to Standard 2019 x64 migration on Windows 10 22H2
completed with native success and operator acceptance, but automated compliance retained infrastructure/add-in and exclusion-evidence
limitations. A subsequent Standard 2019 x86 to x64 migration was accepted through PSFoundation 1.8.5, including post-removal continuation,
compliance, activation and an unchanged repeat. Those observations do not certify the C# rewrite, arbitrary interrupted-install replay or
offline-only deployment.

Substrate still needs the controlled Office/Outlook, servicing, package, remote-host and runtime-only validation recorded in
[TODO.md](TODO.md). Historical pilot journals remain inspectable but do not authorize new execution. See the [Office workflow
guide](www/guides/office.md) for current recovery boundaries.
