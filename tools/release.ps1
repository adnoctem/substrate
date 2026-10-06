#Requires -Version 7.0
<#
.SYNOPSIS
  Prepares or explicitly publishes a verified substrate package.
.PARAMETER Version
  Semantic-release version. Releases before 1.0.0 are rejected.
.PARAMETER Prepare
  Builds and verifies the selected version without publishing.
.PARAMETER Publish
  Publishes prepared packages. Credentials come from PSGALLERY_API_KEY and NUGET_API_KEY.
.PARAMETER ValidateOnly
  Checks prepared file hashes without publishing or loading credentials.
.PARAMETER Destination
  Selects All, PSGallery or NuGet. The default publishes both distributions.
.PARAMETER PackageId
  Optionally selects specific prepared NuGet packages when retrying a partial publication.
.PARAMETER DryRun
  Reports the intended operation without building or publishing.
.EXAMPLE
  ./tools/release.ps1 -Prepare -Version 1.0.0
.EXAMPLE
  ./tools/release.ps1 -Publish -Version 1.0.0 -DryRun
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param (
  [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$')][string]$Version,
  [switch]$Prepare,
  [switch]$Publish,
  [switch]$ValidateOnly,
  [ValidateSet('All', 'PSGallery', 'NuGet')][string]$Destination = 'All',
  [string[]]$PackageId,
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if (([int]$Prepare.IsPresent + [int]$Publish.IsPresent + [int]$ValidateOnly.IsPresent) -ne 1) { throw 'Select exactly one of Prepare, Publish or ValidateOnly.' }

if ($PackageId -and (-not $Publish -or $Destination -ne 'NuGet')) { throw 'PackageId requires Publish with Destination NuGet.' }

if ([version]($Version -split '-', 2)[0] -lt [version]'1.0.0') { throw 'Substrate releases start at 1.0.0.' }

$action = if ($Prepare) { 'Prepare and verify' } elseif ($ValidateOnly) { 'Validate prepared files' } else { "Publish to $Destination" }

if ($DryRun -or (-not $ValidateOnly -and -not $PSCmdlet.ShouldProcess("substrate $Version", $action))) {
  Write-Output "DRY RUN: $action substrate $Version."

  return
}

Push-Location $root

try {
  $stage = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell'
  $dist = Join-Path $root 'dist'
  $record = Join-Path $root 'build/release.json'

  function Get-ReleaseFile {
    @(Get-ChildItem -LiteralPath $stage -File -Recurse) + @(Get-ChildItem -LiteralPath $dist -File -Recurse) | Sort-Object FullName
  }

  if ($Prepare) {
    # Failed preparation must not leave an earlier release record usable.
    if (Test-Path -LiteralPath $record) { Remove-Item -LiteralPath $record -Force }

    if (Test-Path -LiteralPath 'CHANGELOG.md') {
      & bun x prettier --write --end-of-line auto CHANGELOG.md

      if ($LASTEXITCODE) { throw 'Changelog formatting failed.' }
    }

    & dotnet msbuild tools/tasks.proj -t:Verify "-p:Version=$Version" -nologo

    if ($LASTEXITCODE) { throw 'Release verification failed.' }

    $files = @(Get-ReleaseFile | ForEach-Object {
        [ordered]@{ Path = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/'); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
      })
    [ordered]@{ Schema = 2; Version = $Version; Files = $files } | ConvertTo-Json -Depth 5 |
      Set-Content -LiteralPath $record -Encoding utf8

    return
  }

  $evidence = Get-Content -LiteralPath $record -Raw | ConvertFrom-Json

  if ($evidence.Schema -ne 2 -or $evidence.Version -cne $Version) { throw 'Prepared package schema or version does not match the release.' }

  $actual = @(Get-ReleaseFile)

  if (-not $actual.Count) { throw 'No prepared release files.' }

  if ($actual.Count -ne $evidence.Files.Count) { throw 'The prepared package file set changed.' }

  foreach ($file in $actual) {
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
    $expected = @($evidence.Files | Where-Object Path -CEQ $relative)

    if ($expected.Count -ne 1 -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne $expected[0].Sha256) {
      throw "Prepared package changed: $relative"
    }
  }

  if ($ValidateOnly) { Write-Output "Validated prepared substrate $Version files."; return }

  $publishGallery = $Destination -in @('All', 'PSGallery')
  $publishNuGet = $Destination -in @('All', 'NuGet')

  if ($publishGallery -and -not $env:PSGALLERY_API_KEY) { throw 'Set PSGALLERY_API_KEY in the publishing environment.' }

  if ($publishNuGet -and -not $env:NUGET_API_KEY) { throw 'Set NUGET_API_KEY in the publishing environment.' }

  $packages = @(Get-ChildItem -LiteralPath (Join-Path $dist 'nuget') -Filter '*.nupkg' -File | Sort-Object Name)

  if ($packages.Count -ne 11) { throw 'Expected eleven prepared NuGet libraries.' }

  if ($PackageId) {
    $selected = foreach ($id in $PackageId | Sort-Object -Unique) {
      $match = @($packages | Where-Object Name -CEQ "$id.$Version.nupkg")

      if ($match.Count -ne 1) { throw "PackageId is not a prepared library: $id" }

      $match[0]
    }

    $packages = @($selected)
  }

  . (Join-Path $PSScriptRoot 'environment.ps1')
  Import-DevelopmentModule Microsoft.PowerShell.PSResourceGet
  # Verify repository identities before giving either feed its credential.
  $repositories = @{}

  if ($publishGallery) { $repositories['PSGallery'] = 'https://www.powershellgallery.com/api/v2' }

  if ($publishNuGet) { $repositories['NuGetGallery'] = 'https://api.nuget.org/v3/index.json' }

  foreach ($name in $repositories.Keys) {
    $repository = Get-PSResourceRepository -Name $name -ErrorAction SilentlyContinue

    if (-not $repository) { Register-PSResourceRepository -Name $name -Uri $repositories[$name] }
    elseif ($repository.Uri.AbsoluteUri.TrimEnd('/') -cne $repositories[$name]) { throw "Unexpected repository URI for $name." }
  }

  if ($publishNuGet) {
    foreach ($package in $packages) {
      Publish-PSResource -NupkgPath $package.FullName -Repository NuGetGallery -ApiKey $env:NUGET_API_KEY -SkipDependenciesCheck -ErrorAction Stop
    }
  }

  if ($publishGallery) {
    Publish-PSResource -Path $stage -Repository PSGallery -ApiKey $env:PSGALLERY_API_KEY -ErrorAction Stop
  }
}
finally { Pop-Location }
