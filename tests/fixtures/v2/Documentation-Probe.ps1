#Requires -Version 5.1
param ([Parameter(Mandatory = $true)][string]$ModulePath)
$ErrorActionPreference = 'Stop'
Import-Module $ModulePath -Force
$commands = @(Get-Command -Module PSFoundation -CommandType Function, Cmdlet)
if ($commands.Count -ne 188) { throw "Unexpected command count: $($commands.Count)" }
foreach ($command in $commands) {
  $command = Get-Command ("PSFoundation\" + $command.Name)
  $help = Get-Help ("PSFoundation\" + $command.Name) -Full
  if (-not $help.Synopsis -or $help.Synopsis -match '^' + [regex]::Escape($command.Name) + '\s*\[') { throw "Missing help: $($command.Name)" }
  if (-not $help.Description) { throw "Missing description: $($command.Name)" }
  if (-not @($help.Examples.Example | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Code) }).Count) { throw "Missing executable example text: $($command.Name)" }
  $common = @('Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'InformationAction', 'ProgressAction', 'ErrorVariable', 'WarningVariable', 'InformationVariable', 'OutVariable', 'OutBuffer', 'PipelineVariable', 'WhatIf', 'Confirm')
  $documented = @($help.Parameters.Parameter | ForEach-Object { $_.Name })
  foreach ($name in $command.Parameters.Keys) {
    if ($name -notin $common -and $name -notin $documented) { throw "Undocumented parameter: $($command.Name) -$name" }
  }
}
Write-Output "Validated help for $($commands.Count) commands in $($PSVersionTable.PSEdition)."
