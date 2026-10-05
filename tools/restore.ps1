#Requires -Version 7.4
<#
.SYNOPSIS
  Restores pinned development dependencies into repository-local directories.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:Restore
#>
[CmdletBinding()]
param ()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'environment.ps1')
$null = [IO.Directory]::CreateDirectory($toolModules)
foreach ($dependency in (Get-Content (Join-Path $PSScriptRoot 'dev-dependencies.json') -Raw | ConvertFrom-Json)) {
  $manifest = Join-Path $toolModules "$($dependency.Name)/$($dependency.RequiredVersion)/$($dependency.Name).psd1"
  if (-not (Test-Path -LiteralPath $manifest)) {
    Save-PSResource -Name $dependency.Name -Version $dependency.RequiredVersion -Repository PSGallery -Path $toolModules -TrustRepository -ErrorAction Stop
  }
}
Push-Location $repositoryRoot
try {
  & dotnet restore Substrate.slnx --locked-mode --nologo
  if ($LASTEXITCODE) { throw 'NuGet restore failed.' }
  & dotnet tool restore
  if ($LASTEXITCODE) { throw 'Documentation tool restore failed.' }
  & bun install --frozen-lockfile
  if ($LASTEXITCODE) { throw 'Formatting tool restore failed.' }
}
finally { Pop-Location }
