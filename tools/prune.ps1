#Requires -Version 5.0

<#
.SYNOPSIS
  Prunes (removes) installed PowerShell module versions.

.DESCRIPTION
  Thin wrapper around Remove-PSModule from the substrate PowerShell module.
  Imports the staged v2 module; build it before invoking this optional helper.

  By default, keeps only the newest version of each module and removes older
  ones. Use -All to remove every version (prompts for confirmation unless
  -Force), or -LatestToKeep to control how many versions are retained.

  Usage:
    .\tools\prune.ps1 [-Name <regex>] [-All] [-LatestToKeep <int>] [-Scope <scope>] [-Path <dir>] [-Force] [-WhatIf]

.EXAMPLE
  .\tools\prune.ps1 -WhatIf
  Previews which old module versions would be removed.

.EXAMPLE
  .\tools\prune.ps1 -All -Force
  Removes all CurrentUser module versions without prompting.

.EXAMPLE
  .\tools\prune.ps1 -Name 'Pester' -LatestToKeep 2
  Keeps the two newest Pester versions, removes older ones.

.LINK
  https://github.com/adnoctem/substrate

.NOTES
  Author: MVProwess <info@mvprowess.com>
  License: MIT
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [string]$Name,

  [ValidateRange(1, [int]::MaxValue)]
  [int]$LatestToKeep = 1,

  [switch]$All,

  [ValidateSet('CurrentUser', 'AllUsers')]
  [string]$Scope = 'CurrentUser',

  [string]$Path,

  [switch]$Force
)

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$maintenancePath = Join-Path -Path $repoRoot -ChildPath 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'

if (-not (Test-Path -LiteralPath $maintenancePath -PathType Leaf)) {
  Write-Error "Module source not found: $maintenancePath"

  exit 1
}

Import-Module $maintenancePath -Force -ErrorAction Stop
Remove-PSModule @PSBoundParameters

exit $LASTEXITCODE
