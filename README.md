<p align="center">
    <!-- substrate -->
    <picture>
      <img src="https://raw.githubusercontent.com/adnoctem/artwork/refs/heads/main/projects/substrate/icon/substrate.svg" alt="substrate Logo" width="256">
    </picture>
    <h1 align="center">substrate</h1>
</p>

[![License](https://img.shields.io/github/license/adnoctem/substrate?label=License)][license]
[![Language](https://img.shields.io/github/languages/top/adnoctem/substrate?label=C%23)][dotnet]
[![PSGallery Version](https://img.shields.io/powershellgallery/v/AdNoctem.Substrate.PowerShell)][psgallery_package]
[![CI Status](https://github.com/adnoctem/substrate/actions/workflows/testing.yaml/badge.svg)][testing_workflow]
[![GitHub Release](https://img.shields.io/github/v/release/adnoctem/substrate?label=Release)][github_releases]
[![GitHub Activity](https://img.shields.io/github/commit-activity/m/adnoctem/substrate?label=Commits)][github_commits]
[![Semantic Release](https://img.shields.io/badge/Semantic_Release-gated-yellow?logo=semanticrelease&logoColor=E5E4E7)][semantic_release]
[![Renovate](https://img.shields.io/badge/Renovate-enabled-brightgreen?logo=renovate&logoColor=1A1F6C)][renovate]
[![PreCommit](https://img.shields.io/badge/PreCommit-enabled-brightgreen?logo=precommit&logoColor=FAB040)][precommit]

> [!WARNING]
>
> During pre-1.0 development (`0.x`), this project will regularly introduce breaking changes without a major version bump.
> Public APIs, file layouts, and command-line interfaces may change between releases. Pin a release for reproducible builds
> and review its release notes before updating. Stable compatibility guarantees will begin with version `1.0.0`.

`substrate` is an open-source [MIT][license]-licensed library of reusable C# APIs, maintained by the [Ad Noctem Collective][org], for
Windows administration, configuration management, and automation. Applications can use the domain libraries without loading PowerShell.

The PowerShell adapter, `AdNoctem.Substrate.PowerShell`, supports Windows PowerShell 5.1 and PowerShell 7 on x64 Windows. The module and
all twelve domain libraries are published and available to use as 0.x releases. Their APIs are still evolving; stable compatibility
guarantees begin at 1.0.0. The predecessor, PSFoundation 1.8.7, remains available for existing scripts. Document generation is a
PowerShell 7 (`Core`) capability: the `Documents` domain and its Open XML dependency are staged only in that edition. Windows PowerShell
5.1 keeps the existing command surface but cannot load the document-generation types.

PSGallery distributes the complete module as `AdNoctem.Substrate.PowerShell`. NuGet distributes twelve separate domain packages:
`AdNoctem.Substrate.Core`, `.Diagnostics`, `.Documents`, `.IO`, `.Interop`, `.Networking`, `.Office`, `.Packages`, `.Policies`, `.Registry`, `.Security`,
and `.Windows` (each with the `AdNoctem.Substrate` prefix). There is no umbrella `AdNoctem.Substrate` package; reference the domains you use.
All packages share the same release version.

The implementation consists of reusable C# domain libraries and a PowerShell adapter. The adapter retains the original 188 commands and
four aliases and adds new commands as the libraries evolve, using compiled cmdlets and a small compatibility script. Comparison tests
retrieve the frozen PSFoundation v1 scripts from Git history.

See the [documentation sources](docs/www/index.md), [architecture](docs/www/architecture.md), [migration history](docs/MIGRATION.md), and
[contributor guide](docs/CONTRIBUTING.md). Build the searchable .NET and PowerShell reference with the `Docs` target; open
`build/docs/site/index.html`.

## Installation

Install the published [PowerShell module][psgallery_package] from PSGallery. Pin a reviewed release for reproducible automation; this
example uses 0.2.1:

```powershell
Install-Module -Name AdNoctem.Substrate.PowerShell -RequiredVersion 0.2.1 -Repository PSGallery -Scope CurrentUser
Import-Module AdNoctem.Substrate.PowerShell -RequiredVersion 0.2.1
```

For .NET applications, reference the domain packages you need from NuGet; see [Using the .NET libraries](docs/www/guides/libraries.md).
Review the [release notes][github_releases] before changing a pinned version. Published packages do not require building this repository.

## Development

```powershell
# Restore pinned tools and dependencies.
dotnet msbuild tools/tasks.proj -t:Restore

# Format, validate, test both PowerShell hosts, document, and package.
dotnet msbuild tools/tasks.proj -t:Format
dotnet msbuild tools/tasks.proj -t:Verify

# Import the staged package in a fresh PowerShell process.
Import-Module ./build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1

# Check the same hooks used in CI.
pre-commit run --all-files
```

The SDK is selected by `global.json`. Developer tasks use PowerShell 7 and Bun; packaged commands support Windows PowerShell 5.1 and
PowerShell 7 on x64 Windows. Build output stays under `build/`, archives under `dist/`. Publishing remains separately gated.

### Contributing

Contributions are welcome via GitHub's Pull Requests. Fork the repository and implement your changes within the forked repository, after
that you may submit a [Pull Request][gh_pr_fork_docs]. Refer to our [documentation for contributors][contributing] for contributing
guidelines, commit message formats and versioning tips.

### Maintainers

This project is owned and maintained by [Ad Noctem Collective](https://github.com/adnoctem) refer to the [`AUTHORS`][authors] or
[`CODEOWNERS`][owners] for more information. You may also use the linked contact details to reach out directly.

### Copyright

_Assets provided by:_ **[Microsoft Corporation][microsoft]**

<!-- File references -->

[license]: LICENSE
[contributing]: docs/CONTRIBUTING.md
[authors]: .github/AUTHORS
[owners]: .github/CODEOWNERS

<!-- General links -->

[org]: https://github.com/adnoctem
[microsoft]: https://www.microsoft.com/
[gh_pr_fork_docs]: https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/creating-a-pull-request-from-a-fork
[github_releases]: https://github.com/adnoctem/substrate/releases
[github_commits]: https://github.com/adnoctem/substrate/commits/main/
[psgallery_package]: https://www.powershellgallery.com/packages/AdNoctem.Substrate.PowerShell
[testing_workflow]: https://github.com/adnoctem/substrate/actions/workflows/testing.yaml

<!-- Third-party -->

[semantic_release]: https://semantic-release.org/
[renovate]: https://renovatebot.com/
[precommit]: https://pre-commit.com/
[dotnet]: https://learn.microsoft.com/dotnet/
