#Requires -Version 5.1
<#
.SYNOPSIS
  Restores, compiles and stages the incremental C# module through the launcher.
.EXAMPLE
  .\PSFoundation.ps1 build
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param ([string]$OutputDirectory, [string]$Name = 'PSFoundation', [ValidateSet('Zip', 'TarGz', 'Both')][string]$Format = 'Both')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $PSCmdlet.ShouldProcess($root, 'Build and package the C# module')) { exit 0 }
$env:DOTNET_CLI_HOME = Join-Path $root 'build/dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$solution = Join-Path $root 'PSFoundation.slnx'
& dotnet restore $solution --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet build $solution --no-restore --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$stage = [IO.Path]::GetFullPath((Join-Path $root 'build/module/PSFoundation'))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $root 'build')) + [IO.Path]::DirectorySeparatorChar
if (-not $stage.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Module staging must remain under build.' }
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
$null = [IO.Directory]::CreateDirectory($stage)
$catalog = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compiled-commands.psd1')
$compiled = @($catalog.Values | ForEach-Object { $_ } | Sort-Object)
$compatibilityCatalog = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compatibility-commands.psd1')
$compatibility = @($compatibilityCatalog.Values | ForEach-Object { $_ } | Sort-Object)
if ($compiled.Count -ne @($compiled | Sort-Object -Unique).Count) { throw 'Compiled command catalog contains duplicate entries.' }
if ($compatibility.Count -ne @($compatibility | Sort-Object -Unique).Count -or @($compatibility | Where-Object { $_ -in $compiled }).Count) { throw 'Compatibility commands must have unique owners.' }
$encoding = New-Object Text.UTF8Encoding($true)
$sourceManifest = Join-Path $root 'src/PSFoundation.psd1'
$exports = Import-PowerShellDataFile $sourceManifest
if (@(Compare-Object @($exports.FunctionsToExport | Sort-Object) @(@($compiled + $compatibility) | Sort-Object)).Count) { throw 'Every public function must have exactly one C# or compatibility owner.' }
Copy-Item -LiteralPath $sourceManifest -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'src/security.psd1') -Destination $stage
foreach ($target in @(@{ Edition = 'Desktop'; Framework = 'net48' }, @{ Edition = 'Core'; Framework = 'netstandard2.0' })) {
  $binaryRoot = Join-Path $stage ('lib/' + $target.Edition)
  & dotnet publish (Join-Path $root 'src/PSFoundation.PowerShell/PSFoundation.PowerShell.csproj') --no-restore --configuration Release --framework $target.Framework --runtime win-x64 --self-contained false --output $binaryRoot --nologo
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  if (Test-Path (Join-Path $binaryRoot 'System.Management.Automation.dll')) { throw 'PowerShell reference assemblies must never be packaged.' }
  foreach ($runtimeFile in @('PSFoundation.WinRtHost.exe', 'PSFoundation.WinRtHost.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $root "build/bin/PSFoundation.WinRtHost/Release/net48/$runtimeFile") -Destination $binaryRoot
  }
}
Copy-Item -LiteralPath (Join-Path $root 'src/PSFoundation.PowerShell/compat.ps1') -Destination (Join-Path $stage 'compat.ps1')
. (Join-Path $PSScriptRoot 'help.ps1')
Write-PSFCompiledHelp -SourceRoot (Join-Path $root 'src') -Stage $stage -Commands $compiled
$quotedNames = ($compiled | ForEach-Object { "'$_'" }) -join ', '
$manifestPath = Join-Path $stage 'PSFoundation.psd1'
$manifest = [IO.File]::ReadAllText($manifestPath)
# Windows Update is native now. Importing PSWindowsUpdate also installs aliases that shadow our compiled commands.
$manifest = $manifest -replace '(?ms)^  RequiredModules\s*=\s*@\(.*?^  \)', '  RequiredModules = @()'
$quotedFunctions = ($compatibility | ForEach-Object { "'$_'" }) -join ', '
$manifest = $manifest -replace '(?s)FunctionsToExport\s*=\s*@\([^)]*\)', "FunctionsToExport = @($quotedFunctions)"
$manifest = $manifest -replace 'CmdletsToExport\s*= @\(\)', "CmdletsToExport = @($quotedNames)"
[IO.File]::WriteAllText($manifestPath, $manifest, $encoding)
$loaderPath = Join-Path $stage 'PSFoundation.psm1'
$quotedAliases = ($exports.AliasesToExport | ForEach-Object { "'$_'" }) -join ', '
$loader = 'Import-Module (Join-Path $PSScriptRoot (''lib/'' + $PSEdition + ''/PSFoundation.PowerShell.dll'')) -Scope Local -ErrorAction Stop' + "`r`n"
$loader += '. (Join-Path $PSScriptRoot ''compat.ps1'')' + "`r`n"
$loader += "Export-ModuleMember -Cmdlet @($quotedNames) -Function @($quotedFunctions) -Alias @($quotedAliases)`r`n"
[IO.File]::WriteAllText($loaderPath, $loader, $encoding)
$null = [IO.Directory]::CreateDirectory($OutputDirectory)
if ($Format -in @('Zip', 'Both')) { Compress-Archive -LiteralPath $stage -DestinationPath (Join-Path $OutputDirectory "$Name.zip") -Force }
if ($Format -in @('TarGz', 'Both')) {
  & tar -czf (Join-Path $OutputDirectory "$Name.tar.gz") -C (Split-Path $stage -Parent) PSFoundation
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Write-Output "Staged incremental module: $stage ($($compiled.Count) compiled commands, $($compatibility.Count) C#-backed compatibility functions)"
exit 0
