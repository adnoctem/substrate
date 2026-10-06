# Office deployment

Office commands separate discovery, media preparation, installation, removal, migration, and maintenance authority. A plan is a reviewable
snapshot; execution rechecks the machine and media. These APIs are intended for thin orchestration wrappers such as winkit's Office scripts.

| Capability             | Commands                                                                                                                                                                         |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Tool provisioning      | `Resolve-OfficeDeploymentToolSource`, `Test-OfficeDeploymentToolSourceAvailability`, `Install-OfficeDeploymentTool`, `Test-OfficeDeploymentTool`, `Get-OfficeDeploymentToolHelp` |
| Discovery and intent   | `Get-OfficeInventory`, `New-OfficeDeploymentConfiguration`, `Get-OfficeDeploymentPlan`                                                                                           |
| Independent assessment | `Test-OfficeDeployment`, `Get-OfficeActivationStatus`, `Test-OfficeDeploymentMedia`                                                                                              |
| Media preparation      | `Save-OfficeDeploymentMedia`                                                                                                                                                     |
| Product lifecycle      | `Install-Office`, `Uninstall-Office`, `Switch-OfficeDeployment`                                                                                                                  |
| Maintenance            | `Update-Office`, `Set-OfficeUpdateConfiguration`, `Add-OfficeLanguage`, `Remove-OfficeLanguage`, `Set-OfficeApplicationSelection`, `Set-OfficeApplicationPreference`             |
| Recovery               | `Get-OfficeDeploymentRecovery`, `Resume-OfficeInstallation`, `Resume-OfficeMigration`                                                                                            |

```powershell
$configuration = New-OfficeDeploymentConfiguration `
  -TargetProductId Standard2024Volume `
  -Architecture 64 `
  -Language en-us,de-de

# Prepare is the only deployment operation that downloads Office payloads.
# The destination's parent must exist; a package is published only after verification.
$media = Save-OfficeDeploymentMedia `
  -Configuration $configuration `
  -SourcePath 'C:\Deployment\Office2024' `
  -OdtPath 'C:\Tools\ODT\setup.exe' `
  -Confirm

$plan = Get-OfficeDeploymentPlan `
  -Action Install `
  -Configuration $configuration `
  -SourcePath $media.Path

$plan | Format-List Action, State, Eligible, Blockers, Warnings, LanguageTransition
$plan | Install-Office -OdtPath 'C:\Tools\ODT\setup.exe' -WhatIf
```

ODT acquisition follows the same resolve/check/install separation as LGPO. `Resolve-OfficeDeploymentToolSource` returns a reviewed,
versioned Microsoft URL without network access; `Test-OfficeDeploymentToolSourceAvailability` probes it with HEAD. A reachable URL is not
signature validation. `Install-OfficeDeploymentTool` downloads the self-extracting package, checks its Microsoft Authenticode signature
before running extraction, validates the extracted `setup.exe`, and returns its validation result including `Path`, `Version` and `Valid`.
It provisions the tool only; it does not install Office. Run from an elevated session with an existing destination parent:

```powershell
Resolve-OfficeDeploymentToolSource
Test-OfficeDeploymentToolSourceAvailability
$tool = Install-OfficeDeploymentTool -Destination 'C:\Tools\ODT' -Confirm
Test-OfficeDeploymentTool -OdtPath $tool.Path
```

`-WhatIf` and `-DryRun` acquire nothing. An existing validated tool is reused; an untrusted existing file is rejected without replacement.
To acquire the reviewed build separately, select a new dedicated directory. Like LGPO source resolution, this is a reviewed source table,
not a live latest-version lookup. Deployment commands continue to require an explicit `OdtPath` and never silently acquire tools.

