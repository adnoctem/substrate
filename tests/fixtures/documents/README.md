# Word-authored press release documents

These `.docx` files were saved by desktop Word 2019 (16.0.10417) from the synthetic authoring template that
`New-WordFixtures.ps1` builds. They exercise how Word writes content controls, rich text, lists, tables and optional
section deletion, so the reader tests run without Word or the private reference files. The documents contain only
synthetic text, a `mailto:press@example.com` link and an `https://example.com` hyperlink.

Word stamps the author name, a local attached-template path and a thumbnail into saved packages. The generator removes
the creator and last-modified-by properties, the `attachedTemplate` element and external relationship, and the
thumbnail part. Regenerating the fixtures can change internal package details such as revision identifiers even when
the visible content is unchanged.

| Fixture                 | Authoring state                                                                        |
| ----------------------- | -------------------------------------------------------------------------------------- |
| `untouched.docx`        | Template opened and saved without filling any field.                                   |
| `complete.docx`         | Every field filled, including formatting, both list kinds, nesting, link and fact box. |
| `deleted-optional.docx` | Required fields filled; every optional block deleted as a whole.                       |
| `wrapper-removed.docx`  | Required fields filled; an optional wrapper removed but its content left behind.       |
| `heading-only.docx`     | Required fields filled; an optional section has a heading but no body.                 |

To regenerate the files, build the module binaries and run the script with PowerShell 7 on a machine with Word
installed:

```powershell
dotnet msbuild tools/tasks.proj -t:Build
pwsh -File tests/fixtures/documents/New-WordFixtures.ps1
```

The fixtures record this Word version's output; they do not establish behavior in every supported Word release.
Checks that need the Word user interface, such as demoting a list item with the Tab key, remain manual.
