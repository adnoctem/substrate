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
$retiredPrivate = @('Read-PSFPolicyString', 'Assert-PSFPolicyDelimiter', 'ConvertFrom-PSFPolicyPayload', 'ConvertTo-PSFPolicyPayload', 'Resolve-IPv6PrefixData')
if ($compiled.Count -ne @($compiled | Sort-Object -Unique).Count) { throw 'Compiled command catalog contains duplicate entries.' }
if ($compatibility.Count -ne @($compatibility | Sort-Object -Unique).Count -or @($compatibility | Where-Object { $_ -in $compiled }).Count) { throw 'Compatibility commands must have unique owners.' }
$encoding = New-Object Text.UTF8Encoding($true)
foreach ($file in @(Get-ChildItem (Join-Path $root 'src') -File)) {
  if ($file.Name -in @('registry.ps1', 'common.ps1', 'errors.ps1', 'log.ps1', 'policies.ps1', 'devices.ps1')) { continue }
  $destination = Join-Path $stage $file.Name
  if ($file.Extension -ne '.ps1') { Copy-Item -LiteralPath $file.FullName -Destination $destination; continue }
  $text = [IO.File]::ReadAllText($file.FullName)
  $tokens = $null
  $parseErrors = $null
  $ast = [Management.Automation.Language.Parser]::ParseInput($text, [ref]$tokens, [ref]$parseErrors)
  if ($parseErrors.Count) { throw "Cannot stage invalid script '$($file.Name)'." }
  # The compiled Restart Manager API owns this binding now; remove its old top-level Add-Type block from staging only.
  if ($file.Name -eq 'system.ps1') {
    $binding = $ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.IfStatementAst] -and $_.Extent.Text -match 'MyCore\.Utils\.FileLockUtil' }
    if (@($binding).Count -ne 1) { throw 'Expected one legacy Restart Manager binding.' }
    $text = $text.Remove($binding.Extent.StartOffset, $binding.Extent.EndOffset - $binding.Extent.StartOffset)
    $ast = [Management.Automation.Language.Parser]::ParseInput($text, [ref]$tokens, [ref]$parseErrors)
  }
  $definitions = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and ($node.Name -in $compiled -or $node.Name -in $compatibility -or $node.Name -in $retiredPrivate) }, $true) | Sort-Object { $_.Extent.StartOffset } -Descending)
  foreach ($definition in $definitions) { $text = $text.Remove($definition.Extent.StartOffset, $definition.Extent.EndOffset - $definition.Extent.StartOffset) }
  [IO.File]::WriteAllText($destination, $text, $encoding)
}
foreach ($target in @(@{ Edition = 'Desktop'; Framework = 'net48' }, @{ Edition = 'Core'; Framework = 'netstandard2.0' })) {
  $binaryRoot = Join-Path $stage ('lib/' + $target.Edition)
  & dotnet publish (Join-Path $root 'src/PSFoundation.PowerShell/PSFoundation.PowerShell.csproj') --no-restore --configuration Release --framework $target.Framework --runtime win-x64 --self-contained false --output $binaryRoot --nologo
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  if (Test-Path (Join-Path $binaryRoot 'System.Management.Automation.dll')) { throw 'PowerShell reference assemblies must never be packaged.' }
}
Copy-Item -LiteralPath (Join-Path $root 'src/PSFoundation.PowerShell/compat.ps1') -Destination (Join-Path $stage 'compat.ps1')
. (Join-Path $PSScriptRoot 'help.ps1')
Write-PSFCompiledHelp -SourceRoot (Join-Path $root 'src') -Stage $stage -Commands $compiled
$quotedNames = ($compiled | ForEach-Object { "'$_'" }) -join ', '
$manifestPath = Join-Path $stage 'PSFoundation.psd1'
$manifest = [IO.File]::ReadAllText($manifestPath)
foreach ($command in $compiled) { $manifest = $manifest -replace ("(?m)^\s*'" + [regex]::Escape($command) + "',?\r?\n"), '' }
$manifest = $manifest -replace 'CmdletsToExport\s*= @\(\)', "CmdletsToExport = @($quotedNames)"
[IO.File]::WriteAllText($manifestPath, $manifest, $encoding)
$loaderPath = Join-Path $stage 'PSFoundation.psm1'
$loader = [IO.File]::ReadAllText($loaderPath)
$loader = 'Import-Module (Join-Path $PSScriptRoot (''lib/'' + $PSEdition + ''/PSFoundation.PowerShell.dll'')) -Scope Local -ErrorAction Stop' + "`r`n" + $loader
$loader += "`r`nExport-ModuleMember -Cmdlet @($quotedNames)`r`n"
[IO.File]::WriteAllText($loaderPath, $loader, $encoding)
$null = [IO.Directory]::CreateDirectory($OutputDirectory)
if ($Format -in @('Zip', 'Both')) { Compress-Archive -LiteralPath $stage -DestinationPath (Join-Path $OutputDirectory "$Name.zip") -Force }
if ($Format -in @('TarGz', 'Both')) {
  & tar -czf (Join-Path $OutputDirectory "$Name.tar.gz") -C (Split-Path $stage -Parent) PSFoundation
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Write-Output "Staged incremental module: $stage ($($compiled.Count) compiled commands, $($compatibility.Count) C#-backed compatibility functions)"
exit 0
