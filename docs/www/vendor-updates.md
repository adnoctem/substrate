# Vendor update proposals

## Initial LGPO provenance

On 2026-10-04 the maintainer supplied the original LGPO ZIP and extracted files. The archive contained exactly `LGPO_30/LGPO.exe`,
`LGPO_30/LGPO.pdf` and `LGPO_30/Microsoft Security Compliance Toolkit - Standalone Use Terms.pdf`. The contained executable matched the
separately supplied binary byte-for-byte. Windows reported a valid Microsoft Authenticode signature and Microsoft Time-Stamp Service
countersignature. No vendor executable was run or policy applied. This review established the pins in
<xref:AdNoctem.Substrate.Policies.LgpoSource.Standalone>; it did not compare a fresh download. The original sizes, digests and signer
thumbprint remain in the historical migration checkpoint in Git. A signer thumbprint is review evidence, not a permanent rotation policy.

## Proposed updates

Scheduled checks inspect the official Microsoft LGPO and Office Deployment Tool download pages. Downloads must stay on HTTPS
`download.microsoft.com`, including every download redirect, and are bounded by size and time limits. LGPO ZIP entries are checked for
unsafe paths, duplicates, and excessive sizes before the executable is inspected.

Candidates must have a valid, timestamped Microsoft Authenticode signature. No candidate executable is run by the update workflow. The LGPO
proposal includes both archive and executable digests. ODT inspection covers the signed distribution executable; the ordinary ODT
installation API separately validates the extracted setup executable before use.

`./tools/vendor-updates.ps1` only writes evidence under `build/vendor-tools`. `-Propose` also prepares source changes locally. The scheduled
workflow runs Verify before creating or updating its fixed automation branch and pull request. Human review is required before merging new
pins. No hashes are silently accepted and no PR is automatically merged.

Download-page changes, ambiguous links, signature failures, and unexpected package layouts fail the check for manual investigation. Upstream
checks are separate from ordinary unit tests. Offline tests cover rejected origins and archive paths without network access. The existing
Renovate configuration covers supported package/tool manifests; the dependency workflow handles exact PowerShell module pins.
