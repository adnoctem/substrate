# Architecture

The domain libraries own Windows administration behavior. Applications can use them directly without loading PowerShell. The PowerShell
adapter provides command binding, confirmation, presentation, and compatibility conventions.

Managers implement functionality owned by PSFoundation. Tools represent external executables such as LGPO and the Office Deployment Tool.
Discovery, preparation, validation, and execution are distinct operations; discovering a tool does not authorize downloading or running it.

Applications choose their own prompts, logging destinations, credentials, and cancellation policy. Consult each operation's reference for
ownership and cancellation limits: a cancellation request does not imply rollback of changes already applied by Windows or an external tool.

The packaged module supports Windows PowerShell 5.1 and PowerShell 7 on x64 Windows. Its Desktop binaries target .NET Framework 4.8; Core
binaries use .NET Standard 2.0. .NET Standard is a library compatibility contract, not a guarantee that Windows-specific operations run on
other operating systems. The AppX/Store bridge additionally requires the shipped .NET Framework helper and its configuration file.

Build output lives under `build/`; distributable archives live under `dist/`. Frozen v1 scripts remain comparison material for compatibility
tests. Production module staging uses the C# binaries, `compat.ps1`, command catalogs, and the v2 manifest template.
