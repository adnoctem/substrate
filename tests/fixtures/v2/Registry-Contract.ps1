#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
. (Join-Path $root 'tools/compatibility.ps1')
$catalog = Import-PowerShellDataFile (Join-Path $root 'tools/compiled-commands.psd1')
$contract = Get-PSFApiContract -ModulePath $ModulePath
$compiled = @($catalog.Values | ForEach-Object { $_ })
$contract.Commands = @($contract.Commands | Where-Object Name -In $compiled)
foreach ($command in $contract.Commands) {
  # CommandType changes deliberately; compare everything else, including validation.
  $command.Kind = 'MigratedCommand'
  # The two script validators have no CmdletBinding attribute. Their inactive ConfirmImpact can report Medium;
  # compiled metadata reports None when SupportsShouldProcess is false. Binding and behavior remain compared.
  if ($command.Name -in @('Test-IPv4Address', 'Test-IPv6Address') -and -not $command.ShouldProcess -and $command.ConfirmImpact -in @('None', 'Medium')) {
    $command.ConfirmImpact = 'None'
  }
  foreach ($set in $command.ParameterSets) {
    foreach ($parameter in $set.Parameters) {
      # Script parameters carry an internal binder implementation attribute.
      $parameter.Attributes = @($parameter.Attributes | Where-Object Type -NotIn @('System.Management.Automation.ArgumentTypeConverterAttribute', 'System.Runtime.CompilerServices.NullableAttribute', 'PSFoundation.PowerShell.Infrastructure.UnwrapListAttribute'))
    }
  }
}
[IO.File]::WriteAllText($ReportPath, ($contract | ConvertTo-Json -Depth 40), (New-Object Text.UTF8Encoding($false)))

if ((Get-Command ConvertTo-RegistryProviderPath).CommandType -eq 'Cmdlet') {
  foreach ($command in $compiled) {
    $owners = @(Get-Command -Name $command -Module PSFoundation -All)
    if ($owners.Count -ne 1 -or $owners[0].CommandType -ne 'Cmdlet') { throw "Ambiguous owner: $command" }
    $help = Get-Help "PSFoundation\$command" -Full
    if (-not $help.Synopsis -or $help.Synopsis.Contains('[[')) { throw "Missing help: $command" }
  }
  foreach ($script in @('registry.ps1', 'common.ps1', 'errors.ps1')) {
    if (Test-Path (Join-Path (Split-Path $ModulePath -Parent) $script)) { throw "Migrated legacy implementation remains in staged module: $script" }
  }
}
