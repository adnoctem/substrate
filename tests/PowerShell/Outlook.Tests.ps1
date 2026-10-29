#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'C# Outlook workflow compatibility' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }

  It 'isolates optional provider errors and preserves diagnostic streams in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $reports = Join-Path $root 'build/test-results/outlook'
    $null = [IO.Directory]::CreateDirectory($reports)
    $report = Join-Path $reports "$Engine-optional.json"
    $module = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'
    $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Outlook-Optional-Probe.ps1'), '-ModulePath', $module, '-ReportPath', $report)
    $result.ExitCode | Should -Be 0 -Because $result.Output
    $observed = Get-Content $report -Raw | ConvertFrom-Json
    $observed.IdentityCount | Should -Be 20
    ($observed.IdentityFields -join ',') | Should -BeExactly 'Kind,StoreID,EntryID,State,Evidence'
    ($observed.PlanFields -join ',') | Should -BeExactly 'EntryID,StoreID,FolderPath,RelativePath,StandardKind,Process,Traverse,Reason'
    $observed.Suggested.State | Should -BeExactly 'Unresolved'
    $observed.Suggested.Evidence | Should -Match '0x80004004 \(E_ABORT\)'
    $observed.IdentityWarnings.Count | Should -Be 1
    $observed.IdentityWarnings[0] | Should -Match 'SuggestedContacts.*optional-failure.*0x80004004'
    ($observed.PlanWarnings -join "`n") | Should -Match 'Skipped branch.*Renamed contacts.*IncompleteIdentity:SuggestedContacts'
    $observed.Unsafe.Process | Should -BeFalse
    $observed.Unsafe.Traverse | Should -BeFalse
    $observed.Unsafe.Reason | Should -BeExactly 'IncompleteIdentity:SuggestedContacts'
    $observed.RequiredFailure | Should -Match 'Inbox identity.*optional-failure.*Absent'

    foreach ($name in @('InboxProcessed', 'KeepProcessed', 'DescendantSkipped', 'IncludedDescendant', 'NoFolderCreation', 'ExplicitArchive', 'ExplicitRoot', 'StoppedDiagnostic', 'Cleanup')) {
      $observed.$name | Should -BeTrue -Because $name
    }

    $observed.ItemAccesses | Should -Be 0
    $observed.PreviewCount | Should -Be 0
    $observed.PreviewAttachments | Should -Be 0
  }

  It 'preserves folder planning and PST lifetimes in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $reports = Join-Path $root 'build/test-results/outlook'
    $null = [IO.Directory]::CreateDirectory($reports)

    foreach ($implementation in @('v1', 'v2')) {
      $module = if ($implementation -eq 'v1') { Join-Path $root 'build/baseline/d2d1498275806684b44169504146302d54b7a084/src/PSFoundation.psd1' } else { Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1' }

      $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Outlook-Probe.ps1'), '-ModulePath', $module, '-ReportPath', (Join-Path $reports "$Engine-$implementation.json"))
      $result.ExitCode | Should -Be 0 -Because $result.Output
    }

    $before = Get-Content (Join-Path $reports "$Engine-v1.json") -Raw | ConvertFrom-Json
    $after = Get-Content (Join-Path $reports "$Engine-v2.json") -Raw | ConvertFrom-Json
    $differences = @()

    foreach ($case in $before.PSObject.Properties) {
      if (($case.Value | ConvertTo-Json -Depth 15 -Compress) -cne ($after.($case.Name) | ConvertTo-Json -Depth 15 -Compress)) { $differences += $case.Name }
    }

    $differences | Should -BeNullOrEmpty -Because "v1/v2 Outlook differences: $($differences -join ', '); reports: $reports"
  }
}

