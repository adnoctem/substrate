# Outstanding work

Implementation ownership has moved to C#. Passing automated tests establishes the covered contracts, not production readiness for every
native Windows workflow. Keep evidence specific to the package, host, identity and scenario tested.

## Controlled live validation

Use explicitly scoped disposable or recoverable environments. Ordinary verification must not install Office, service Windows or mutate remote systems.

- **Office and Outlook:** validate actual acquisition, installation, migration, cancellation, recovery and supported repair-tool workflows.
  Cover retained products/add-ins, bilingual language order, different source IDs, newer targets, SYSTEM identity, UNC media and
  offline-only payloads. Independently check effective policy and preferences. Exercise existing ANSI/Unicode PST attachment, reuse,
  cleanup, duplicate display names, archive/optimization and legacy MAPI with disposable data. Historical PSFoundation pilots are not
  acceptance evidence for substrate. Quick/Online Repair, automatic rollback and uncertain installer replay remain unsupported.
- **Windows Update:** scan, select exact identities, hide/unhide, install/uninstall, cancel and inspect reboot reporting on a VM. Add
  focused coverage for callback/error/cancellation combinations.
- **AppX, Store and WinGet:** install, register, provision, reset and remove a disposable signed package; check all-user inventory and
  protection rules, DISM variants and interruption. Validate capability-restricted Store access separately from AppX inventory. Record
  WinGet/provider versions and repository agreement/trust behavior.
- **Directory, remoting and certificates:** authenticated remote discovery, unavailable WSMan/CredSSP endpoints, failed credentials and
  cancellation; loaded-user certificate stores and permission failures. Sign disposable scripts with a test certificate and timestamp
  service, never a production signing key.
- **Registry and filesystem authority:** elevated binary hive save/restore/load/unload, remote registry, SACL and owner changes; drive
  mapping, labels, font installation, restrictive ACLs and partial failures. Never use the real default-user hive as an expendable fixture.
- **Runtime matrix:** clean runtime-only x64 Windows installations, native .NET Framework 4.8 as distinct from 4.8.1, supported PowerShell
  versions and interactive confirmation hosts. Additional architectures require an explicit support decision and evidence.

## Focused follow-up coverage

- Native network provider unavailability and failure, localized messages and selected conversion/culture edge cases.
- Module provider dependency/trust failures and prerelease restore manifests.
- Credential-pair replacement failure and concurrent filesystem changes; the legacy two-file AES format is neither authenticated nor transactional.
- Locale-specific SFC diagnostics and bounded CBS-tail reading; task, firewall and group-policy failures after live smoke tests.
- Review consumer scripts that match complete PowerShell error identifiers: compiled cmdlets can change the implementing-context suffix.

## Automation and distribution

- Validate hosted vendor and PowerShell dependency proposal workflows independently of ordinary green CI. Inspect downloaded candidates
  without executing them; review changes before merging.
- Configure NuGet ownership and trusted publishing, then validate the first authorized PSGallery/NuGet release and hosted Super-Linter run.
  Local packaging and verification do not establish successful publication.
- Coordinate winkit's module-identity migration with its maintainer and agent; keep that work separate from this repository.
- Make the release decision after reviewing the remaining evidence. Publishing remains an explicit, gated operation.

See [MIGRATION.md](MIGRATION.md) for project history and accepted design decisions, and [CONTRIBUTING.md](CONTRIBUTING.md) for the verification workflow.
