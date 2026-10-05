# Troubleshooting

## Missing development tools

Run `dotnet msbuild tools/tasks.proj -t:Restore`. Use the SDK selected by `global.json`, PowerShell 7.4+ for developer tasks, and Bun for
the pinned Markdown formatter. Windows PowerShell 5.1 is also required for compatibility validation. Restored PowerShell modules live in
`build/tools/modules`, independent of globally installed module versions.

## Changes do not appear after rebuilding

Use a fresh PowerShell process when testing changed assemblies. Removing and reimporting a module does not reliably unload its .NET
assemblies. Import `build/module/PSFoundation/PSFoundation.psd1`, not the frozen manifest directly under `src`.

## Missing command help

Run the `HelpFiles` or `Docs` target and inspect `Get-Help <command> -Full` in a fresh session. Keep the staged `en-US` directories
alongside the loader and binaries when copying the module.

## Platform-specific failures

Record the Windows version, PowerShell edition, process architecture, module version, operation result, and native error code. Do not
include credentials, license keys, or private machine inventories in public reports. AppX inventory success does not establish Store API
access; Store operations depend on Windows capabilities and caller identity.

## Live validation

Automated tests do not exercise production Office migration, Windows Update installation, or remote directory changes. Follow
`docs/migration/progress.md` in the repository for the remaining validation work, using disposable or recoverable machines.
