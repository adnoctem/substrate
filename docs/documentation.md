# Maintaining documentation

Restore tools once with `dotnet msbuild tools/tasks.proj -t:Restore`.

| Source                                                 | Generated output                                       |
| ------------------------------------------------------ | ------------------------------------------------------ |
| C# XML comments                                        | Assembly XML documentation and DocFX .NET reference    |
| Command metadata and `docs/commands/PSFoundation/*.md` | PowerShell reference pages and packaged `Get-Help` XML |
| Handwritten Markdown                                   | Tutorials, architecture, and troubleshooting pages     |

Use `dotnet msbuild tools/tasks.proj -t:UpdateHelp` after changing public commands or types. Review the resulting Markdown changes, fill in
new descriptions and examples, and commit those sources. This is the explicit source-writing operation. Use
`dotnet msbuild tools/tasks.proj -t:Docs` to verify sources and build the site under `build/docs/site`.

Document behavior that a caller needs: missing-value semantics, ownership, exceptions, privileges, cancellation, and partial completion.
Avoid summaries that merely repeat an obvious property name. Undocumented public members remain visible in the reference. Malformed XML and
invalid parameter references fail compilation; missing comments alone do not require boilerplate.

PlatyPS owns the command Markdown structure. The general Markdown formatter excludes these files to preserve that structure. PowerShell help
is generated from these pages for both module editions. Compatibility functions use external help declarations, so their prose is maintained
in one place.

The normal pipeline generates files only under `build/` and `dist/`. It does not publish a website or a PowerShell Gallery release.
