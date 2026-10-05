# AGENTS.md - substrate

Substrate provides reusable C# libraries and a PowerShell module for Windows PowerShell 5.1 and PowerShell 7 on x64 Windows. Read
[CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow.

## Repository tasks

Use `dotnet msbuild tools/tasks.proj -t:<Target>`. Restore first on a fresh checkout.

| Target         | Purpose                                                               |
| -------------- | --------------------------------------------------------------------- |
| Restore        | Restore locked NuGet packages, DocFX, PowerShell tools, and formatter |
| Build / Stage  | Compile / assemble the importable module                              |
| Test           | Managed tests plus packaged-module checks in both PowerShell hosts    |
| UpdateHelp     | Explicitly refresh tracked PowerShell metadata and API catalog        |
| Docs           | Validate help and build the reference site                            |
| Format / Check | Apply formatting / check formatting and analysis                      |
| Pack           | Create archives and checksums under dist                              |
| Verify         | Check, test, document, and package                                    |
| Clean          | Remove generated output while retaining dependency caches             |

Before committing or handing changed files back, run `pre-commit run --all-files`. Resolve findings and rerun after hooks modify files. For
implementation changes, run the relevant MSBuild targets and finish with `Verify` when changing the build or packaging pipeline.

## Architecture and public APIs

- Reusable libraries have no PowerShell dependency, prompts, host UI assumptions, or implicit elevation. Applications own policy.
- Use Manager for owned domain behavior and Tool for external executables. Keep preparation, validation, and execution explicit.
- Define cancellation and partial-completion behavior for long operations. Release native resources deterministically.
- Public PowerShell ownership is declared in `tools/compiled-commands.psd1` and `tools/compatibility-commands.psd1`.
- Implement compiled commands in the PowerShell adapter; keep host compatibility functions in its `compat.ps1`.
- C# comments feed DocFX. Reviewed `docs/commands/AdNoctem.Substrate.PowerShell` Markdown feeds PlatyPS and packaged help.
- Document meaningful contracts without adding filler comments to obvious members. UpdateHelp refreshes metadata; review its output.
- Frozen `src/*.ps1` files are v1 comparison material. Do not add new production behavior there.

## Conventions

- C#: four-space indentation, nullable references, deterministic builds, warnings as errors. Follow the existing domain layout.
- PowerShell: two-space indentation, UTF-8 with BOM, CRLF; the formatter normalizes encoding. Shipped scripts remain compatible with 5.1.
- Development scripts may use PowerShell 7 when declared with Requires. Keep runtime and development prerequisites distinct.
- Keep repository filenames free of spaces; test whitespace paths using temporary fixtures.
- Prefer focused functional tests for substantial behavior. Live administrative mutations belong on disposable or recoverable machines.
- Conventional commits: `type(scope): summary`, with scopes `src|tools|tests|config|docs`. Types:
  `feat|fix|docs|refactor|test|chore|build|ci`. A body of at least 20 characters is required except for docs commits.
- Semantic-release supplies the release version to verification and staging. Do not manually bump the frozen v1 manifest.
- Publishing is explicitly gated. No pushing or publishing without user authorization.

## Security review rules

- Never commit secrets, credentials, tokens, or private machine configuration. Fixtures must be synthetic or sanitized.
- Do not log secrets or put them in process arguments. Encoding is not encryption.
- Verify downloaded executable content against independently trusted hashes or signatures before execution.
- Never disable certificate validation or bypass integrity failures.
- Do not use Invoke-Expression with constructed commands. Keep code separate from data and constrain values crossing process boundaries.

## Layout

- `src/AdNoctem.Substrate.*`: C# domain libraries, PowerShell adapter, and isolated Windows Runtime helper.
- `tests/AdNoctem.Substrate.*.Tests`: managed tests; `tests/PowerShell`: current packaged-module tests.
- `tests/*.Tests.ps1`: retained v1 reference tests, excluded from the ordinary v2 run.
- `tools/`: MSBuild orchestration and supporting scripts; `tools/module.psd1`: v2 manifest template.
- `docs/`: documentation sources; `build/`, `dist/`: ignored output.
- This file lives at `docs/AGENTS.md`; root `AGENTS.md` is a symlink. Edit this file.