Describe 'C# Outlook profile inventory' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }

  It 'classifies accounts and delivery stores without reaching an item in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $reports = Join-Path $root 'build/test-results/outlook'
    $null = [IO.Directory]::CreateDirectory($reports)
    $report = Join-Path $reports "$Engine-account.json"
    $module = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'
    $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Outlook-Account-Probe.ps1'), '-ModulePath', $module, '-ReportPath', $report)
    $result.ExitCode | Should -Be 0 -Because $result.Output
    $observed = Get-Content $report -Raw | ConvertFrom-Json

    $observed.AccountCount | Should -Be 4
    ($observed.AccountFields -join ',') | Should -BeExactly 'DisplayName,SmtpAddress,UserName,AccountType,AccountTypeName,DeliveryStoreID,DeliveryStoreName,DeliveryFilePath,DeliveryStoreSource,IsDeliveryStoreInferred,IsServerSynchronized,MayDeferItemBodies,Evidence'
    ($observed.StoreFields -join ',') | Should -BeExactly 'DisplayName,StoreID,FilePath,DataFileFormat,ExchangeStoreType,ExchangeStoreName,IsExchangeBacked,IsSynchronized,MayContainHeaderOnlyItems,SizeBytes,DeliveredFrom,Evidence'
    ($observed.TypeNames -join ',') | Should -BeExactly 'Exchange,POP3,IMAP,POP3'
    ($observed.DeferBodies -join ',') | Should -BeExactly 'False,False,True,False'

    # An extension cannot classify an Outlook 2007 IMAP store: it is a PST and
    # reports olNotExchange, yet its items can still be header-only stubs.
    $observed.ImapStore.DataFileFormat | Should -BeExactly 'PST'
    $observed.ImapStore.ExchangeStoreType | Should -Be 3
    $observed.ImapStore.IsExchangeBacked | Should -BeFalse
    $observed.ImapStore.IsSynchronized | Should -BeTrue
    $observed.ImapStore.MayContainHeaderOnlyItems | Should -BeTrue
    $observed.ImapStore.Evidence | Should -Match "IMAP account 'imap@synthetic.test'"
    $observed.ImapStore.SizeBytes | Should -Be 4096

    $observed.LocalStore.IsSynchronized | Should -BeFalse
    $observed.LocalStore.MayContainHeaderOnlyItems | Should -BeFalse
    @($observed.LocalStore.DeliveredFrom).Count | Should -Be 0

    # Delivery is correlated by StoreID, so a POP account landing in the
    # Exchange mailbox is visible before anyone removes that account.
    ($observed.ExchangeStore.DeliveredFrom -join ',') | Should -BeExactly 'mailbox@synthetic.test,pop@synthetic.test'
    $observed.ExchangeStore.ExchangeStoreName | Should -BeExactly 'PrimaryMailbox'
    $observed.ExchangeStore.MayContainHeaderOnlyItems | Should -BeTrue

    $observed.Orphan.DeliveryStoreID | Should -BeExactly ''
    $observed.Orphan.Evidence | Should -Match 'no delivery store'
    $observed.Orphan.DeliveryStoreSource | Should -BeExactly 'Unresolved'

    # A store read from the account is reported as read, never as inferred.
    $observed.Modern.DeliveryStoreSource | Should -BeExactly 'Account'
    $observed.Modern.IsDeliveryStoreInferred | Should -BeFalse

    $observed.ItemAccesses | Should -Be 0
    $observed.FolderCreation | Should -BeTrue
  }

  It 'resolves delivery from the profile default where the account cannot report it in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $reports = Join-Path $root 'build/test-results/outlook'
    $null = [IO.Directory]::CreateDirectory($reports)
    $report = Join-Path $reports "$Engine-delivery.json"
    $module = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'
    $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Outlook-Account-Probe.ps1'), '-ModulePath', $module, '-ReportPath', $report)
    $result.ExitCode | Should -Be 0 -Because $result.Output
    $observed = Get-Content $report -Raw | ConvertFrom-Json

    # Account.DeliveryStore arrived in Outlook 2010. Below it the read fails, and the
    # profile default is the store new mail goes to unless an account overrides it.
    $observed.LegacyPop.DeliveryStoreID | Should -BeExactly 'legacy-exchange'
    $observed.LegacyPop.DeliveryStoreName | Should -BeExactly 'Postfach - Legacy'
    $observed.LegacyPop.DeliveryFilePath | Should -Match 'legacy-outlook\.ost$'
    $observed.LegacyPop.DeliveryStoreSource | Should -BeExactly 'ProfileDefault'
    $observed.LegacyPop.IsDeliveryStoreInferred | Should -BeTrue
    $observed.LegacyPop.Evidence | Should -Match 'DeliveryStore is unavailable'
    $observed.LegacyPop.Evidence | Should -Match 'cannot report a per-account override'
    $observed.LegacyMailbox.DeliveryStoreSource | Should -BeExactly 'ProfileDefault'

    # This is the finding the survey exists for: a POP account delivering into the
    # Exchange mailbox, now visible on the clients that could never report it.
    ($observed.LegacyDefault.DeliveredFrom -join ',') | Should -BeExactly 'mailbox@legacy.test,pop@legacy.test'
    $observed.LegacyDefault.IsExchangeBacked | Should -BeTrue
    $observed.LegacyDefault.Evidence | Should -Match 'taken from the profile default'

    # Only the default store collects the inference; the other store stays empty
    # rather than claiming accounts that were never shown to deliver into it.
    @($observed.LegacyOther.DeliveredFrom).Count | Should -Be 0
    $observed.LegacyOther.Evidence | Should -Not -Match 'profile default'

    # No readable default either: nothing is inferred and nothing is reported.
    $observed.Blind.DeliveryStoreID | Should -BeExactly ''
    $observed.Blind.DeliveryStoreName | Should -BeExactly ''
    $observed.Blind.DeliveryStoreSource | Should -BeExactly 'Unresolved'
    $observed.Blind.IsDeliveryStoreInferred | Should -BeFalse
    $observed.Blind.Evidence | Should -Match 'profile default delivery store is unavailable'
  }
}
