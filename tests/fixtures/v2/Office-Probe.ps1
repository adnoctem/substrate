#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$module = Get-Module ([IO.Path]::GetFileNameWithoutExtension($ModulePath))
$managed = (Get-Command Get-OfficeInventory).CommandType -eq 'Cmdlet'

$privatePrefix = if ($managed) { 'Substrate' } else { 'PSF' }

$observations = [ordered]@{}
$observations['live-read-only-inventory'] = Get-OfficeInventory

if ($managed) {
  $nativeInventory = [AdNoctem.Substrate.Office.OfficeInventoryManager]::new().Read([Threading.CancellationToken]::None)
  $wireInventory = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Inventory($nativeInventory)
  $wireHash = & $module { param($Value) Get-SubstrateOfficeFingerprint $Value } $wireInventory

  if ($wireHash -cne [AdNoctem.Substrate.Office.OfficeInventorySerializer]::GetFingerprint($nativeInventory)) { throw 'Native inventory serialization changed its fingerprint.' }

  $edgeInventory = [AdNoctem.Substrate.Office.OfficeInventoryManager]::new().Analyze("Synthetic '\`"<>&`n" + [char]0x2028, [AdNoctem.Substrate.Office.OfficeRegistryRecord[]]@(), $null, $null)
  $edgeHash = & $module { param($Value) Get-SubstrateOfficeFingerprint $Value } ([AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Inventory($edgeInventory))

  if ($edgeHash -cne [AdNoctem.Substrate.Office.OfficeInventorySerializer]::GetFingerprint($edgeInventory, ($PSVersionTable.PSVersion.Major -le 5))) {
    $expectedJson = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Inventory($edgeInventory) | ConvertTo-Json -Depth 30 -Compress
    $actualJson = [AdNoctem.Substrate.Office.OfficeInventorySerializer]::ToJson($edgeInventory, ($PSVersionTable.PSVersion.Major -le 5))

    throw "Native synthetic inventory escaping differs: expected $expectedJson; actual $actualJson"
  }
}

$observations['tool-source'] = Resolve-OfficeDeploymentToolSource
$observations['host-platform'] = & $module { param($Prefix) & ("Test-{0}OfficeHost" -f $Prefix) } $privatePrefix
$repo = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$processHost = Join-Path $repo 'build/bin/ProcessHost/Release/net48/ProcessHost.exe'
$observations['unsigned-tool'] = Test-OfficeDeploymentTool -OdtPath $processHost
$rejectedMedia = Test-OfficeDeploymentMedia -SourcePath (Join-Path $repo 'README.md')
$observations['untrusted-media'] = $rejectedMedia | Select-Object Valid, Path, Manifest, Fingerprint, ReasonCode, Error, Diagnostic

if ($rejectedMedia.ReasonCode -eq 'ReprepareMedia') {
  # Source locations and CLR exception types differ between scripts and compiled commands.
  $expectedException = if ($managed) { 'AdNoctem.Substrate.Office.OfficeException' } else { 'System.InvalidOperationException' }

  if ($rejectedMedia.Diagnostic.ExceptionType -cne $expectedException) { throw 'Unexpected media exception type.' }

  foreach ($property in @('ExceptionType', 'ScriptPath', 'Line')) { $observations['untrusted-media'].Diagnostic.PSObject.Properties.Remove($property) }
}

$previewPath = Join-Path $repo 'build/office-tool-preview-absent'
$observations['tool-preview'] = Install-OfficeDeploymentTool -Destination $previewPath -DryRun
$observations['tool-whatif'] = Install-OfficeDeploymentTool -Destination $previewPath -WhatIf
$observations['tool-help-whatif'] = @(Get-OfficeDeploymentToolHelp -OdtPath $processHost -WhatIf)

if (Test-Path -LiteralPath $previewPath) { throw 'Tool preview created its destination.' }

$observations['unsigned-execution'] = & $module {
  param ($Executable, $Managed)

  $toolCommand = if ($Managed) { 'Invoke-SubstrateOfficeTool' } else { 'Invoke-PSFOfficeTool' }

  try { & $toolCommand -OdtPath $Executable -Mode /help; throw 'Unsigned tool was executed.' }
  catch { if (-not $_.Exception.Data.Contains('OfficeReason')) { throw }; [string]$_.Exception.Data['OfficeReason'] }
} $processHost $managed

foreach ($pathCase in @(@{ Name = 'protected-path'; Path = (Join-Path $repo 'README.md') }, @{ Name = 'traversal'; Path = 'C:\Media\..\Windows' })) {
  $observations[$pathCase.Name] = & $module {
    param ($Path, $Prefix)

    try { & ("Assert-{0}OfficeProtectedPath" -f $Prefix) $Path; [PSCustomObject]@{ Valid = $true } }
    catch { [PSCustomObject]@{ Valid = $false; Reason = $_.Exception.Data['OfficeReason']; Diagnostic = $_.Exception.Data['OfficeDiagnostic'] } }
  } $pathCase.Path $privatePrefix
}

$target = New-OfficeDeploymentConfiguration -TargetProductId Standard2019Volume -Language de-DE, en-us, de-de -Version 16.0.10417.20095 -ExcludeApp Teams, Groove
$observations['configuration'] = $target
$observations['defaults'] = New-OfficeDeploymentConfiguration Standard2024Volume
$live = $observations['live-read-only-inventory']
$absentProduct = @('Standard2019Volume', 'ProPlus2019Volume', 'Standard2021Volume', 'ProPlus2021Volume' | Where-Object { $_ -notin @($live.Products | ForEach-Object { $_.ProductId }) })[0]
$absentPlan = Get-OfficeDeploymentPlan -Action Remove -RemoveProductId $absentProduct -Inventory $live
$observations['remove-read-only-noop'] = Uninstall-Office -Plan $absentPlan -OdtPath $processHost -DryRun |
  Select-Object Target, Source, Action, Status, Phase, ReasonCode, Changed, ChangeKnown, AlreadyCompliant, WrapperExitCode, RecoveryRequired
$observations['execution-action-guard'] = & {
  try { Install-Office -Plan $absentPlan -OdtPath $processHost -DryRun; throw 'Mismatched plan action was accepted.' }
  catch { if (-not $_.Exception.Data.Contains('OfficeReason')) { throw }; [string]$_.Exception.Data['OfficeReason'] }
}
$observations['media-preview-rejects-unsigned-tool'] = & {
  try { Save-OfficeDeploymentMedia -Configuration $target -SourcePath $previewPath -OdtPath $processHost -DryRun; throw 'Unsigned tool was accepted.' }
  catch { if (-not $_.Exception.Data.Contains('OfficeReason')) { throw }; [string]$_.Exception.Data['OfficeReason'] }
}
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
    $rows = New-Object 'Collections.Generic.List[AdNoctem.Substrate.Office.OfficeRegistryRecord]'

    foreach ($record in $Records) {
      $values = New-Object 'Collections.Generic.Dictionary[string,AdNoctem.Substrate.Registry.RegistryValue]'

      foreach ($property in $record.Values.PSObject.Properties) {
        $name = if ($property.Name -eq '(default)') { '' } else { $property.Name }

        $values.Add($name, [AdNoctem.Substrate.Registry.RegistryValue]::String([string]$property.Value))
      }

      $rows.Add([AdNoctem.Substrate.Office.OfficeRegistryRecord]::new([Microsoft.Win32.RegistryView]::Registry64, [AdNoctem.Substrate.Registry.RegistryPath]::Parse('HKLM\' + $record.Path), $values, [string[]]$record.SubKeys))
    }

    $result = [AdNoctem.Substrate.Office.OfficeInventoryManager]::new().Analyze($active, $rows, $null, $null)

    return [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Inventory($result)
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

foreach ($case in @('Complete', 'Malformed', 'Conflict', 'Empty', 'Unclassified')) {
  $fixture = @($records)

  if ($case -eq 'Malformed') { $fixture += New-Record ($root + 'Inventory\Office\16.0') @{ OfficeProductReleaseIds = 'Standard2019Volume'; OfficePackageVersion = 'bad' } }

  if ($case -eq 'Conflict') { $fixture += New-Record ($root + 'Inventory\Office\16.0') @{ OfficeProductReleaseIds = 'ProPlus2019Volume'; OfficePackageVersion = '16.0.10417.20095' } }

  if ($case -eq 'Empty') { $fixture = @() }

  if ($case -eq 'Unclassified') {
    $uninstall = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\'
    $fixture += New-Record ($uninstall + 'Synthetic Office Runtime') @{ Publisher = 'Microsoft Corporation'; DisplayName = 'Synthetic Office Runtime' }
    $fixture += New-Record ($uninstall + '{11111111-2222-3333-4444-555555555555}') @{ Publisher = 'Microsoft Corporation'; DisplayName = 'Synthetic Office Tools' }
  }

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

  if ($managed) {
    $parsed = [AdNoctem.Substrate.Office.OfficeDeploymentPlanDocument]::Parse(($observations["plan-$action"] | ConvertTo-Json -Depth 30 -Compress))

    if ([string]$parsed.Request.Action -ne $action) { throw "Plan round-trip changed $action." }
  }

  $observations["xml-$action"] = (& $module { param($Values, $Prefix) (& ("New-{0}OfficeXml" -f $Prefix) @Values).OuterXml } $xmlArguments $privatePrefix)
}

$observations['plan-clean'] = Get-OfficeDeploymentPlan -Action Install -Configuration $target -Inventory (Read-Fixture @())
$observations['xml-download'] = & $module { param($Target, $Prefix) (& ("New-{0}OfficeXml" -f $Prefix) -Action Download -Configuration $Target -MediaPath 'C:\Media\Office').OuterXml } $target $privatePrefix
$beforeRecovery = Read-Fixture $records
$recoveryPlan = Get-OfficeDeploymentPlan -Action Migrate -Configuration $target -Inventory $beforeRecovery -RemoveProductId Standard2019Volume

if ($managed) {
  $document = [AdNoctem.Substrate.Office.OfficeDeploymentPlanDocument]::Parse(($recoveryPlan | ConvertTo-Json -Depth 30 -Compress))
  $record = [PSCustomObject][ordered]@{
    SchemaVersion = 1; RunId = ('a' * 32); MachineId = $recoveryPlan.MachineId; Action = 'Migrate'; Plan = $recoveryPlan
    ConfigurationFingerprint = (& $module { param($Value) Get-SubstrateOfficeFingerprint $Value } $recoveryPlan.Configuration)
    MediaFingerprint = $recoveryPlan.MediaFingerprint; Phase = 'StageMedia'; PhaseCompleted = $false; NativeResults = @(); RebootRequired = $false
    CreatedAt = '2026-01-01T00:00:00Z'; UpdatedAt = '2026-01-01T00:00:00Z'; Result = $null
  }
  $nativeRecord = [AdNoctem.Substrate.Office.OfficeRecoveryRecord]::Parse(($record | ConvertTo-Json -Depth 30 -Compress), $record.RunId, $record.MachineId)

  if ($nativeRecord.Plan.InventoryFingerprint -cne $document.InventoryFingerprint) { throw 'Recovery parsing changed its recorded inventory.' }
}

& $module {
  function script:Get-OfficeInventory { $script:RecoveryProbeInventory }

  function script:Get-OfficeDeploymentRecovery { [PSCustomObject]@{ Path = 'C:\Synthetic\recovery.json'; Record = $script:RecoveryProbeRecord } }

  function script:Get-PSFOfficeActivity { [PSCustomObject]@{ Busy = $false; Apps = @() } }

  function script:Test-PendingReboot { [PSCustomObject]@{ PendingReboot = $false } }

  function script:Test-OfficeDeploymentMedia {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Test double preserves the public command name.')]
    param ()

    [PSCustomObject]@{ Valid = $true; Fingerprint = 'synthetic'; Manifest = [PSCustomObject]@{ Version = '16.0.10417.20095' } }
  }

  function script:Get-OfficeActivationStatus { [PSCustomObject]@{ Status = 'Licensed' } }

  function script:Invoke-PSFOfficeWorkflow {
    param ($Plan)

    [PSCustomObject]@{ Status = 'WouldExecute'; Action = $Plan.Action; RemoveProductId = $Plan.RemoveProductId; RemoveMsi = $Plan.RemoveMsi }
  }
}

foreach ($case in @('BeforeLaunch', 'Removed', 'Partial', 'Conflict', 'Complete')) {
  $current = $beforeRecovery | ConvertTo-Json -Depth 25 | ConvertFrom-Json
  $current.Products[0].Version = '16.0.10417.29999'
  $phase = 'StageMedia'
  $completed = $false

  if ($case -eq 'Removed') { $current.Products = @(); $phase = 'Remove'; $completed = $true }

  if ($case -eq 'Partial') { $phase = 'Migrate' }

  if ($case -eq 'Conflict') { $current.Products[0].ProductId = 'VisioPro2019Volume' }

  if ($case -eq 'Complete') { $current = $beforeRecovery; $phase = 'Verify'; $completed = $true }

  $record = [PSCustomObject]@{ SchemaVersion = 1; Action = 'Migrate'; Plan = $recoveryPlan; Phase = $phase; PhaseCompleted = $completed; MediaFingerprint = 'synthetic' }
  $observations["recovery-$case"] = & $module {
    param ($Current, $Record)

    if ('AdNoctem.Substrate.Office.OfficeRecoveryPolicy' -as [type]) {
      # Compare the native decision policy with the frozen script coordinator.
      # End-to-end native execution/recovery is exercised separately with an offline runtime.
      $compliant = (Test-OfficeDeployment -Configuration $Record.Plan.Configuration -Inventory $Current).Compliant
      $decision = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::RecoveryDecision($Record, $Current, 'Migrate', $compliant)

      if ($decision.Disposition -eq 'Continue') {
        return [PSCustomObject]@{ Status = 'WouldExecute'; Action = 'Migrate'; ReasonCode = $null; Error = $null; RecoveryRequired = $null; WrapperExitCode = $null; AlreadyCompliant = $null; RemoveProductId = @($decision.RemainingRemovalIds); RemoveMsi = $Record.Plan.RemoveMsi }
      }

      return [PSCustomObject]@{ Status = $(if ($compliant) { 'Completed' } else { 'Blocked' }); Action = 'Recover'; ReasonCode = $decision.ReasonCode; Error = $decision.Detail; RecoveryRequired = $decision.RecoveryRequired; WrapperExitCode = $decision.WrapperExitCode; AlreadyCompliant = $compliant; RemoveProductId = $null; RemoveMsi = $null }
    }

    $script:RecoveryProbeInventory = $Current
    $script:RecoveryProbeRecord = $Record
    Invoke-PSFOfficeRecovery -Recovery ([PSCustomObject]@{ RunId = '11111111111111111111111111111111'; LogRoot = 'C:\Synthetic' }) -OriginalAction Migrate -OdtPath 'C:\Synthetic\setup.exe' -DryRun $true |
      Select-Object Status, Action, ReasonCode, Error, RecoveryRequired, WrapperExitCode, AlreadyCompliant, RemoveProductId, RemoveMsi
  } $current $record
}

$observations | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
