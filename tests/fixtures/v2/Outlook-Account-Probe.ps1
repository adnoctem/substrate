#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
Add-Type -Path (Join-Path $PSScriptRoot 'OutlookFixture.cs')

$directory = Split-Path $ReportPath -Parent
$imapPath = Join-Path $directory 'account-imap.pst'
[IO.File]::WriteAllBytes($imapPath, (New-Object byte[] 4096))

# An Outlook 2007 shaped profile: the Exchange mailbox owns the OST, a POP
# account delivers into that same OST, and the IMAP account is backed by a PST.
$session = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
$exchange = $session.AddExisting((Join-Path $directory 'account-outlook.ost'), 'exchange-store')
$exchange.DisplayName = 'Postfach - Synthetic'
$exchange.ExchangeStoreType = 0
$imap = $session.AddExisting($imapPath, 'imap-store')
$imap.DisplayName = 'Synthetic IMAP'
$local = $session.AddExisting((Join-Path $directory 'account-local.pst'), 'local-store')
$local.DisplayName = 'Personal Folders'
$null = $session.AddAccount('mailbox@synthetic.test', 0, $exchange)
$null = $session.AddAccount('pop@synthetic.test', 2, $exchange)
$null = $session.AddAccount('imap@synthetic.test', 1, $imap)
$orphan = $session.AddAccount('orphan@synthetic.test', 2, $null)
$orphan.SmtpAddress = $null

$creations = $exchange.FolderCreations + $imap.FolderCreations + $local.FolderCreations
$accounts = @(Get-OutlookAccount -Namespace $session)
$stores = @(Get-OutlookStore -Namespace $session)

# The same profile on an Outlook 2007 client: no account exposes DeliveryStore, so
# every read fails and the store each account delivers into has to come from the
# profile default instead. The default store is listed first, as Outlook reports it.
$legacy = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
$legacyExchange = $legacy.AddExisting((Join-Path $directory 'legacy-outlook.ost'), 'legacy-exchange')
$legacyExchange.DisplayName = 'Postfach - Legacy'
$legacyExchange.ExchangeStoreType = 0
$legacyLocal = $legacy.AddExisting((Join-Path $directory 'legacy-local.pst'), 'legacy-local')
$legacyLocal.DisplayName = 'Archivordner'

foreach ($pair in @(@('mailbox@legacy.test', 0), @('pop@legacy.test', 2))) {
  $added = $legacy.AddAccount($pair[0], $pair[1], $null)
  $added.HideDeliveryStore = $true
}

$legacyAccounts = @(Get-OutlookAccount -Namespace $legacy)
$legacyStores = @(Get-OutlookStore -Namespace $legacy)

# A client that cannot report delivery and has no readable default store either:
# nothing may be inferred, and the account must stay unresolved.
$blind = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
$blindStore = $blind.AddExisting((Join-Path $directory 'blind-outlook.ost'), 'blind-exchange')
$blindStore.ExchangeStoreType = 0
$blindStore.StandardErrors[6] = New-Object InvalidOperationException 'Synthetic unreadable Inbox.'
$blindAccount = $blind.AddAccount('pop@blind.test', 2, $null)
$blindAccount.HideDeliveryStore = $true
$blindAccounts = @(Get-OutlookAccount -Namespace $blind)

# Outlook 2007 again, but the profile records each account's delivery store. The POP account was re-pointed to a
# local PST, which the profile default cannot show. Two accounts share a name, so their records cannot be told
# apart, and one record names a store the profile does not hold. Records live below a synthetic HKCU key.
$profilesKey = 'Software\substrate-tests\' + [guid]::NewGuid().ToString('N')
$accountsKey = "HKCU:\$profilesKey\Recorded\9375CFF0413111d3B88A00104B2A6676"
$recordedEnvironment = [Environment]::GetEnvironmentVariable('SUBSTRATE_OUTLOOK_PROFILES_KEY')

function Add-SyntheticRecord {
  param ([string]$Key, [string]$Name, [string]$EntryId)

  $path = Join-Path $accountsKey $Key
  $null = New-Item -Path $path -Force
  $nameBytes = [Text.Encoding]::Unicode.GetBytes($Name + [char]0)
  $entryBytes = [byte[]]($EntryId -split '(..)' | Where-Object { $_ } | ForEach-Object { [Convert]::ToByte($_, 16) })
  $null = New-ItemProperty -Path $path -Name 'Account Name' -PropertyType Binary -Value $nameBytes
  $null = New-ItemProperty -Path $path -Name 'Delivery Store EntryID' -PropertyType Binary -Value $entryBytes
}

