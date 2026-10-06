#Requires -Version 5.1
# Shared repository-local tool resolution. Dot-source from development scripts only.
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$toolModules = Join-Path $repositoryRoot 'build/tools/modules'
$env:PSModulePath = $toolModules + [IO.Path]::PathSeparator + $env:PSModulePath
$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot 'build/dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

function Import-DevelopmentModule {
  param ([Parameter(Mandatory = $true)][string]$Name)

  $dependency = @(Get-Content (Join-Path $PSScriptRoot 'dev-dependencies.json') -Raw | ConvertFrom-Json) | Where-Object Name -EQ $Name

  if (-not $dependency) { throw "No pinned development dependency: $Name" }

  $manifest = Join-Path $toolModules "$Name/$($dependency.RequiredVersion)/$Name.psd1"

  if (-not (Test-Path -LiteralPath $manifest)) { throw "Run 'dotnet msbuild tools/tasks.proj -t:Restore' to restore $Name." }

  Import-Module $manifest -Force -Global -ErrorAction Stop
}
