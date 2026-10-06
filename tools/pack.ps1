#Requires -Version 7.0
<#
.SYNOPSIS
  Packages the staged PowerShell module and reusable NuGet libraries with SHA256 checksums.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:Pack
#>
[CmdletBinding()]
param ()

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$configuration = if ($env:SUBSTRATE_CONFIGURATION) { $env:SUBSTRATE_CONFIGURATION } else { 'Release' }

$stage = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell'
$manifest = Import-PowerShellDataFile (Join-Path $stage 'AdNoctem.Substrate.PowerShell.psd1')

if (-not (Test-Path -LiteralPath (Join-Path $stage 'en-US/AdNoctem.Substrate.PowerShell-help.xml'))) { throw 'Generate help before packaging.' }

$version = [string]$manifest.ModuleVersion

if ($manifest.PrivateData.PSData.Prerelease) { $version += '-' + $manifest.PrivateData.PSData.Prerelease }

$dist = Join-Path $root 'dist'
$null = [IO.Directory]::CreateDirectory($dist)
$nuget = [IO.Path]::GetFullPath((Join-Path $dist 'nuget'))

if (-not $nuget.StartsWith([IO.Path]::GetFullPath($dist) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'NuGet output must remain inside dist.' }

if (Test-Path -LiteralPath $nuget) { Remove-Item -LiteralPath $nuget -Recurse -Force }

& dotnet pack (Join-Path $root 'Substrate.slnx') --no-build --no-restore --configuration $configuration "-p:Version=$version" --output $nuget --nologo

if ($LASTEXITCODE) { throw 'NuGet packaging failed.' }

$packages = @(Get-ChildItem -LiteralPath $nuget -Filter '*.nupkg' -File | Sort-Object Name)

if ($packages.Count -ne 11) { throw 'Expected exactly the eleven reusable domain packages.' }

# Release asset globs must never include archives left by an earlier package version.
Get-ChildItem -LiteralPath $dist -File | Where-Object { $_.Name -match '^AdNoctem.Substrate.PowerShell-[0-9].*\.(zip|tar\.gz)$' } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
$name = "AdNoctem.Substrate.PowerShell-$version"
$zip = Join-Path $dist "$name.zip"
$tar = Join-Path $dist "$name.tar.gz"
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force
& tar -czf $tar -C (Split-Path $stage -Parent) AdNoctem.Substrate.PowerShell

if ($LASTEXITCODE) { throw 'tar failed.' }

$checksums = @($zip, $tar) + @($packages.FullName) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetRelativePath($dist, $_).Replace('\', '/') }
[IO.File]::WriteAllLines((Join-Path $dist 'CHECKSUMS_SHA256.txt'), $checksums)
Write-Output "Packaged $name and $($packages.Count) NuGet libraries in $dist"
