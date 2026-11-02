# substrate

Substrate provides reusable C# libraries and PowerShell commands for Windows administration. Applications can use the domain libraries
without loading PowerShell; the module supports Windows PowerShell 5.1 and PowerShell 7 on x64 Windows.

The [PowerShell module](https://www.powershellgallery.com/packages/AdNoctem.Substrate.PowerShell) and all eleven
[NuGet domain libraries](guides/libraries.md) are published as 0.x releases and available to use. Their APIs are still evolving: pin a
reviewed release and read its release notes before upgrading. Stable compatibility guarantees begin at 1.0.0.

## Reference

The generated [.NET reference](../../build/docs/api/toc.yml) describes the public library types and members, including examples and
contracts from C# XML comments. Start with <xref:AdNoctem.Substrate.Registry.RegistryManager> or
<xref:AdNoctem.Substrate.Networking.NetworkManager> for registry and networking workflows.

The [PowerShell reference](../../build/docs/commands/toc.yml) covers the exported commands. The same reviewed content is packaged for
`Get-Help <command> -Full`. These navigation links resolve in the built site; generate it with `dotnet msbuild tools/tasks.proj -t:Docs` and
open `build/docs/site/index.html`.

## Guides

- [.NET libraries](guides/libraries.md): published NuGet packages, version pinning and runtime requirements.
- [PowerShell workflows](guides/powershell.md): policy files, process results, registry snapshots, prerequisites and elevation.
- [Office deployment](guides/office.md): prepare media, review plans, execute and assess recovery.
- [Outlook PST sources](guides/outlook.md): attach, reuse and release existing stores.
- [Architecture](architecture.md) and [troubleshooting](troubleshooting.md).

For repository development, see [Contributing](https://github.com/adnoctem/substrate/blob/main/docs/CONTRIBUTING.md), [migration
history](https://github.com/adnoctem/substrate/blob/main/docs/MIGRATION.md), and [outstanding
work](https://github.com/adnoctem/substrate/blob/main/docs/TODO.md). The [documentation maintenance guide](documentation.md) explains how
sources become reference pages and packaged help.
