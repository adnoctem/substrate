# Contributing

PSFoundation v2 uses C# domain libraries with a PowerShell adapter. The frozen v1 scripts remain available for compatibility comparisons;
they are not the production module build input.

## Prerequisites

Develop on x64 Windows with the SDK selected by `global.json`, .NET Framework 4.8 or later, PowerShell 7.4+, Windows PowerShell 5.1, Bun
1.4.2, Git, and pre-commit 4.6.0. The SDK version is a development requirement; importing the module does not require the SDK or DocFX.
Framework tests on a machine with 4.8.1 exercise that installed runtime, not a separate 4.8 installation.

```powershell
dotnet msbuild tools/tasks.proj -t:Restore
pre-commit install
dotnet msbuild tools/tasks.proj -t:Verify
```

Restore uses NuGet lock files, the local .NET tool manifest, exact PowerShell tool versions in `tools/dev-dependencies.json`, and
`bun.lock`. PowerShell tools are restored under `build/tools/modules`; no installation of the legacy runtime dependencies is needed.

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
| Create archives               | `dotnet msbuild tools/tasks.proj -t:Pack`           |
| Complete verification         | `dotnet msbuild tools/tasks.proj -t:Verify`         |

The default configuration is Release. Use `-p:Configuration=Debug` when needed. Restore explicitly before running these tasks on a clean
checkout. Build compiles only; Stage assembles the module; HelpFiles adds runtime help. Pack includes staging and help through target
dependencies. Direct `dotnet build PSFoundation.slnx` remains supported for ordinary compilation.

For a focused Pester investigation, the supporting runner accepts `./tools/test.ps1 -Path ./tests/PowerShell/Registry.Tests.ps1`. It
defaults to the v2 suite; archived root-level tests exercise v1. PowerShell source coverage measures compatibility scripts only and is
informational, not C# coverage.

## Formatting and hooks

C# uses four spaces. PowerShell uses two spaces, CRLF, and UTF-8 with BOM for Windows PowerShell 5.1. The repository formatter and
PSScriptAnalyzer share the checked-in settings. PlatyPS command Markdown and the generated API catalog have their own generation
conventions.

Run `pre-commit run --all-files` before committing. The hooks check PowerShell and C# style, Markdown, spelling, and workflow configuration.
They do not build the entire documentation site or run the test suite at every commit. CI runs the same hooks and MSBuild targets. Both
PowerShell editions are tested as runtime hosts, without repeating every managed test per shell.

## Documentation

See [documentation maintenance](documentation.md). XML comments describe the reusable .NET API. PowerShell Markdown describes command
behavior, while compiled metadata supplies parameter binding details. The generated [API catalog](API.md) leads to the reference. Build the
site under `build/docs/site`; no site is published by the build.

When changing commands, update the owning catalog, implement the adapter, run UpdateHelp, and review the resulting command pages. Keep
non-obvious defaults, exceptions, ownership, platform requirements, and cancellation behavior documented. Prefer representative examples and
focused functional tests to repetitive property documentation and implementation-mirroring tests.

## Packaging and releases

Import the staged module in a fresh process:

```powershell
Import-Module ./build/module/PSFoundation/PSFoundation.psd1
Get-Help Get-OfficeInventory -Full
```

A rebuild cannot unload assemblies already imported by an existing PowerShell process. Keep the loader, both binary directories, helper
executable, configuration, data, and help together when copying the package.

Semantic-release passes its selected version to `tools/release.ps1 -Prepare`, which verifies and packages that version. The v1 manifest
remains frozen. Versions below 2.0.0 are rejected: the first v2 release needs an intentional major-release decision. The release workflow
remains manual and requires `PSFOUNDATION_RELEASE_ENABLED=true` on a supported release branch.

```powershell
./tools/release.ps1 -Prepare -Version 2.0.0 -DryRun
./tools/release.ps1 -Publish -Version 2.0.0 -DryRun
```

Actual publication uses the already verified staged files and rejects changed contents. Set `NUGET_API_KEY` only in the publishing
environment. Never put a key in command-line arguments or an MSBuild property. Ordinary Verify does not publish. Scheduled upstream checks
are also separate from ordinary tests.

## Commit conventions

Use `type(scope): summary` with an imperative, lowercase summary and no trailing period. Types: feat, fix, docs, refactor, test, chore,
build, ci. Scopes: src, tools, tests, config, docs. Include a body of at least 20 characters, except for docs commits. Explain the problem,
resulting behavior, and relevant validation. Use a BREAKING CHANGE footer only when an intentional compatibility change requires a major
release.

## Live validation

Keep administrative mutation tests separate from ordinary development checks. Use disposable or recoverable Windows machines for Office
deployment, Windows Update servicing, AppX lifecycle operations, and remote directory or certificate workflows. Track remaining evidence in
[migration progress](migration/progress.md).
