#Requires -Version 5.1
<#
.SYNOPSIS
  Captures API contracts from the pinned, immutable v1 source in a fresh host.
.PARAMETER WriteFixtures
  Writes reviewed-source captures to tests/Fixtures/Api rather than build/baseline.
.EXAMPLE
  .\PSFoundation.ps1 baseline -WriteFixtures
#>
[CmdletBinding()]
param ([switch]$WriteFixtures)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$commit = 'd2d1498275806684b44169504146302d54b7a084'
$destination = Join-Path $root "build/baseline/$commit"
$archive = Join-Path $root 'build/baseline/v1.zip'
$null = [IO.Directory]::CreateDirectory((Split-Path $archive -Parent))
& git -C $root archive --format=zip "--output=$archive" $commit src
if ($LASTEXITCODE -ne 0) { throw 'Cannot materialize the pinned v1 source.' }
if (-not (Test-Path -LiteralPath $destination)) {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [IO.Compression.ZipFile]::ExtractToDirectory($archive, $destination)
}
# Verify Git content (including its declared line-ending normalization) before reuse.
$baselineFiles = @(& git -C $root ls-tree -r --name-only $commit -- src)
foreach ($file in $baselineFiles) {
  $expected = & git -C $root rev-parse "${commit}:$file"
  $actual = & git -C $root hash-object "--path=$file" (Join-Path $destination $file)
  if ($LASTEXITCODE -ne 0 -or $actual -ne $expected) { throw "Pinned baseline differs at '$file'." }
}
. (Join-Path $PSScriptRoot 'compatibility.ps1')
$contract = Get-PSFApiContract -ModulePath (Join-Path $destination 'src/PSFoundation.psd1')
if ($contract.Commands.Count -ne 192) { throw 'The v1 baseline must contain 188 functions and four aliases.' }
$outputRoot = Join-Path $root 'build/baseline'
if ($WriteFixtures) { $outputRoot = Join-Path $root 'tests/Fixtures/Api' }
$null = [IO.Directory]::CreateDirectory($outputRoot)
$capture = [ordered]@{
  SourceCommit = $commit; SourceVersion = '1.8.7'; Edition = $PSVersionTable.PSEdition
  PowerShellVersion = [string]$PSVersionTable.PSVersion; Contract = $contract
}
$path = Join-Path $outputRoot ($PSVersionTable.PSEdition.ToLowerInvariant() + '.json')
[IO.File]::WriteAllText($path, (($capture | ConvertTo-Json -Depth 40) + "`r`n"), (New-Object Text.UTF8Encoding($false)))
Write-Output "Captured $($contract.Commands.Count) exports from v1.8.7 to $path"
