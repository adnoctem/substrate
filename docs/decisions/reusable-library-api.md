# Reusable C# API boundaries

Accepted for the registry iteration on 2026-10-04 and subsequently adopted for the remaining module migration, now authorized by the
maintainer. All domains follow the same reusable-library and PowerShell-compatibility boundaries.

AdNoctem.Substrate.PowerShell's C# libraries are usable directly from other applications. PowerShell is one consumer. Host independence does
not imply operating-system independence: the registry implementation is a Windows capability.

## Two contracts

- Preserve the pinned PowerShell command names, binding, outputs, errors, confirmation and stream behavior.
- Design the public C# API around typed, coherent operations rather than the PowerShell command inventory. C# names can change during the
  unreleased v2 design. Once adopted by consumers, those public CLR contracts also require compatibility management.
- Keep implementation details internal. Legacy semantics remain in the internal `AdNoctem.Substrate.Registry.Compatibility` namespace, with
  friend access for adapters and tests. They are not exported C# types. The public registry API does not expose provider paths, console
  colors, script comparison quirks, string statuses or PowerShell types.
- Use `Manager` for capabilities AdNoctem.Substrate.PowerShell owns, such as `RegistryManager`, `NetworkManager` and a future
  `PolicyManager` for policy compilation, decompilation, snapshots and files. Use `Tool` for integration with separately maintained vendor
  applications: `LgpoTool` and the future `OdtTool`. AdNoctem.Substrate.PowerShell owns the integration, not those executables. Use
  `Service` for a focused responsibility and specific names such as `Parser`, `Resolver`, `Watcher` or `CommandRunner` where they describe
  the work. Do not add suffixes mechanically or move all code into one manager. This distinction supersedes the earlier blanket rename of
  LGPO to Manager.

## Host independence

- No prompts, UI/thread affinity, global host, service locator or mandatory dependency-injection container in public library operations.
- No silent elevation, credential selection, application policy, log destination or environment changes. The caller supplies authority and
  chooses policy. Optional progress is data supplied through `IProgress<T>`; its implementation determines delivery.
- Retain typed native failures. Absence, access failure, unsupported data, conflict and partial completion are different outcomes.
- Use immutable, validated values and snapshots. Mutable input/output arrays are copied. Ordinary reads need no plan or result envelope.
- Resolve views consistently and make recursion, expansion, overwrite, restore mode and ownership explicit.
- Keep long-running work cancellable between native operations. Use genuine asynchronous process/event waiting where available; document
  thread-pool offloading of synchronous native traversal. Do not promise interruption of an in-flight synchronous Windows call.
- Dispose handles and owned process resources deterministically. Document where the caller owns a returned native handle or mount lease.
- Do not describe multiple registry writes, file import, copy/move or snapshot restoration as transactions. Preserve completed-work evidence
  on cancellation/failure and recheck state before destructive operations.

## Shared foundations

The networking iteration also accepts one `compat.ps1` for legacy PowerShell functions whose extra-argument behavior cannot be reproduced by
direct C# cmdlets. These wrappers contain parameter declarations, stream handling and calls into C#; reusable domain logic stays in C#.
`NetworkManager` handles reads/calculations and `NetworkValidator` handles pure validation. `Show-Color` intentionally gains standard
PowerShell parameters and explicit capture of unused arguments for completion. See
[the compatibility notes](../migration/compatibility-exceptions.md).

Logging accepts structured events with explicitly supplied destinations. Filesystem and registry paths remain distinct types; PowerShell
provider interpretation belongs at the compatibility boundary. `AdNoctem.Substrate.Core` contains only demonstrably shared primitives.
`AdNoctem.Substrate.Diagnostics`, `AdNoctem.Substrate.IO` and `AdNoctem.Substrate.Networking` provide focused reusable foundations. The old
ordered operation result field bag is internal compatibility code, not a public C# result model. No application host or separate NuGet
publishing workflow is introduced.

The expanded C# library surface is an approved addition to the original compatibility-only scope in [original rewrite outline](../v2.md). Preserve essential contract
comments during implementation: ownership, cancellation, partial writes, destructive effects, concurrency, platform limits and compatibility
quirks. Complete public XML documentation and examples in a separate pass after the C# API and PowerShell compatibility stabilize. That
documentation pass remains a v2 release gate; existing PowerShell help stays available throughout migration.

## Documentation delivery

The maintainer selected generated API references: C# documentation comments feed DocFX for .NET reference pages; PowerShell metadata and
Markdown help feed PlatyPS for command reference and Get-Help. Handwritten Markdown is reserved for tutorials, architecture, troubleshooting
and migration evidence. Do not add further handwritten API reference files. The full comment pass and generation pipeline are deferred until
the end of the implementation; retain essential contract comments and the existing help packaging in the meantime.
