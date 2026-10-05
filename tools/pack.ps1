#Requires -Version 7.0
<#
.SYNOPSIS
  Archives the staged module and writes SHA256 checksums.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:Pack
#>
[CmdletBinding()]
param ()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $root 'build/module/PSFoundation'
$manifest = Import-PowerShellDataFile (Join-Path $stage 'PSFoundation.psd1')
if (-not (Test-Path -LiteralPath (Join-Path $stage 'en-US/PSFoundation-help.xml'))) { throw 'Generate help before packaging.' }
$version = [string]$manifest.ModuleVersion
if ($manifest.PrivateData.PSData.Prerelease) { $version += '-' + $manifest.PrivateData.PSData.Prerelease }
$dist = Join-Path $root 'dist'
$null = [IO.Directory]::CreateDirectory($dist)
# Release asset globs must never include archives left by an earlier package version.
Get-ChildItem -LiteralPath $dist -File | Where-Object { $_.Name -match '^PSFoundation-[0-9].*\.(zip|tar\.gz)$' } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
$name = "PSFoundation-$version"
$zip = Join-Path $dist "$name.zip"
$tar = Join-Path $dist "$name.tar.gz"
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force
& tar -czf $tar -C (Split-Path $stage -Parent) PSFoundation
if ($LASTEXITCODE) { throw 'tar failed.' }
$checksums = @($zip, $tar) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines((Join-Path $dist 'CHECKSUMS_SHA256.txt'), $checksums)
Write-Output "Packaged $name in $dist"
