# Using the .NET libraries

Substrate provides reusable C# libraries under the `AdNoctem.Substrate.*` package IDs. Install the domain package your application uses;
NuGet resolves its dependencies. All domain packages share the release version. The PowerShell adapter is distributed separately as
`AdNoctem.Substrate.PowerShell` on PSGallery.

For example, an application using the registry library can add a reference with:

```shell
dotnet add package AdNoctem.Substrate.Registry
```

The libraries expose typed operations without loading PowerShell. Applications supply credentials, cancellation, logging and user
confirmation policy. Windows-specific operations require Windows and suitable authority; .NET Standard compatibility does not imply
cross-platform support. Check each package's target frameworks and generated API contracts before deploying it.

`AdNoctem.Substrate.Packages` includes an isolated x64 .NET Framework 4.8 Windows Runtime helper for AppX and Store operations. Its
transitive MSBuild targets copy the helper executable and configuration beside the application's output and published files. Keep both
files beside the package assembly when deploying manually. Store capability access is a separate requirement. Single-file deployment
and trimming are not established deployment modes for these Windows integrations.

Read the [repository documentation](https://github.com/adnoctem/substrate/tree/main/docs/www) and
[build instructions](https://github.com/adnoctem/substrate/blob/main/docs/CONTRIBUTING.md). The `Docs` target builds searchable API reference
from the shipped XML comments; the repository's [validation backlog](https://github.com/adnoctem/substrate/blob/main/docs/TODO.md) records
remaining native deployment scenarios.
