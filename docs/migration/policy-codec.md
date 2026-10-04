# Registry policy codec checkpoint

`PSFoundation.Registry.Policies` supplies a reusable PReg version 1 codec. It uses the shared `RegistryPath` and `RegistryValue` models; it
neither applies policy nor runs LGPO. The caller supplies the registry hive when projecting a relative policy key.

- `RegistryPolicyRecord` retains ordered records, duplicate names, directive names, unknown types and copied raw payloads. Decoding is
  explicit; ordinary values can be projected into the existing typed registry model without losing unsigned integer bits.
- `RegistryPolicyCodec` validates bounded files and payloads, preserves exact raw bytes, and reports malformed-input offsets relative to the
  caller's starting stream position. The caller owns the readable, seekable stream. Cancellation is checked between records; a synchronous
  stream read cannot be interrupted.
- `RegistryPolicyFileService` provides synchronous and asynchronous reads and atomic file publication through `FileManager`. Asynchronous
  reads buffer at most the file-size limit before parsing. All records are validated before publication, parents must already exist, and
  overwrite is explicit. Cancellation before publication preserves the existing destination. This is not a policy-application transaction.

The staged PowerShell package now compiles `ConvertFrom-RegistryPolicy` and `ConvertTo-RegistryPolicy`. Their private script codec helpers
are removed from staging. Remaining LGPO commands still use their frozen implementations. Existing PowerShell help and parameter contracts
remain packaged and compared with v1.

## Compatibility evidence

Fresh-host comparisons cover decoded and raw records, concrete data types, exact file bytes, empty pipelines, duplicate values, directives,
confirmation, existing and locked destinations, malformed records, overflow, cleanup and legacy error envelopes. Managed tests independently
check the recorded binary fixture, every truncated prefix of a record, raw immutability, unsigned conversions, caller-owned streams,
cancellation and destination preservation. The encoder bounds string and sequence payloads before allocating an oversized buffer.

The public C# decoder checks NUL terminators ordinally. The v1 PowerShell decoder used culture-sensitive string suffix checks, which can
ignore NUL under modern globalization and accept malformed strings. That behavior is retained only in the internal compatibility path and
covered against both hosts. Any future correction to the PowerShell behavior needs its own explicit compatibility decision. Broader host
cultures and privileged policy application are not validated by these codec tests.

## Verification

On 2026-10-04, both staged assembly sets build without warnings or errors. The managed suite passes 136 tests per target (272 executions),
including 86 registry tests. Full Pester runs pass with 915 tests and six existing skips on PowerShell 7, and 918 tests and three existing
skips on Windows PowerShell 5.1. Subsequent numeric-error refinements are checked by rebuilding, rerunning the managed tests, and comparing
the final staged package in both hosts. Policy comparisons cover 41 observations per host, alongside the existing registry and foundation
probes. These checks do not validate LGPO execution or machine policy application.