Both `Groove` and `OneDrive` remain valid ODT exclusion IDs. `Groove` targets the legacy OneDrive for Business sync client; use `OneDrive`
for the modern sync client. To exclude both during Office deployment, specify `-ExcludeApp Groove,OneDrive` consistently for configuration,
preparation and execution. Changing an existing `Groove`-only selection to both is an intentional application-selection change. See
[Microsoft's modern OneDrive example](https://learn.microsoft.com/en-us/microsoft-365-apps/deploy/overview-office-deployment-tool#exclude-onedrive-when-installing-microsoft-365-apps-or-other-applications)
and [legacy client guidance](https://learn.microsoft.com/en-us/sharepoint/exclude-or-uninstall-previous-sync-client).

Default language is exactly `en-us`, independent of the operating system or account. Explicit lists preserve order; the first language is
the primary shell language. `-AutoSourceLocales` opts into installed-Office discovery; add `-LocaleSource OperatingSystem` to use the
machine installation UI language from `HKLM\SYSTEM\CurrentControlSet\Control\Nls\Language:InstallLanguage`. This is not the user's display
language, keyboard layout, or regional format. Ambiguous installed-Office language evidence is a blocker rather than an implicit fallback.
For multiple Click-to-Run products, automatic sourcing requires complete per-product language observations and one agreed primary language;
it preserves that primary first and includes the union of observed languages. Conflicting primaries, unknown inventory, and legacy MSI
sources require explicit language intent. Explicit `-Language` and `-AutoSourceLocales` are mutually exclusive. UI languages, language
interface packs, and proofing resources are distinct; this discovery does not establish complete MSI or proofing-resource coverage.

`Get-OfficeInventory` retains App Paths observations in `AppPathEvidence`: registry view/key, raw and resolved target, `State` and `Reason`.
Confirmed missing executable references do not by themselves count as Click-to-Run residue. Present or uncertain Click-to-Run references
still block when configuration is absent, as do genuine remaining Click-to-Run registry artifacts. Discovery never removes registry entries.
Path probing uses literal local fixed-drive paths, bounded environment expansion by registry view and ancestor checks; network paths,
reparse points, access failures and ambiguous targets cannot establish absence. Windows system paths and short-name aliases in 32-bit
PowerShell on x64 Windows remain uncertain to avoid redirection errors. Absolute Office installation paths are not remapped according to the
invoking process's bitness.

An ordinary migration journal at completed `Remove` can continue with `Resume-OfficeMigration` after fresh inventory establishes removal.
Recovery hashes its historical `Before` snapshot unchanged, revalidates current state and media, and intersects original removal IDs with
current products. When none remain, continuation installs without repeating removal and writes a new run/journal, preserving the original.
Use a fresh recovery descriptor after reloading an updated module; preview first and supply a fresh `SecureString` key for execution when
needed. This does not enable replay of uncertain partial installations or historical pilot journals.

Executors accept only their matching plan action. `Uninstall-Office` requires exact `RemoveProductId` selections. `Switch-OfficeDeployment`
alone combines selected removal and installation, including explicitly authorized broad MSI removal. Updates preserve other deployment
dimensions; language operations preserve the primary language. An intentional primary-language replacement belongs to migration.
`Set-OfficeUpdateConfiguration` accepts explicit `Enabled`, `UpdatePath`, `TargetVersion`, and `Channel` settings. Deadlines are
deliberately excluded because they can forcibly close applications later. Application preferences accept validated Office
`REG_SZ`/`REG_DWORD` records under `Settings.Preferences`, with `Key`, `Name`, `Value`, `Type`, `App`, and `Id`; their scope includes
existing and future users.

All mutation commands support `-WhatIf`; deployment commands also support `-DryRun`. Previews create no files, journals, or logs and never
start an installer or stop applications. A compliant no-op returns `AlreadyCompliant`; an absent removal selection returns `AlreadyAbsent`.
`-Confirm:$false` acknowledges the displayed scope but does not disable validation or language warnings. The module never exits the host,
reboots it, invokes registry uninstall strings, or accepts arbitrary ODT XML or command-line arguments.

Media schema 2 records the pinned build, product, channel, architecture, available languages, tool version, and complete payload hashes.
Deployment language selection can be a subset of the available languages. Old `winkit-office-media.json` schema-1 packages require explicit
preparation into a new directory. Files and manifests require Administrators/SYSTEM ownership and write access; hashes do not authenticate
an attacker-replaced manifest. Local/UNC media must be accessible to the actual execution identity. Recovery records must remain local.

Media metadata can be `Office/Data/v<architecture>.cab` or `Office/Data/v<architecture>_<pinned-version>.cab`, with the exact architecture
and manifest build. Neutral and every declared language stream remain required. Missing metadata or neutral content reports `MissingMedia`;
missing language content reports `MissingLanguageMedia`. The readable `Error` lists missing paths and, when metadata is absent, both CAB
alternatives. Failed preparation preserves that diagnostic while removing temporary downloads and leaving the destination unpublished. This
validates package structure and integrity; native local-source behavior still needs validation with the selected ODT and build. Execution
keeps an exact version and `AllowCdnFallback=FALSE`. Microsoft notes that absent generic CAB metadata can lead to CDN downloads in some
deployment flows; see
[package anatomy and installation options](https://learn.microsoft.com/en-us/microsoft-365-apps/best-practices/install-options).

Operation results have `SchemaVersion = 1` and include `RunId`, `Action`, `Phase`, `Status`, `ReasonCode`, `Before`, `After`,
`Verification`, `Configuration`, `LanguageTransition`, `Activation`, `NativeResults`, `RebootRequired`, `RecoveryRequired`, `RecoveryPath`,
`LogPaths`, and cleanup details. `Changed = $null` with `ChangeKnown = $false` means the outcome is uncertain, including after an invoked
installer fails. Installation verification and activation are independent. Wrappers can use `WrapperExitCode` (`0`, `1`, or `3010`) while
retaining native exit codes. Use `ConvertTo-Json -Depth 30` for the complete nested result. Do not flatten unknowns into successful
compliance.

Workflow results also include `Execution` (`ModuleVersion`, `ModulePath`, `PowerShellVersion`, `PowerShellEdition`, `ProcessBitness`) from
the executing module and process. Journal result/log metadata retains this context; an earlier preparation report does not establish the
version used for a later execution. This is additive within schema 1 and does not rewrite historical inventory or journal fingerprints.

`Diagnostic` adds safe structured detail where available. Media trust failures identify the object/path, owner or write-grant SID, rights,
and inheritance. Recovery validation failures identify the read/JSON/schema/identity/context/settings/fingerprint stage and safe error
category; direct reader errors expose this under `Exception.Data['OfficeDiagnostic']`, alongside the existing `OfficeReason`. Malformed
journal contents and raw parser exception text are not echoed. These diagnostics do not repair ACLs or relax validation. New plans retain
the assessment under optional `Media` metadata. Preparation, plan revalidation, staging and recovery preserve its safe diagnostic on
validation failure; a valid assessment with only a changed fingerprint does not become an ACL/JSON error. Older plans and journals without
`Media` remain readable. Recorded assessments are informational; execution and recovery always validate media afresh.

Recovery journals are written atomically under `%ProgramData%\PSFoundation-Office` by default. They contain no product keys. A key is
accepted only as `SecureString` and materialized in protected temporary XML for ODT; it is never placed on a process command line. Secure
erasure of storage and redaction of ODT's own logs cannot be guaranteed. Cleanup failures retain the original error and report protected
residue.

```powershell
$recovery = Get-OfficeDeploymentRecovery -RunId $result.RunId
$recovery | Resume-OfficeInstallation -OdtPath 'C:\Tools\ODT\setup.exe' -WhatIf
# Migration journals require Resume-OfficeMigration and fresh confirmation of remaining scope.
```

**Host requirements:** ordinary native execution accepts x64 Windows 10 22H2 (build 19045) and later Windows desktop hosts, including
Windows 11. Older Windows 10 builds, Windows Server and ARM hosts remain outside this backend's scope. Elevated 64-bit PowerShell, no
pending reboot, and no active deployment are still required. Product IDs are deployment identifiers, not a claim of current vendor lifecycle
support. Routine tests mock ODT and do not certify any real Office installation. Standalone MSI removal is unsupported. Recovery supports
verification of completed installations, pre-launch continuation, and migration continuation after verified Click-to-Run removal. Replaying
an uncertain partial installer returns `UnsupportedRecoveryState`; Quick Repair, Online Repair, rollback, and journal-free mutation are not
implemented.

ODT execution deliberately has no forced timeout or cancellation kill policy: `setup.exe` may delegate work to shared services. Tests of
generic process cancellation and the ODT download extractor do not establish safe cancellation of a live Office installation. Application
closure requires explicit consent. A held deployment lock or a changed inventory blocks execution before any installer launch.

| Recovery observation                                                      | Decision                                                                                  |
| ------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Fresh state fully verifies the recorded destination, including activation | Complete verification without another ODT invocation                                      |
| Supported pre-launch checkpoint                                           | Revalidate authority, host, media and state; preview/continue the original operation      |
| Migration checkpoint after confirmed completed C2R removal                | Continue installation of the recorded destination without repeating removal               |
| Busy deployment or pending reboot                                         | Stop and wait/resolve the reported prerequisite                                           |
| Uncertain partial installation or unverified unsupported checkpoint       | `UnsupportedRecoveryState`; inspect native state/logs before a separately reviewed action |

These are decisions constrained by the recorded action and current gates, not permission to replay any failed journal.

Inventory resolves installed builds in evidence order:

1. Microsoft's documented `ClickToRun\Inventory\Office\16.0:OfficePackageVersion`, with matching `OfficeProductReleaseIds`.
2. When that key or version value is absent, agreeing `Version` values under
   `ClickToRun\ProductReleaseIDs\<ActiveConfiguration>\<ProductId>.16\<resource>`. The selected product must register `x-none` and at least
   one language; every listed resource must have exactly one valid four-part version, and all must agree in the same registry view.

`Products[].VersionSource` identifies `ClickToRunInventory` or `ActiveProductResources`; `Evidence` records the selected path or why the
version remains unknown. Resource versions are retained in `RegisteredResources`. Missing evidence can fall through; malformed documented
versions, conflicting product identities, and duplicate inventory records cannot. Inactive trees, another product's resources, shared
`culture` leaves alone, and `VersionToReport` cannot supply the fallback. Valid documented inventory takes precedence over resource data.

The fallback is based on Office Standard 2019 x86 and x64 observations whose resource and collected binary versions agree. It recognizes
that registry structure without a product-year allowlist; other releases still need native validation. It is a derived observation, not a
Microsoft-documented contract or proof of application health. No assumption is made that every pre-Microsoft-365 product lacks the inventory
key. Microsoft's
[installed-version guidance](https://learn.microsoft.com/en-us/microsoft-365-apps/updates/microsoft-guidance-on-office-build-install)
explicitly excludes telemetry as installation-state evidence.

`Languages` is derived from the active per-product culture registrations. A single registered language supplies `PrimaryLanguage`; with
multiple registered languages, the current reader uses `ClientCulture` only when it belongs to that set. Missing or ambiguous observations
remain unknown. These inference rules still need broader native validation, especially bilingual installations. Per-user language
preferences and requested XML are not installation evidence. `VerificationLimitations` reflects unresolved language fields on observed
Click-to-Run products. A clean or MSI-only source can now reach an eligible ordinary plan; that is not a promise of verified
post-installation compliance. Installed build and application exclusions can still be unknown, and legacy MSI automatic locale sourcing is
not implemented. `VersionToReport` is not substituted for an absent installed-build observation. Update-policy and preference execution
returns `AppliedUnverified` until effective settings can be independently verified. Validate these scenarios on separately authorized
disposable VMs before production adoption; a mocked passing suite is not that evidence.

Inventory distinguishes known Click-to-Run infrastructure and known Office add-ins in `RelatedComponents` from legacy Office entries in
`Msi`. Orphaned Click-to-Run infrastructure remains an unknown state. Known add-in registrations must survive deployment; this check does
not establish add-in compatibility with the destination architecture. Unrecognized Office components remain subject to conservative
classification and blocking. Infrastructure classification requires a Microsoft publisher, an Office 16 product-code structure, and a
specific Click-to-Run Licensing, Extensibility or Localization component name. It does not rely on a list of component SKU fragments such as
`007E` or `008F`. Both have been observed for Licensing Components; neither is canonical. Actual product codes remain in inventory as
identities. Unrecognized names or other Office families are not automatically treated as infrastructure. `RegisteredLanguages` retains the
culture registrations used by language inference; `LanguageEvidence` preserves machine language observations separately from user
preferences.

These inventory fields are additive within schema 1. Existing plans must be recreated after inventory changes; execution revalidates current
observations. Existing recovery journals remain subject to their original authority and the current verification gates.

Per-product `ExcludedApps` registration is observed separately from requested configuration: an explicitly empty string yields an empty
`ExcludeApp` list; an absent, null or malformed value stays unknown. Valid strings are normalized for whitespace, case, order and
duplicates, with the selected registry value recorded in `Evidence`. Missing data does not prove every application is installed, and shared
executable presence alone cannot establish a SKU's application selection. No unsupported fallback is inferred from the requested XML.

## Migration and historical pilot records

Migrations use ordinary schema-1 plans and `Switch-OfficeDeployment`. The former `-PilotMigration` option and scenario-specific
host/resource assumptions have been removed. Select source Click-to-Run products explicitly with `-RemoveProductId`; use `-RemoveMsi` only
when authorizing supported legacy MSI removal. Missing observations still prevent verified compliance.

Historical schema-2 pilot journals remain inspectable with `Get-OfficeDeploymentRecovery`, but resume commands reject them with
`UnsupportedPilotRecovery`. They cannot be executed as ordinary plans or converted into new removal authority. A completed installation with
failed verification must not be replayed to change its reporting status.

Historical PSFoundation pilot results are summarized in the repository's [migration history](https://github.com/adnoctem/substrate/blob/main/docs/MIGRATION.md). They do not establish native acceptance of the C# rewrite. See the [remaining validation work](https://github.com/adnoctem/substrate/blob/main/docs/TODO.md).
