#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$module = Get-Module ([IO.Path]::GetFileNameWithoutExtension($ModulePath))
Add-Type -Path (Join-Path $PSScriptRoot 'OutlookFixture.cs')
$observations = [ordered]@{}
$observations['installations'] = @(Get-OutlookInstallation)
$observations['repair-tools'] = @(Find-OutlookRepairTool)
$session = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
$store = $session.AddExisting('C:\Synthetic.pst', 'synthetic')
# Identity keys retain v1 parity; refined failure states and diagnostics have a separate regression probe.
$observations['identities'] = @(Get-OutlookStandardFolderIdentity -Namespace $session -StoreRoot $store.Root | Select-Object Kind, StoreID, EntryID)
$observations['plan'] = @(Get-OutlookFolderPlan -Namespace $session -StoreRoot $store.Root -Recurse -Exclusions 'Posteingang\Skip')
$observations['explicit-root'] = @(Get-OutlookFolderPlan -Namespace $session -StoreRoot $store.Root -FolderName '' -Recurse)
$observations['root'] = (Get-OutlookStoreRoot $session).EntryID
$observations['child'] = (Get-OutlookSubFolder -ParentFolder $store.Root -Name 'Posteingang').EntryID
$observations['created'] = (Get-OutlookSubFolder -ParentFolder $store.Root -Name 'Created' -Create).Name
$observations['message-id'] = & $module { Get-TransportMessageId -HeaderText "Received: elsewhere`r`nMessage-ID: <synthetic@example.test>`r`n" }
$session.Application.Version = '12.0'
$observations['legacy-identities'] = @(Get-OutlookStandardFolderIdentity -Namespace $session -StoreRoot $store.Root)
$path = Join-Path (Split-Path $ReportPath -Parent) 'synthetic.pst'
[IO.File]::WriteAllText($path, 'synthetic')

try {
  $session = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
  $observations['preview'] = @(Open-OutlookPstStore -Namespace $session -LiteralPath $path -WhatIf)
  $context = Open-OutlookPstStore -Namespace $session -LiteralPath $path
  $observations['attached'] = $context | Select-Object StoreId, DisplayName, AttachedByCall, Closed
  $borrowed = Open-OutlookPstStore -Namespace $session -LiteralPath $path
  Close-OutlookPstStore $borrowed
  $observations['borrowed'] = $borrowed | Select-Object StoreId, AttachedByCall, Closed, Root
  Close-OutlookPstStore $context
  Close-OutlookPstStore $context
  $observations['closed'] = $context | Select-Object StoreId, AttachedByCall, Closed, Root
  $observations['counts'] = [PSCustomObject]@{ Added = $session.Added; Removed = $session.Removed; Remaining = $session.Stores.Count }
}
finally { [IO.File]::Delete($path) }

$observations | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
