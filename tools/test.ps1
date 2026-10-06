#Requires -Version 5.1

<#
.SYNOPSIS
  Runs Pester tests for the AdNoctem.Substrate.PowerShell module.

.DESCRIPTION
  Invokes Pester against the packaged-module tests in tests/PowerShell. By
  default Integration-tagged tests are excluded. Exits
  with 1 for failed tests, failed containers or empty discovery, otherwise 0.

.PARAMETER Path
  Path to test files or directory. Defaults to tests/PowerShell, the v2 host suite.
.PARAMETER Coverage
  Collect informational source coverage in JaCoCo format, with no percentage gate.
.PARAMETER OutputDirectory
  Write NUnit test results and optional coverage here. Coverage defaults this to
  build/test-results when no directory is supplied.
.PARAMETER IncludeIntegration
  Include tests tagged Integration. These may require privileges or change state.

.EXAMPLE
  PS> ./test.ps1
  Runs the packaged-module tests in tests/PowerShell.

.EXAMPLE
  PS> ./tools/test.ps1 -Path ./tests/PowerShell/Registry.Tests.ps1
  Runs only the current registry host tests.

.LINK
  https://github.com/adnoctem/substrate

.NOTES
  Author: MVProwess <info@mvprowess.com>
  License: MIT
#>

[CmdletBinding()]
param (
  [string[]]$Path = @(Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath 'tests/PowerShell'),
  [switch]$Coverage,
  [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'build/test-results/powershell'),
  [switch]$IncludeIntegration,
  [switch]$Managed
)

$ErrorActionPreference = 'Stop'

if ($Managed) {
  $root = Split-Path $PSScriptRoot -Parent
  $env:DOTNET_CLI_HOME = Join-Path $root 'build/dotnet'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
  & dotnet test (Join-Path $root 'Substrate.slnx') --no-build --no-restore --configuration Release --logger 'trx' --results-directory (Join-Path $root 'build/test-results/managed')

  exit $LASTEXITCODE
}

. (Join-Path $PSScriptRoot 'environment.ps1')
Import-DevelopmentModule Pester

$config = [PesterConfiguration]@{
  Run    = @{
    Path     = $Path
    PassThru = $true
  }
  Output = @{
    Verbosity = 'Detailed'
  }
}

if (-not $IncludeIntegration) { $config.Filter.ExcludeTag = @('Integration') }

if ($Coverage -and -not $OutputDirectory) {
  $OutputDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'build/test-results'
}

if ($OutputDirectory) {
  $OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
  $null = [IO.Directory]::CreateDirectory($OutputDirectory)
  $config.TestResult.Enabled = $true
  $config.TestResult.OutputFormat = 'NUnitXml'
  $config.TestResult.OutputPath = Join-Path $OutputDirectory 'tests.xml'
}

if ($Coverage) {
  # Detailed verbosity dumps every uncovered command; the XML retains that
  # information while Normal keeps the CI log useful.
  $config.Output.Verbosity = 'Normal'
  $config.CodeCoverage.Enabled = $true
  $config.CodeCoverage.Path = @(Join-Path (Split-Path $PSScriptRoot -Parent) 'src/PowerShell/compat.ps1')
  $config.CodeCoverage.OutputFormat = 'JaCoCo'
  $config.CodeCoverage.OutputPath = Join-Path $OutputDirectory 'coverage.xml'
  $config.CodeCoverage.CoveragePercentTarget = 0
}

$result = Invoke-Pester -Configuration $config

if ($result.FailedCount -gt 0 -or $result.FailedContainersCount -gt 0 -or $result.TotalCount -eq 0) { exit 1 }

exit 0
