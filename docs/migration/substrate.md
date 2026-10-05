# Substrate migration

The PSFoundation rewrite at commit `5a66588` was cloned with complete ancestor history into the independent local repository
`D:\Software\AdNoctem\substrate`. Its development branch is now `main`, and origin is configured as `git@github.com:adnoctem/substrate.git`.
No GitHub repository has been created and nothing has been pushed or published.

Substrate uses `AdNoctem.Substrate` for C# namespaces and assemblies. The PowerShell package is `AdNoctem.Substrate.PowerShell`, with a new
module GUID and an independent release sequence starting at 1.0.0. PSFoundation release tags are not imported. The 188 command names and
four aliases remain unchanged. NuGet distribution and the winkit dependency migration are separate follow-up tasks.

## Preserved compatibility identities

- The frozen `src/*.ps1`, original manifest/module, root-level v1 tests, and historical migration records retain PSFoundation identity.
- Baseline tests materialize PSFoundation 1.8.7 from Git commit `d2d1498275806684b44169504146302d54b7a084`; full history is required in CI.
- Registry policy and Outlook context PowerShell type tags retain their existing names; they are compatibility markers, not C# namespaces.
- Office recovery schemas, fingerprints, the `Global\PSFoundation.OfficeDeployment` lock, and `PSFoundation-Office` storage remain stable.
- Existing default log/tool storage names remain stable. Renaming disk state belongs in an explicit future migration.

The old repository remains intact at `D:\Software\AdNoctem\PSFoundation`. Its local `main` contains the release-freeze commit which has not
been pushed. The local successor notice must not claim that substrate is already available on GitHub or a package feed.

## Audit before remote setup

Review the rename, tooling, and repository settings locally. After separate authorization, create the empty public repository, push main,
and verify hosted CI before enabling any publishing. Do not import old release tags or force-push the predecessor. Keep winkit on 1.8.7
until its own migration is validated. Do not archive either repository during this transition.
