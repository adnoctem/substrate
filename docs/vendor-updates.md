# Vendor update proposals

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
