# Reusable C# API boundaries

Accepted for the registry iteration on 2026-10-04 and subsequently adopted for the remaining module migration, now authorized by the
maintainer. All domains follow the same reusable-library and PowerShell-compatibility boundaries.

PSFoundation's C# libraries are usable directly from other applications. PowerShell is one consumer. Host independence does not imply
operating-system independence: the registry implementation is a Windows capability.

## Two contracts

- Preserve the pinned PowerShell command names, binding, outputs, errors, confirmation and stream behavior.
- Design the public C# API around typed, coherent operations rather than the PowerShell command inventory. C# names can change during the
  unreleased v2 design. Once adopted by consumers, those public CLR contracts also require compatibility management.
- Keep implementation details internal. Legacy semantics remain in the internal `PSFoundation.Registry.Compatibility` namespace, with friend
  access for adapters and tests. They are not exported C# types. The public registry API does not expose provider paths, console colors,
  script comparison quirks, string statuses or PowerShell types.
- Use `Manager` for a convenient domain entry point, `Service` for a focused responsibility, and specific names such as `Parser`,
  `Resolver`, `Watcher` or `CommandRunner` where they describe the work. Do not add a suffix mechanically or move all code into one manager.

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

Logging accepts structured events with explicitly supplied destinations. Filesystem and registry paths remain distinct types; PowerShell
provider interpretation belongs at the compatibility boundary. `PSFoundation.Core` contains only demonstrably shared primitives.
`PSFoundation.Diagnostics`, `PSFoundation.IO` and `PSFoundation.Networking` provide focused reusable foundations. The old ordered operation
result field bag is internal compatibility code, not a public C# result model. No application host or separate NuGet publishing workflow is
introduced.

The expanded C# library surface is an approved addition to the original compatibility-only scope in `v2.md`. Preserve essential contract
comments during implementation: ownership, cancellation, partial writes, destructive effects, concurrency, platform limits and compatibility
quirks. Complete public XML documentation and examples in a separate pass after the C# API and PowerShell compatibility stabilize. That
documentation pass remains a v2 release gate; existing PowerShell help stays available throughout migration.
