#Requires -Version 5.1
<#
.SYNOPSIS
  Captures API contracts from the pinned, immutable v1 source in a fresh host.
.PARAMETER WriteFixtures
  Writes reviewed-source captures to tests/Fixtures/Api rather than build/baseline.
.PARAMETER Inventory
  Records public command dependencies and migration status in build/baseline/commands.json.
.EXAMPLE
  dotnet msbuild tools/tasks.proj -t:Baseline
#>
[CmdletBinding()]
param ([switch]$WriteFixtures, [switch]$Inventory)

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

# The v1 archive declares CRLF text; verify its normalized Git content independently of current checkout settings.
$baselineFiles = @(& git -C $root ls-tree -r --name-only $commit -- src)

foreach ($file in $baselineFiles) {
  $expected = & git -C $root rev-parse "${commit}:$file"
  $actual = & git -c core.autocrlf=true -C $root hash-object "--path=$file" (Join-Path $destination $file)

  if ($LASTEXITCODE -ne 0 -or $actual -ne $expected) { throw "Pinned baseline differs at '$file'." }
}

. (Join-Path $PSScriptRoot 'compatibility.ps1')
$contract = Get-SubstrateApiContract -ModulePath (Join-Path $destination 'src/PSFoundation.psd1')

if ($contract.Commands.Count -ne 192) { throw 'The v1 baseline must contain 188 functions and four aliases.' }

$outputRoot = Join-Path $root 'build/baseline'

if ($WriteFixtures) { $outputRoot = Join-Path $root 'tests/Fixtures/Api' }

$null = [IO.Directory]::CreateDirectory($outputRoot)
$capture = [ordered]@{
  SourceCommit = $commit; SourceVersion = '1.8.7'; Edition = $PSVersionTable.PSEdition
  PowerShellVersion = [string]$PSVersionTable.PSVersion; Contract = $contract
}
$path = Join-Path $outputRoot ($PSVersionTable.PSEdition.ToLowerInvariant() + '.json')
[IO.File]::WriteAllText($path, (($capture | ConvertTo-Json -Depth 40) + "`n"), (New-Object Text.UTF8Encoding($false)))
Write-Output "Captured $($contract.Commands.Count) exports from v1.8.7 to $path"

if ($Inventory) {
  $catalog = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compiled-commands.psd1')
  $compiled = @($catalog.Values | ForEach-Object { $_ })
  $compatibilityCatalog = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'compatibility-commands.psd1')
  $compatibility = @($compatibilityCatalog.Values | ForEach-Object { $_ })

  $rows = foreach ($file in Get-ChildItem (Join-Path $destination 'src') -Filter '*.ps1') {
    $ast = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$null, [ref]$null)

    foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
      if ($function.Name -notin $contract.Commands.Name) { continue }

      $calls = @($function.Body.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true) |
          ForEach-Object { $_.GetCommandName() } | Where-Object { $_ } | Sort-Object -Unique)

      $implementation = if ($function.Name -in $compiled) { 'CompiledCmdlet' } elseif ($function.Name -in $compatibility) { 'CompatibilityFunction' } else { 'LegacyFunction' }

      [PSCustomObject][ordered]@{ Command = $function.Name; Source = $file.Name; Compiled = $function.Name -in $compiled; Implementation = $implementation; Calls = $calls }
    }
  }

  $inventoryPath = Join-Path $root 'build/baseline/commands.json'
  [IO.File]::WriteAllText($inventoryPath, (($rows | ConvertTo-Json -Depth 8) + "`n"), (New-Object Text.UTF8Encoding($false)))
  $rows | Group-Object Source | ForEach-Object { Write-Output ("{0}: {1} commands" -f $_.Name, $_.Count) }
}
