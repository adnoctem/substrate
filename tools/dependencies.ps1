#Requires -Version 7.0
<#
.SYNOPSIS
  Proposes updates to pinned PowerShell development modules.
.DESCRIPTION
  Writes dependency candidates only. The workflow restores, verifies, and opens a reviewable pull request.
.EXAMPLE
  ./tools/dependencies.ps1
#>
[CmdletBinding()]
param ()
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'dev-dependencies.json'
$dependencies = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
$changes = [Collections.Generic.List[string]]::new()
foreach ($dependency in $dependencies) {
  $latest = Find-PSResource -Name $dependency.Name -Repository PSGallery -ErrorAction Stop
  if ([version]$latest.Version -gt [version]$dependency.RequiredVersion) {
    $changes.Add("$($dependency.Name): $($dependency.RequiredVersion) -> $($latest.Version)")
    $dependency.RequiredVersion = $latest.Version.ToString()
  }
}
if ($changes.Count) {
  [IO.File]::WriteAllText($path, (($dependencies | ConvertTo-Json -Depth 4) -replace '\r?\n', "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
}
$changes
