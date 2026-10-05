#Requires -Version 7.0
<#
.SYNOPSIS
  Prepares or explicitly publishes a verified substrate package.
.PARAMETER Version
  Semantic-release version. Releases before 1.0.0 are rejected.
.PARAMETER Prepare
  Builds and verifies the selected version without publishing.
.PARAMETER Publish
  Publishes the already prepared module. Credentials are read from NUGET_API_KEY.
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
  [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Prepare -eq $Publish) { throw 'Select exactly one of Prepare or Publish.' }
if ([version]($Version -split '-', 2)[0] -lt [version]'1.0.0') { throw 'Substrate releases start at 1.0.0.' }
$action = if ($Prepare) { 'Prepare and verify' } else { 'Publish' }
if ($DryRun -or -not $PSCmdlet.ShouldProcess("AdNoctem.Substrate.PowerShell $Version", $action)) {
  Write-Output "DRY RUN: $action AdNoctem.Substrate.PowerShell $Version."
  return
}
Push-Location $root
try {
  if ($Prepare) {
    & bun run format
    if ($LASTEXITCODE) { throw 'Changelog formatting failed.' }
    & dotnet msbuild tools/tasks.proj -t:Verify "-p:Version=$Version" -nologo
    if ($LASTEXITCODE) { throw 'Release verification failed.' }
    $stage = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell'
    $files = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = [IO.Path]::GetRelativePath($stage, $_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
      })
    [ordered]@{ Version = $Version; Files = $files } | ConvertTo-Json -Depth 5 |
      Set-Content -LiteralPath (Join-Path $root 'build/release.json') -Encoding utf8
    return
  }
  $stage = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell'
  $evidence = Get-Content (Join-Path $root 'build/release.json') -Raw | ConvertFrom-Json
  if ($evidence.Version -cne $Version) { throw 'Prepared package version does not match the release.' }
  $actual = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName)
  if ($actual.Count -ne $evidence.Files.Count) { throw 'The prepared package file set changed.' }
  foreach ($file in $actual) {
    $relative = [IO.Path]::GetRelativePath($stage, $file.FullName)
    $expected = @($evidence.Files | Where-Object Path -CEQ $relative)
    if ($expected.Count -ne 1 -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne $expected[0].Sha256) {
      throw "Prepared package changed: $relative"
    }
  }
  if (-not $env:NUGET_API_KEY) { throw 'Set NUGET_API_KEY in the publishing environment.' }
  Publish-PSResource -Path $stage -Repository PSGallery -ApiKey $env:NUGET_API_KEY -ErrorAction Stop
}
finally { Pop-Location }
