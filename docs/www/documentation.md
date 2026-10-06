# Maintaining documentation

Restore tools once with `dotnet msbuild tools/tasks.proj -t:Restore`.

| Source                                                                      | Generated output                                       |
| --------------------------------------------------------------------------- | ------------------------------------------------------ |
| C# XML comments                                                             | Assembly XML documentation and DocFX .NET reference    |
| Command metadata and `docs/www/commands/AdNoctem.Substrate.PowerShell/*.md` | PowerShell reference pages and packaged `Get-Help` XML |
| Handwritten Markdown                                                        | Tutorials, architecture, and troubleshooting pages     |

Use `dotnet msbuild tools/tasks.proj -t:UpdateHelp` after changing public commands. Review the resulting Markdown changes, fill in
new descriptions and examples, and commit those sources. This is the explicit source-writing operation. Use
`dotnet msbuild tools/tasks.proj -t:Docs` to verify sources and build the site under `build/docs/site`.

Document behavior that a caller needs: missing-value semantics, ownership, exceptions, privileges, cancellation, and partial completion.
Avoid summaries that merely repeat an obvious property name. Undocumented public members remain visible in the reference. Malformed XML and
invalid parameter references fail compilation; missing comments alone do not require boilerplate.

PlatyPS owns the command Markdown structure; Prettier formats these pages alongside the rest of the documentation.
UpdateHelp formats its generated Markdown before handing it back for review.
Docs validates the formatted command metadata and generates PowerShell help for both module editions.
Compatibility functions use external help declarations, so their prose is maintained
in one place.

Website sources live under `docs/www`, including reviewed command help. The root of `docs` contains repository documents only.
There is no separately maintained API catalog or handwritten member reference. Update C# XML comments for library contracts and examples;
Docs rebuilds the .NET reference directly from the staged assemblies. PowerShell navigation is generated from the command pages under
`build/docs/commands`; .NET navigation is generated under `build/docs/api`.

The normal pipeline generates files only under `build/` and `dist/`. Docs does not rewrite tracked sources. It does not publish a website or a PowerShell Gallery release.
