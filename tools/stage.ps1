#Requires -Version 7.0
<#
.SYNOPSIS
  Stages built assemblies, compatibility functions and the v2 module manifest.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:Stage
#>
[CmdletBinding()]
param ()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'environment.ps1')
$configuration = if ($env:SUBSTRATE_CONFIGURATION) { $env:SUBSTRATE_CONFIGURATION } else { 'Release' }
$version = if ($env:SUBSTRATE_VERSION) { $env:SUBSTRATE_VERSION } else { '1.0.0' }
if ($configuration -notin @('Debug', 'Release') -or $version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') { throw 'Invalid build configuration or version.' }
$stage = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'build/module/AdNoctem.Substrate.PowerShell'))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'build')) + [IO.Path]::DirectorySeparatorChar
if (-not $stage.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Module staging must remain under build.' }
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
$null = [IO.Directory]::CreateDirectory($stage)
$compiled = @((Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compiled-commands.psd1')).Values | ForEach-Object { $_ } | Sort-Object)
$compatibility = @((Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compatibility-commands.psd1')).Values | ForEach-Object { $_ } | Sort-Object)
$all = @($compiled + $compatibility)
if ($all.Count -ne @($all | Sort-Object -Unique).Count) { throw 'Each command must have exactly one implementation owner.' }
foreach ($target in @(@{ Edition = 'Desktop'; Framework = 'net48' }, @{ Edition = 'Core'; Framework = 'netstandard2.0' })) {
  $binaryRoot = Join-Path $stage ('lib/' + $target.Edition)
  & dotnet publish (Join-Path $repositoryRoot 'src/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.csproj') --no-build --no-restore --configuration $configuration --framework $target.Framework --self-contained false --output $binaryRoot --nologo
  if ($LASTEXITCODE) { throw 'Assembly staging failed.' }
  if (Test-Path -LiteralPath (Join-Path $binaryRoot 'System.Management.Automation.dll')) { throw 'PowerShell reference assemblies must never be packaged.' }
  foreach ($runtimeFile in @('AdNoctem.Substrate.WinRtHost.exe', 'AdNoctem.Substrate.WinRtHost.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "build/bin/AdNoctem.Substrate.WinRtHost/$configuration/net48/$runtimeFile") -Destination $binaryRoot
  }
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src/AdNoctem.Substrate.PowerShell/compat.ps1') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src/security.psd1') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'module.psd1') -Destination (Join-Path $stage 'AdNoctem.Substrate.PowerShell.psd1')
$manifestPath = Join-Path $stage 'AdNoctem.Substrate.PowerShell.psd1'
$parts = $version -split '-', 2
$prerelease = if ($parts.Count -eq 2) { $parts[1] } else { '' }
$aliases = (Import-PowerShellDataFile $manifestPath).AliasesToExport
$quotedNames = ($compiled | ForEach-Object { "'$_'" }) -join ', '
$quotedFunctions = ($compatibility | ForEach-Object { "'$_'" }) -join ', '
$quotedAliases = ($aliases | ForEach-Object { "'$_'" }) -join ', '
$loader = 'Import-Module (Join-Path $PSScriptRoot (''lib/'' + $PSEdition + ''/AdNoctem.Substrate.PowerShell.dll'')) -Scope Local -ErrorAction Stop' + "`r`n"
$loader += '. (Join-Path $PSScriptRoot ''compat.ps1'')' + "`r`n"
$loader += "Export-ModuleMember -Cmdlet @($quotedNames) -Function @($quotedFunctions) -Alias @($quotedAliases)`r`n"
[IO.File]::WriteAllText((Join-Path $stage 'AdNoctem.Substrate.PowerShell.psm1'), $loader, [Text.UTF8Encoding]::new($true))
Update-ModuleManifest -Path $manifestPath -ModuleVersion $parts[0] -Prerelease $prerelease -FunctionsToExport $compatibility -CmdletsToExport $compiled
Write-Output "Staged $version with $($compiled.Count) compiled commands and $($compatibility.Count) compatibility functions."
