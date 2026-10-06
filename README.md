# substrate

[![License](https://img.shields.io/github/license/adnoctem/substrate?label=License)][license]
[![Language](https://img.shields.io/github/languages/top/adnoctem/substrate?label=C%23)][dotnet]
[![PSGallery Version](https://img.shields.io/powershellgallery/v/AdNoctem.Substrate.PowerShell)][psgallery_package]
[![CI Status](https://github.com/adnoctem/substrate/actions/workflows/testing.yaml/badge.svg)][testing_workflow]
[![GitHub Release](https://img.shields.io/github/v/release/adnoctem/substrate?label=Release)][github_releases]
[![GitHub Activity](https://img.shields.io/github/commit-activity/m/adnoctem/substrate?label=Commits)][github_commits]
[![Semantic Release](https://img.shields.io/badge/Semantic_Release-gated-yellow?logo=semanticrelease&logoColor=E5E4E7)][semantic_release]
[![Renovate](https://img.shields.io/badge/Renovate-enabled-brightgreen?logo=renovate&logoColor=1A1F6C)][renovate]
[![PreCommit](https://img.shields.io/badge/PreCommit-enabled-brightgreen?logo=precommit&logoColor=FAB040)][precommit]

`substrate` is an open-source [MIT][license]-licensed library of reusable C# APIs, maintained by the [Ad Noctem Collective][org], for
Windows administration, configuration management, and automation. Applications can use the domain libraries without loading PowerShell.

The PowerShell adapter, `AdNoctem.Substrate.PowerShell`, supports Windows PowerShell 5.1 and PowerShell 7 on x64 Windows. Its first release
is planned as 1.0.0; it has not been published. The predecessor, PSFoundation 1.8.7, remains available for existing scripts.

The implementation consists of reusable C# domain libraries and a PowerShell adapter. The adapter preserves 188 commands and four aliases,
using compiled cmdlets and a small compatibility script. Comparison tests retrieve the frozen PSFoundation v1 scripts from Git history.

See the [documentation sources](docs/www/index.md), [architecture](docs/www/architecture.md), [migration history](docs/MIGRATION.md), and
[contributor guide](docs/CONTRIBUTING.md). Build the searchable .NET and PowerShell reference with the `Docs` target; open
`build/docs/site/index.html`.

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
