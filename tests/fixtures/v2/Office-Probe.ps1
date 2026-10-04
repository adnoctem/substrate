#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$module = Get-Module PSFoundation
$managed = (Get-Command Get-OfficeInventory).CommandType -eq 'Cmdlet'
$observations = [ordered]@{}
$observations['live-read-only-inventory'] = Get-OfficeInventory
$observations['tool-source'] = Resolve-OfficeDeploymentToolSource
$repo = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$processHost = Join-Path $repo 'build/bin/ProcessHost/Release/net48/ProcessHost.exe'
$observations['unsigned-tool'] = Test-OfficeDeploymentTool -OdtPath $processHost
$previewPath = Join-Path $repo 'build/office-tool-preview-absent'
$observations['tool-preview'] = Install-OfficeDeploymentTool -Destination $previewPath -DryRun
$observations['tool-whatif'] = Install-OfficeDeploymentTool -Destination $previewPath -WhatIf
$observations['tool-help-whatif'] = @(Get-OfficeDeploymentToolHelp -OdtPath $processHost -WhatIf)
if (Test-Path -LiteralPath $previewPath) { throw 'Tool preview created its destination.' }
$observations['unsigned-execution'] = & $module {
  param ($Executable)
  try { Invoke-PSFOfficeTool -OdtPath $Executable -Mode /help; throw 'Unsigned tool was executed.' }
  catch { if (-not $_.Exception.Data.Contains('OfficeReason')) { throw }; [string]$_.Exception.Data['OfficeReason'] }
} $processHost
foreach ($pathCase in @(@{ Name = 'protected-path'; Path = (Join-Path $repo 'README.md') }, @{ Name = 'traversal'; Path = 'C:\Media\..\Windows' })) {
  $observations[$pathCase.Name] = & $module {
    param ($Path)
    try { Assert-PSFOfficeProtectedPath $Path; [PSCustomObject]@{ Valid = $true } }
    catch { [PSCustomObject]@{ Valid = $false; Reason = $_.Exception.Data['OfficeReason']; Diagnostic = $_.Exception.Data['OfficeDiagnostic'] } }
  } $pathCase.Path
}
$target = New-OfficeDeploymentConfiguration -TargetProductId Standard2019Volume -Language de-DE, en-us, de-de -Version 16.0.10417.20095 -ExcludeApp Teams, Groove
$observations['configuration'] = $target
$observations['defaults'] = New-OfficeDeploymentConfiguration Standard2024Volume
$observations['casing'] = New-OfficeDeploymentConfiguration standard2019volume -Channel perpetualvl2019 -Language de-de -ExcludeApp teams, Groove
$observations['activation'] = @(Get-OfficeActivationStatus O365BusinessRetail; Get-OfficeActivationStatus unknown; Get-OfficeActivationStatus 0)
foreach ($case in @('Language', 'Channel', 'Locale')) {
  try {
    switch ($case) {
      Language { $null = New-OfficeDeploymentConfiguration Standard2019Volume -Language not-supported }
      Channel { $null = New-OfficeDeploymentConfiguration Standard2019Volume -Channel Current }
      Locale { $null = New-OfficeDeploymentConfiguration Standard2019Volume -LocaleSource InstalledOffice }
    }
    throw 'Invalid configuration was accepted.'
  }
  catch {
    $errorValue = $_.Exception
    while ($errorValue.InnerException -and -not $errorValue.Data.Contains('OfficeReason')) { $errorValue = $errorValue.InnerException }
    if (-not $errorValue.Data.Contains('OfficeReason')) { throw }
    $observations["invalid-$case"] = [string]$errorValue.Data['OfficeReason']
  }
}
$root = 'SOFTWARE\Microsoft\Office\ClickToRun\'
$active = '11111111-2222-3333-4444-555555555555'
$productPath = $root + 'ProductReleaseIDs\' + $active + '\Standard2019Volume.16'
function New-Record {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Builds synthetic records in memory.')]
  [CmdletBinding()]
  param ([string]$Path, [hashtable]$Values = @{}, [string[]]$SubKeys = @())
  [PSCustomObject]@{ View = 'Registry64'; Path = $Path; Values = [PSCustomObject]$Values; SubKeys = $SubKeys }
}
$records = @(
  New-Record ($root + 'Configuration') @{ ProductReleaseIds = 'Standard2019Volume'; Platform = 'x64'; CDNBaseUrl = 'https://officecdn.microsoft.com/pr/f2e724c1-748f-4b47-8fb8-8e0d210e9208'; ClientCulture = 'de-de'; VersionToReport = '16.0.99999.1'; 'Standard2019Volume.ExcludedApps' = 'Groove,Teams' }
  New-Record ($root + 'ProductReleaseIDs') @{ ActiveConfiguration = $active } @($active)
  New-Record $productPath @{} @('de-de', 'en-us', 'x-none')
  New-Record ($productPath + '\de-de') @{ Version = '16.0.10417.20095' }
  New-Record ($productPath + '\en-us') @{ Version = '16.0.10417.20095' }
  New-Record ($productPath + '\x-none') @{ Version = '16.0.10417.20095' }
)
function Read-Fixture {
  param ([object[]]$Records)
  if ($managed) {
    $rows = New-Object 'Collections.Generic.List[PSFoundation.Office.OfficeRegistryRecord]'
    foreach ($record in $Records) {
      $values = New-Object 'Collections.Generic.Dictionary[string,PSFoundation.Registry.RegistryValue]'
      foreach ($property in $record.Values.PSObject.Properties) {
        $name = if ($property.Name -eq '(default)') { '' } else { $property.Name }
        $values.Add($name, [PSFoundation.Registry.RegistryValue]::String([string]$property.Value))
      }
      $rows.Add([PSFoundation.Office.OfficeRegistryRecord]::new([Microsoft.Win32.RegistryView]::Registry64, [PSFoundation.Registry.RegistryPath]::Parse('HKLM\' + $record.Path), $values, [string[]]$record.SubKeys))
    }
    $result = [PSFoundation.Office.OfficeInventoryManager]::new().Analyze($active, $rows, $null, $null)
    return [PSFoundation.PowerShell.Office.OfficeCompatibility]::Inventory($result)
  }
  & $module {
    param ($Rows, $Machine)
    $script:OfficeProbeRecords = $Rows
    $script:OfficeProbeMachine = $Machine
    function script:Get-PSFOfficeRegistrySnapshot { $script:OfficeProbeRecords }
    function script:Get-PSFOfficeMachineId { $script:OfficeProbeMachine }
  } $Records $active
  Get-OfficeInventory
}
foreach ($case in @('Complete', 'Malformed', 'Conflict', 'Empty')) {
  $fixture = @($records)
  if ($case -eq 'Malformed') { $fixture += New-Record ($root + 'Inventory\Office\16.0') @{ OfficeProductReleaseIds = 'Standard2019Volume'; OfficePackageVersion = 'bad' } }
  if ($case -eq 'Conflict') { $fixture += New-Record ($root + 'Inventory\Office\16.0') @{ OfficeProductReleaseIds = 'ProPlus2019Volume'; OfficePackageVersion = '16.0.10417.20095' } }
  if ($case -eq 'Empty') { $fixture = @() }
  $inventory = Read-Fixture $fixture
  $observations["inventory-$case"] = $inventory
  $assessment = Test-OfficeDeployment -Configuration $target -Inventory $inventory
  $observations["assessment-$case"] = $assessment | Select-Object SchemaVersion, Compliant, Discrepancies, Unknowns
  if (($case -eq 'Complete') -ne $assessment.Compliant) { throw "Unexpected compliance for $case." }
}
$inventory = Read-Fixture $records
$target.Version = '16.0.010417.20095'
$observations['exact-version-text'] = Test-OfficeDeployment $target $inventory | Select-Object SchemaVersion, Compliant, Discrepancies, Unknowns
$observations['plan-exact-version-text'] = Get-OfficeDeploymentPlan -Action Install -Configuration $target -Inventory $inventory
$target.Version = '16.0.10417.20095'
$preference = [ordered]@{ Key = 'software\microsoft\office\16.0\word\options'; Name = 'synthetic'; Value = '<&>'; Type = 'REG_SZ'; App = 'word16'; Id = 'synthetic' }
foreach ($action in @('Install', 'Migrate', 'Update', 'Remove', 'AddLanguage', 'RemoveLanguage', 'SetApplicationSelection', 'SetUpdateConfiguration', 'SetApplicationPreference')) {
  $arguments = @{ Action = $action; Configuration = $target; Inventory = $inventory }
  $xmlArguments = @{ Action = $action; Configuration = $target; MediaPath = 'C:\Media\Office & tools' }
  switch ($action) {
    Remove {
      $arguments.Remove('Configuration'); $arguments.RemoveProductId = @('Standard2019Volume')
      $xmlArguments.Remove('Configuration'); $xmlArguments.RemoveProductId = @('Standard2019Volume')
    }
    Migrate { $arguments.RemoveProductId = @('Standard2019Volume'); $arguments.RemoveMsi = $true; $xmlArguments.RemoveMsi = $true }
    { $_ -in @('AddLanguage', 'RemoveLanguage') } { $arguments.Language = @('en-us'); $xmlArguments.Language = @('en-us') }
    SetUpdateConfiguration { $arguments.Settings = [ordered]@{ Enabled = $false; Channel = 'PerpetualVL2019' }; $xmlArguments.Settings = $arguments.Settings }
    SetApplicationPreference { $arguments.Settings = @{ Preferences = @($preference) }; $xmlArguments.Settings = $arguments.Settings }
  }
  $observations["plan-$action"] = Get-OfficeDeploymentPlan @arguments
  $observations["xml-$action"] = (& $module { param($Values) (New-PSFOfficeXml @Values).OuterXml } $xmlArguments)
}
$observations['plan-clean'] = Get-OfficeDeploymentPlan -Action Install -Configuration $target -Inventory (Read-Fixture @())
$observations['xml-download'] = & $module { param($Target) (New-PSFOfficeXml -Action Download -Configuration $Target -MediaPath 'C:\Media\Office').OuterXml } $target
$observations | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
