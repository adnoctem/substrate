#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
. (Join-Path $root 'tools/compatibility.ps1')
$catalog = Import-PowerShellDataFile (Join-Path $root 'tools/compiled-commands.psd1')
$contract = Get-PSFApiContract -ModulePath $ModulePath
$contract.Commands = @($contract.Commands | Where-Object Name -In $catalog.Registry)
foreach ($command in $contract.Commands) {
  # CommandType changes deliberately; compare everything else, including validation.
  $command.Kind = 'MigratedCommand'
  foreach ($set in $command.ParameterSets) {
    foreach ($parameter in $set.Parameters) {
      # Script parameters carry an internal binder implementation attribute.
      $parameter.Attributes = @($parameter.Attributes | Where-Object Type -NotIn @('System.Management.Automation.ArgumentTypeConverterAttribute', 'System.Runtime.CompilerServices.NullableAttribute'))
    }
  }
}
[IO.File]::WriteAllText($ReportPath, ($contract | ConvertTo-Json -Depth 40), (New-Object Text.UTF8Encoding($false)))

if ((Get-Command ConvertTo-RegistryProviderPath).CommandType -eq 'Cmdlet') {
  foreach ($command in $catalog.Registry) {
    $owners = @(Get-Command -Name $command -Module PSFoundation -All)
    if ($owners.Count -ne 1 -or $owners[0].CommandType -ne 'Cmdlet') { throw "Ambiguous owner: $command" }
    $help = Get-Help "PSFoundation\$command" -Full
    if (-not $help.Synopsis -or $help.Synopsis.Contains('[[')) { throw "Missing help: $command" }
  }
  if (Test-Path (Join-Path (Split-Path $ModulePath -Parent) 'registry.ps1')) { throw 'Legacy registry implementation remains in staged module.' }
}
