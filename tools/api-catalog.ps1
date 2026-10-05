#Requires -Version 7.0
<#
.SYNOPSIS
  Generates or verifies the tracked reference index from DocFX metadata and command catalogs.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:UpdateHelp
#>
[CmdletBinding()]
param ([switch]$Update)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# API reference')
$lines.Add('')
$lines.Add('Generated catalog. Refresh with `dotnet msbuild tools/tasks.proj -t:UpdateHelp`.')
$lines.Add('')
$lines.Add('Build the complete searchable .NET reference with `dotnet msbuild tools/tasks.proj -t:Docs`; open `build/docs/site/index.html`.')
$lines.Add('The .NET catalog links below use DocFX cross-references, resolved in that generated site. PowerShell pages are also readable here on GitHub.')
$lines.Add('')
$lines.Add('## .NET libraries')
$lines.Add('')
foreach ($file in Get-ChildItem (Join-Path $root 'build/docs/api') -Filter 'AdNoctem.Substrate.*.yml' | Sort-Object Name) {
  if ($file.BaseName -match '^AdNoctem\.Substrate\.[^.]+$') { continue }
  $text = [IO.File]::ReadAllText($file.FullName)
  if ($text -notmatch '(?m)^  type: (Class|Interface|Enum|Struct|Delegate)\s*$') { continue }
  $uid = $file.BaseName
  $lines.Add("- [$uid](xref:$uid)")
}
$lines.Add('')
$lines.Add('## PowerShell commands')
$lines.Add('')
foreach ($file in Get-ChildItem (Join-Path $root 'docs/commands/AdNoctem.Substrate.PowerShell') -Filter '*.md' | Sort-Object Name) {
  $lines.Add("- [$($file.BaseName)](commands/AdNoctem.Substrate.PowerShell/$($file.Name))")
}
$lines.Add('')
$lines.Add('Aliases: `Get-Network`, `Get-Prefix`, `Get-NetworkCIDR`, `Get-PrefixCIDR`. Their help resolves to the corresponding command.')
$lines.Add('')
$content = $lines -join "`n"
$path = Join-Path $root 'docs/API.md'
if ($Update) { [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false)) }
elseif (-not (Test-Path -LiteralPath $path) -or [IO.File]::ReadAllText($path).Replace("`r`n", "`n") -cne $content) { throw 'docs/API.md is stale. Run the UpdateHelp target and review the changes.' }
