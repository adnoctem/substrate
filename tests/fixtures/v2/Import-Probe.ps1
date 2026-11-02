#Requires -Version 5.1
param ([Parameter(Mandatory = $true)][string]$ModulePath)

$ErrorActionPreference = 'Stop'
$module = Import-Module $ModulePath -Force -PassThru

if ($module.Name -ne 'AdNoctem.Substrate.PowerShell' -or $module.ExportedCommands.Count -ne 194) { throw 'Incremental package lost exports or module identity.' }

foreach ($name in @('ConvertTo-RegistryProviderPath', 'Resolve-RegistryPath', 'New-OperationResult')) {
  $commands = @(Get-Command -Name $name -Module AdNoctem.Substrate.PowerShell -All)

  if ($commands.Count -ne 1 -or $commands[0].CommandType -ne 'Cmdlet') { throw "Command '$name' does not have one compiled owner." }
}

if ((AdNoctem.Substrate.PowerShell\ConvertTo-RegistryProviderPath 'Registry::HKEY_CURRENT_USER\Software') -cne 'HKCU:\Software') { throw 'Compiled registry normalization differs.' }

$key = AdNoctem.Substrate.PowerShell\Resolve-RegistryPath 'HKCU:\Software'

try {
  if ($key -isnot [Microsoft.Win32.RegistryKey] -or $key.Name -ne 'HKEY_CURRENT_USER\Software') { throw 'Compiled registry handle cannot be used in this host.' }
}
finally { if ($null -ne $key) { $key.Dispose() } }

$result = AdNoctem.Substrate.PowerShell\New-OperationResult -Target 'synthetic' -Action 'inspect' -Status 'Completed' -Before $null -Changed:$false

if ($result.Target -ne 'synthetic' -or $result.Changed -ne $false -or $null -ne $result.Before) { throw 'Result mapping differs.' }

if ($result.PSObject.Properties.Name -contains 'After') { throw 'An omitted result field was materialized.' }

$failures = @()
$null = AdNoctem.Substrate.PowerShell\ConvertTo-RegistryProviderPath 'NONSENSE\Path' -ErrorAction SilentlyContinue -ErrorVariable failures

if ($failures.Count -ne 1 -or $failures[0].Exception.GetType().FullName -ne 'Microsoft.PowerShell.Commands.WriteErrorException') { throw 'Nonterminating error mapping differs.' }

if (Test-Path (Join-Path (Split-Path $ModulePath -Parent) 'System.Management.Automation.dll')) { throw 'Package contains a competing PowerShell runtime.' }

if ($PSVersionTable.PSEdition -eq 'Core') {
  if ([AdNoctem.Substrate.Documents.PressReleaseTags]::SchemaId -cne 'press-release/1') { throw 'Documents types are not directly usable in PowerShell 7.' }
  if (-not [AdNoctem.Substrate.Documents.TemplatePack]) { throw 'TemplatePack type is unavailable in PowerShell 7.' }
}
elseif ($null -ne ('AdNoctem.Substrate.Documents.PressReleaseTags' -as [type])) {
  throw 'Documents types must stay out of the Windows PowerShell edition.'
}

Write-Output "Import and compiled-command smoke tests passed: $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)"
