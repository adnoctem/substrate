# Shared foundation checkpoint

This slice continues the approved migration under the [reusable API rules](../decisions/reusable-library-api.md). The staged package has 28
compiled commands out of 188 public functions, plus the four unchanged aliases. It is not the complete module rewrite.

## Reusable services

| Library     | Entry points                                            | Contracts                                                                                                                                           |
| ----------- | ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| Diagnostics | `ProcessManager`, `ProcessRequest`, `ProcessResult`     | Windows argv quoting, separate captured streams, explicit timeouts and stop policy, deterministic process ownership, bounded capture and cleanup.   |
| Diagnostics | `LogManager`, `LogEntry`, `ILogSink`, `JsonLineLogSink` | Caller supplies timestamps and sinks; serialized writer access, explicit writer ownership, no console or global logger.                             |
| Diagnostics | `ErrorTranslator`, `ErrorTranslation`, `ErrorDomain`    | Curated diagnostic lookup, unsigned code identity, exception-chain precedence, ambiguous text produces no match; no automatic suppression or retry. |
| IO          | `FileSystemPath`, `FileManager`                         | Explicit base for relative paths, native long-name expansion, atomic file publication, cancellable streaming and SHA-256.                           |
| Networking  | `IPAddressParser`, `IPNetwork`, `NetworkProbeService`   | Strict literal parsing, immutable subnet data, range containment, explicit bounded TCP probes and native socket failure evidence.                   |

All three libraries target netstandard2.0 and have no PowerShell reference. Registry process execution now uses `ProcessManager`, retaining
the registry utility's view selection and cancellation contract. `Core.OperationResult` moved to internal compatibility code; arbitrary
PowerShell result fields are not the shared public C# result model.

Process cancellation before launch throws. After launch, `TerminateProcess` or `TerminateTree` returns stopped-process evidence;
`WaitForExit` observes the request but lets owned work finish. The latter can wait indefinitely and must be selected deliberately. Tree
termination cannot guarantee cleanup of detached descendants. Captured stdout/stderr each default to a 16 Mi-character limit; excess is
drained and discarded with truncation flags. PowerShell's legacy adapter requests the previous unbounded capture behavior.

Filesystem normalization is lexical, not a sandbox or symlink resolution service. Atomic publication covers one destination, requires an
existing parent and does not imply a multi-file transaction. Log sinks do not redact or choose which application data to log. TCP success
only establishes connectivity, not service health or authentication. These policies remain the caller's responsibility.

## Command cutover

| Command                                | Compiled adapter                                                         | Domain implementation or boundary                                                      |
| -------------------------------------- | ------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| `New-OperationResult`                  | `Core.NewOperationResultCommand`                                         | Internal ordered result compatibility; now also checks omitted and null string fields. |
| `Add-OperationResult`                  | `Core.AddOperationResultCommand`                                         | Same mapper; preserves the caller's `IList` identity and conditional output.           |
| `Write-OperationResultLog`             | `Core.WriteOperationResultLogCommand`                                    | Legacy host path/defaults and ETS JSON presentation.                                   |
| `Resolve-LongPath`                     | `Core.ResolveLongPathCommand`                                            | PowerShell provider resolution followed by `FileSystemPath`.                           |
| `Write-Log`                            | `Diagnostics.WriteLogCommand`                                            | Host information stream presentation; colors stay outside the reusable logger.         |
| `Get-ErrorTranslation`                 | `Diagnostics.GetErrorTranslationCommand`                                 | `ErrorTranslator`.                                                                     |
| `Invoke-SafeProcess`                   | `Diagnostics.InvokeSafeProcessCommand`                                   | `ProcessManager`; maps legacy text, files and native error records.                    |
| `Test-IPv4Address`, `Test-IPv6Address` | `Networking.TestIPv4AddressCommand`, `Networking.TestIPv6AddressCommand` | Internal legacy validators; the public C# parser has stricter literal semantics.       |

The JSON log adapter invokes only the installed `ConvertTo-Json` cmdlet in the current runspace to preserve host-specific ETS, dates,
escaping and depth behavior. It creates no scripts or additional runspace and performs no domain operation there. Ordinary C# logging uses
the independent JSON-line sink. This is an adapter serialization boundary, not a domain dependency on PowerShell.

The contract harness now compares every command in `tools/compiled-commands.psd1`, rather than a fixed registry subset. Help generation,
staging and export ownership use that same catalog. The existing registry comparison remains in place, alongside 66 new foundation
observations per host. Tests use synthetic files, a fixture executable and loopback listeners; they perform no installation, network
configuration or privileged system mutation.

## Narrow compatibility allowances

The registry checkpoint's command-kind, nullable metadata and error-context-suffix allowances still apply. Two additions are explicit:

- `Add-OperationResult.Results` has an adapter-only argument transformation that replaces the script binder's implicit conversion. Its
  parameter remains `IList`. Tests prove an empty caller-owned `ArrayList` is mutated in place, without copying it. The transformation
  attribute alone is excluded from metadata comparison. The upstream
  [interface collection binding issue](https://github.com/PowerShell/PowerShell/issues/20066) describes a related compiled-binder failure;
  this repository's actual behavior is established by fresh-host fixtures.
- The two read-only IP validators can report inactive `ConfirmImpact=Medium` as functions; compiled metadata reports `None` when
  `SupportsShouldProcess` is false. Only those two commands and those values are normalized. No confirmation parameter or behavior changes.

Windows PowerShell 5.1's existing `Write-Log -InformationAction Ignore` error is preserved. Native process errors retain the legacy outer
method-invocation exception and short error identifier. The current text comparisons use this workstation's host culture; broader localized
host validation remains a release gate, with structured assertions needed for localized host messages.

## Verification

On 2026-10-04, SDK 10.0.401 builds both staged assembly sets with zero warnings or errors. All 118 managed tests pass on each of net48
(running on Framework 4.8.1) and net10.0-windows: 236 executions. The suites cover owned process trees, quoting, concurrent pipe draining,
cancellation, logging ownership, file replacement, subnet edge cases and IPv4/IPv6 loopback connectivity.

Full Pester runs pass with 913 tests and six existing skips on PowerShell 7.6.6, and 916 tests and three existing skips on Windows
PowerShell 5.1.26100.9444. The final scalar-log and fixed-size-collection boundary refinements are followed by fresh staged-module
comparisons in both hosts. Metadata/help checks cover all 28 compiled commands; behavior checks cover 79 registry and 66 foundation
observations per host. Privileged or destructive integration gates were not enabled.

## Remaining work

Default-adapter selection, remote administration, all policy/tool workflows, other system domains and Office still need their full C#
cutovers. Twelve basic functions, including `Show-Color` and ten networking functions, need a
[binding compatibility decision](compatibility-exceptions.md) before cutover. Do not treat partial domain foundations as completed domain
migrations. The native runtime matrix, privileged registry gates, downstream workflows and comprehensive XML documentation remain release
requirements.