try {
  $recorded = New-Object AdNoctem.Substrate.Tests.Fixtures.OutlookFixture
  $recorded.ProfileName = 'Recorded'
  $recordedExchange = $recorded.AddExisting((Join-Path $directory 'recorded-outlook.ost'), 'A0A0A0A0')
  $recordedExchange.DisplayName = 'Postfach - Recorded'
  $recordedExchange.ExchangeStoreType = 0
  $recordedLocal = $recorded.AddExisting((Join-Path $directory 'recorded-local.pst'), 'B1B1B1B1')
  $recordedLocal.DisplayName = 'Personal Folders'

  foreach ($pair in @(@('pop@recorded.test', 2), @('twin@recorded.test', 2), @('twin@recorded.test', 2), @('unknown@recorded.test', 2))) {
    $added = $recorded.AddAccount($pair[0], $pair[1], $null)
    $added.HideDeliveryStore = $true
  }

  Add-SyntheticRecord -Key '00000002' -Name 'pop@recorded.test' -EntryId 'b1b1b1b1'
  Add-SyntheticRecord -Key '00000003' -Name 'twin@recorded.test' -EntryId 'B1B1B1B1'
  Add-SyntheticRecord -Key '00000004' -Name 'twin@recorded.test' -EntryId 'A0A0A0A0'
  Add-SyntheticRecord -Key '00000005' -Name 'unknown@recorded.test' -EntryId 'C2C2C2C2'

  [Environment]::SetEnvironmentVariable('SUBSTRATE_OUTLOOK_PROFILES_KEY', $profilesKey)
  $recordedAccounts = @(Get-OutlookAccount -Namespace $recorded)
  $recordedStores = @(Get-OutlookStore -Namespace $recorded)
}
finally {
  [Environment]::SetEnvironmentVariable('SUBSTRATE_OUTLOOK_PROFILES_KEY', $recordedEnvironment)
  Remove-Item -LiteralPath "HKCU:\$profilesKey" -Recurse -Force -ErrorAction SilentlyContinue
}

try {
  $observations = [ordered]@{
    AccountCount   = $accounts.Count
    AccountFields  = @($accounts[0].PSObject.Properties.Name)
    StoreFields    = @($stores[0].PSObject.Properties.Name)
    TypeNames      = @($accounts | ForEach-Object { $_.AccountTypeName })
    Synchronized   = @($accounts | ForEach-Object { $_.IsServerSynchronized })
    DeferBodies    = @($accounts | ForEach-Object { $_.MayDeferItemBodies })
    Orphan         = @($accounts | Where-Object DisplayName -EQ 'orphan@synthetic.test')[0]
    ExchangeStore  = @($stores | Where-Object StoreID -EQ 'exchange-store')[0]
    ImapStore      = @($stores | Where-Object StoreID -EQ 'imap-store')[0]
    LocalStore     = @($stores | Where-Object StoreID -EQ 'local-store')[0]
    ItemAccesses   = ($exchange.ItemAccesses + $imap.ItemAccesses + $local.ItemAccesses)
    FolderCreation = (($exchange.FolderCreations + $imap.FolderCreations + $local.FolderCreations) -eq $creations)
    Modern         = @($accounts | Where-Object DisplayName -EQ 'pop@synthetic.test')[0]
    LegacyPop      = @($legacyAccounts | Where-Object DisplayName -EQ 'pop@legacy.test')[0]
    LegacyMailbox  = @($legacyAccounts | Where-Object DisplayName -EQ 'mailbox@legacy.test')[0]
    LegacyDefault  = @($legacyStores | Where-Object StoreID -EQ 'legacy-exchange')[0]
    LegacyOther    = @($legacyStores | Where-Object StoreID -EQ 'legacy-local')[0]
    Blind          = $blindAccounts[0]
    RecordedPop    = @($recordedAccounts | Where-Object DisplayName -EQ 'pop@recorded.test')[0]
    RecordedTwins  = @($recordedAccounts | Where-Object DisplayName -EQ 'twin@recorded.test')
    RecordedOrphan = @($recordedAccounts | Where-Object DisplayName -EQ 'unknown@recorded.test')[0]
    RecordedLocal  = @($recordedStores | Where-Object StoreID -EQ 'B1B1B1B1')[0]
    RecordedOst    = @($recordedStores | Where-Object StoreID -EQ 'A0A0A0A0')[0]
  }

  $observations | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
}
finally {
  [IO.File]::Delete($imapPath)
}
