#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
Add-Type -Path (Join-Path $PSScriptRoot 'OutlookFixture.cs')

$directory = Split-Path $ReportPath -Parent
$readablePath = Join-Path $directory 'unavailable-readable.pst'
[IO.File]::WriteAllBytes($readablePath, (New-Object byte[] 128))

$session = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
$readable = $session.AddExisting($readablePath, 'readable-store')
$readable.DisplayName = 'Readable local store'
$readable.ExchangeStoreType = 3
$unavailable = $session.AddExisting(
  (Join-Path $directory 'unavailable-outlook.ost'),
  'unavailable-store'
)
$unavailable.DisplayName = 'Unavailable mailbox'
$unavailable.ExchangeStoreType = 0
$unavailable.HideIdentity = $true
$null = $session.AddAccount('mailbox@synthetic.test', 2, $readable)
$unavailableAccount = $session.AddAccount('unavailable@synthetic.test', 0, $null)
$unavailableAccount.HideDisplayName = $true
$creations = $readable.FolderCreations

try {
  $accounts = @(Get-OutlookAccount -Namespace $session)
  $stores = @(Get-OutlookStore -Namespace $session)
  $observations = [ordered]@{
    AccountCount       = $accounts.Count
    StoreCount         = $stores.Count
    StoreFields        = @($stores[0].PSObject.Properties.Name)
    ReadableStore      = @($stores | Where-Object StoreID -EQ 'readable-store')[0]
    UnavailableStore   = @($stores | Where-Object { -not $_.IsAvailable })[0]
    ReadableAccount    = @($accounts | Where-Object DisplayName -EQ 'mailbox@synthetic.test')[0]
    UnavailableAccount = $accounts[1]
    ReadableDelivered  = @(
      @($stores | Where-Object StoreID -EQ 'readable-store')[0].DeliveredFrom
    )
    ItemAccesses       = $readable.ItemAccesses
    FolderCreations    = ($readable.FolderCreations - $creations)
  }

  $observations | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
}
finally {
  [IO.File]::Delete($readablePath)
}
