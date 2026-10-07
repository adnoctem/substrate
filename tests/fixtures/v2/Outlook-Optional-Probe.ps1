#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
Add-Type -Path (Join-Path $PSScriptRoot 'OutlookFixture.cs')
$session = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
$store = $session.AddExisting('C:\Synthetic.pst', 'optional-failure')
$contacts = $store.Standard[6].Folders.Add('Renamed contacts')
$contacts.DefaultItemType = 2
$child = $contacts.Folders.Add('NeverVisit')
$store.StandardErrors[30] = [Runtime.InteropServices.COMException]::new('Synthetic aborted lookup.', [AdNoctem.Substrate.Tests.Fixtures.OutlookFixture]::E_ABORT)
$creations = $store.FolderCreations
$warnings = @()
$identities = @(Get-OutlookStandardFolderIdentity -Namespace $session -StoreRoot $store.Root -WarningVariable warnings -WarningAction SilentlyContinue)
$planWarnings = @()
$plan = @(Get-OutlookFolderPlan -Namespace $session -StoreRoot $store.Root -Recurse -WarningVariable planWarnings -WarningAction SilentlyContinue)
$included = @(Get-OutlookFolderPlan -Namespace $session -StoreRoot $store.Root -Recurse -Include Inbox, SuggestedContacts -WarningAction SilentlyContinue)
$observations = [ordered]@{
  IdentityCount      = $identities.Count
  IdentityFields     = @($identities[0].PSObject.Properties.Name)
  Suggested          = @($identities | Where-Object Kind -EQ SuggestedContacts)[0]
  IdentityWarnings   = @($warnings | ForEach-Object { $_.ToString() })
  PlanWarnings       = @($planWarnings | ForEach-Object { $_.ToString() })
  PlanFields         = @($plan[0].PSObject.Properties.Name)
  InboxProcessed     = $plan[0].Process
  KeepProcessed      = @($plan | Where-Object RelativePath -EQ 'Posteingang\Keep')[0].Process
  Unsafe             = @($plan | Where-Object EntryID -EQ $contacts.EntryID)[0]
  DescendantSkipped  = @($plan | Where-Object EntryID -EQ $child.EntryID).Count -eq 0
  IncludedDescendant = @($included | Where-Object EntryID -EQ $child.EntryID)[0].Process
  NoFolderCreation   = $store.FolderCreations -eq $creations
  ItemAccesses       = $store.ItemAccesses
}

$store.StandardErrors.Clear()
$null = $store.Root.Folders.Values.Remove($store.Standard[6])
$null = $store.Standard.Remove(6)
$null = $store.Root.Folders.Add('Archive')
$observations['RequiredFailure'] = ''

try { $null = Get-OutlookFolderPlan -Namespace $session -StoreRoot $store.Root }
catch { $observations['RequiredFailure'] = $_.Exception.Message }

$observations['ExplicitArchive'] = @(Get-OutlookFolderPlan -Namespace $session -StoreRoot $store.Root -FolderName Archive)[0].Process
$observations['ExplicitRoot'] = @(Get-OutlookFolderPlan -Namespace $session -StoreRoot $store.Root -FolderName '' -Recurse | Where-Object RelativePath -EQ Archive)[0].Process
$path = Join-Path (Split-Path $ReportPath -Parent) 'optional-preview.pst'
$existingPath = Join-Path (Split-Path $ReportPath -Parent) 'optional-existing.pst'
[IO.File]::WriteAllText($path, 'synthetic')
[IO.File]::WriteAllText($existingPath, 'synthetic')
$store.FilePath = $existingPath
$context = $null

try {
  $observations['PreviewCount'] = @(Open-OutlookPstStore -Namespace $session -LiteralPath $path -WhatIf).Count
  $observations['PreviewAttachments'] = $session.Added
  $context = Open-OutlookPstStore -Namespace $session -LiteralPath $path
  $ownedStore = $session.GetStoreFromID($context.StoreId)
  $ownedStore.StandardErrors[30] = [Runtime.InteropServices.COMException]::new('Synthetic aborted lookup.', [AdNoctem.Substrate.Tests.Fixtures.OutlookFixture]::E_ABORT)
  $observations['StoppedDiagnostic'] = $false

  try { $null = Get-OutlookFolderPlan -Namespace $session -StoreRoot $context.Root -WarningAction Stop }
  catch { $observations['StoppedDiagnostic'] = $_.Exception.Message -match 'SuggestedContacts' }
}
finally {
  if ($null -ne $context) { Close-OutlookPstStore -Context $context }
  [IO.File]::Delete($path)
  [IO.File]::Delete($existingPath)
}

$observations['Cleanup'] = $context.Closed -and $null -eq $context.Root -and $session.Removed -eq 1 -and $session.Stores.Count -eq 1
$observations | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
