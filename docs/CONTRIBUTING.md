# Contributing

Substrate uses C# domain libraries with a PowerShell adapter. Compatibility tests retrieve the frozen v1 scripts from Git history under `build/baseline`.

## Prerequisites

Develop on x64 Windows with the SDK selected by `global.json`, .NET Framework 4.8 or later, PowerShell 7.4+, Windows PowerShell 5.1, Bun
1.4.2, Git, and pre-commit 4.6.0. The SDK version is a development requirement; importing the module does not require the SDK or DocFX.
Framework tests on a machine with 4.8.1 exercise that installed runtime, not a separate 4.8 installation.

```powershell
dotnet msbuild tools/tasks.proj -t:Restore
pre-commit install
dotnet msbuild tools/tasks.proj -t:Verify
```

Restore uses NuGet lock files, the local .NET tool manifest, and exact PowerShell tool versions in `tools/dev-dependencies.json`.
PowerShell tools are restored under `build/tools/modules`; no installation of the legacy runtime dependencies is needed.

## Daily workflow

| Task                          | Command                                             |
| ----------------------------- | --------------------------------------------------- |
| Compile                       | `dotnet msbuild tools/tasks.proj -t:Build`          |
| Stage the module              | `dotnet msbuild tools/tasks.proj -t:Stage`          |
| Run managed tests             | `dotnet msbuild tools/tasks.proj -t:TestManaged`    |
| Run packaged-module tests     | `dotnet msbuild tools/tasks.proj -t:TestPowerShell` |
| Build help and reference      | `dotnet msbuild tools/tasks.proj -t:Docs`           |
| Refresh tracked help metadata | `dotnet msbuild tools/tasks.proj -t:UpdateHelp`     |
| Apply formatting              | `dotnet msbuild tools/tasks.proj -t:Format`         |
| Check style and analysis      | `dotnet msbuild tools/tasks.proj -t:Check`          |
| Create distribution packages  | `dotnet msbuild tools/tasks.proj -t:Pack`           |
| Test NuGet consumers          | `dotnet msbuild tools/tasks.proj -t:TestPackages`   |
| Complete verification         | `dotnet msbuild tools/tasks.proj -t:Verify`         |

The default configuration is Release. Use `-p:Configuration=Debug` when needed. Restore explicitly before running these tasks on a clean
checkout. Build compiles only; Stage assembles the module; HelpFiles adds runtime help. Pack includes staging and help through target
dependencies. Direct `dotnet build Substrate.slnx` remains supported for ordinary compilation.

Projects use short paths such as `src/IO/IO.csproj` and `tests/IO.Tests/IO.Tests.csproj`.
`Directory.Build.props` derives the assembly and resource namespace from the stable `AdNoctem.Substrate` prefix and project name.
NuGet package IDs default to those assembly names. C# namespace declarations remain explicit; Git remotes do not affect package identity.

For a focused Pester investigation, the supporting runner accepts `./tools/test.ps1 -Path ./tests/PowerShell/Registry.Tests.ps1`. It
defaults to the current suite. PowerShell source coverage measures compatibility scripts only and is
informational, not C# coverage.

## Formatting and hooks

C# and project files use the pinned CSharpier tool with its defaults; PowerShell uses two spaces.
The local pre-commit hooks restore .NET tools and run CSharpier on changed files. `Format` and `Check` use the same tool for the repository.
No repository-wide line ending is required. The PowerShell formatter preserves existing line endings and BOMs.
Scripts containing non-ASCII text and executed by Windows PowerShell 5.1 still need UTF-8 with BOM; staging adds it to shipped scripts.
Prettier runs unpinned through `bun x`, uses the Bun cache instead of a repository `node_modules`, and formats all Markdown, including command help.
The hook selects tracked Markdown; command-line formatting honors `.gitignore`.

Run `pre-commit run --all-files` before committing. The hooks check PowerShell and C# style, Markdown, spelling, and workflow configuration.
They do not build the entire documentation site or run the test suite at every commit. CI runs the same hooks and MSBuild targets. Both
PowerShell editions are tested as runtime hosts, without repeating every managed test per shell. A separate Super-Linter workflow retains
the shared PowerShell analysis checks on Ubuntu.

## Documentation

See [documentation maintenance](www/documentation.md). XML comments describe the reusable .NET API. PowerShell Markdown describes command
behavior, while compiled metadata supplies parameter binding details. The [site overview](www/index.md) introduces the generated reference
and workflow guides. Build the
site under `build/docs/site`; no site is published by the build.

