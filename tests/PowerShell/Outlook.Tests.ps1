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