When changing commands, update the owning catalog, implement the adapter, run UpdateHelp, and review the resulting command pages. Keep
non-obvious defaults, exceptions, ownership, platform requirements, and cancellation behavior documented. Prefer representative examples and
focused functional tests to repetitive property documentation and implementation-mirroring tests.

## Packaging and releases

Import the staged module in a fresh process:

```powershell
Import-Module ./build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1
Get-Help Get-OfficeInventory -Full
```

A rebuild cannot unload assemblies already imported by an existing PowerShell process. Keep the loader, both binary directories, helper
executable, configuration, data, and help together when copying the package.

Pack writes module archives under `dist/` and eleven `AdNoctem.Substrate.<domain>` NuGet packages under `dist/nuget/`, with SHA256 checksums
for both. The PowerShell adapter and Windows Runtime helper are not separate NuGet packages. The Packages library carries its helper and
transitive build targets; see the [.NET libraries guide](www/guides/libraries.md).
Verify restores the packages into isolated .NET Framework and modern .NET consumers, then publishes and runs those consumers locally
to check transitive dependencies and helper deployment. These tests do not invoke the helper or perform administrative operations.

Semantic-release generates CHANGELOG.md from commits; do not maintain it by hand. It passes its selected version to `tools/release.ps1
-Prepare`, which verifies and packages that version. Substrate starts an independent release sequence at 1.0.0; PSFoundation release tags
are not imported. `main` and `next` retain their existing release-branch configuration; neither is a prerelease branch. The release workflow remains manual and requires
`SUBSTRATE_RELEASE_ENABLED=true` on either branch. The final dispatch job in Testing is commented out. If enabled later, it supplies the
tested commit and Release rejects a branch that has advanced since that test.

Before enabling publication, configure:

- `PSGALLERY_TOKEN`, a repository secret containing the PSGallery API key for `AdNoctem.Substrate.PowerShell`.
- `NUGET_USER`, a repository variable containing the personal NuGet username used to authenticate, even when an organization owns packages.
- A [NuGet trusted publishing policy](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) for GitHub owner `adnoctem`,
  repository `substrate`, workflow filename `release.yaml`, and package pattern `AdNoctem.Substrate.*`. The workflow does not use an environment.
  NuGet/login exchanges the job's OIDC identity for a temporary API key; no stored NuGet API key is required in GitHub.
- Optionally `ANC_GITHUB_TOKEN` when repository rules require a dedicated release identity; otherwise the workflow uses `GITHUB_TOKEN`.
  That identity must be allowed to push the generated changelog commit and release tags under the repository's branch rules.

A NuGet organization is optional and supports shared package ownership. It does not reserve the `AdNoctem.*` name; prefix reservation is
a separate application to NuGet. Complete ownership and publishing-policy setup before enabling the release gate.

```powershell
./tools/release.ps1 -Prepare -Version 1.0.0 -DryRun
./tools/release.ps1 -Publish -Version 1.0.0 -DryRun
```

Actual publication validates the complete prepared file set and its hashes, then publishes the NuGet libraries and PSGallery module
before attaching assets to the GitHub release. Local publishing reads `NUGET_API_KEY` and `PSGALLERY_API_KEY` from the environment only.
Never put keys in command-line arguments or MSBuild properties. Ordinary Verify does not publish.

Publication across packages and feeds is not transactional. The workflow retains `build/release.json`, the staged module and `dist/`
as an artifact for 14 days, including on failure. If publication stops partway through, inspect the feeds, restore these directories
under the matching checkout, and run `./tools/release.ps1 -ValidateOnly -Version <version>`. Retry only missing NuGet IDs with
`-Publish -Destination NuGet -PackageId <id1>,<id2>`, or the module with `-Publish -Destination PSGallery`, using the same version and fresh
credentials. Do not rebuild or overwrite a published version. Inspect the release commit/tag and finish the GitHub release separately if
semantic-release had already created its tag. Scheduled upstream checks remain separate from ordinary tests and publication.

## Commit conventions

Use `type(scope): summary` with an imperative, lowercase summary and no trailing period. Types: feat, fix, docs, refactor, test, chore,
build, ci. Scopes: src, tools, tests, config, docs. Include a body of at least 20 characters, except for docs commits. Explain the problem,
resulting behavior, and relevant validation. Use a BREAKING CHANGE footer only when an intentional compatibility change requires a major
release.

## Live validation

Keep administrative mutation tests separate from ordinary development checks. Use disposable or recoverable Windows machines for Office
deployment, Windows Update servicing, AppX lifecycle operations, and remote directory or certificate workflows. Track remaining evidence in
[TODO](TODO.md).
