#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Compiled foundation compatibility' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }

  It 'matches pinned v1 results, paths, logging, errors and process execution in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $reports = Join-Path $root 'build/test-results/foundations'
    $null = [IO.Directory]::CreateDirectory($reports)

    foreach ($implementation in @('v1', 'v2')) {
      $module = if ($implementation -eq 'v1') { Join-Path $root 'build/baseline/d2d1498275806684b44169504146302d54b7a084/src/PSFoundation.psd1' } else { Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1' }

      $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Foundation-Probe.ps1'), '-ModulePath', $module, '-ReportPath', (Join-Path $reports "$Engine-$implementation.json"))
      $result.ExitCode | Should -Be 0 -Because $result.Output
    }

    $before = Get-Content (Join-Path $reports "$Engine-v1.json") -Raw | ConvertFrom-Json
    $after = Get-Content (Join-Path $reports "$Engine-v2.json") -Raw | ConvertFrom-Json
    $differences = @()

    foreach ($case in $before.PSObject.Properties) {
      if (($case.Value | ConvertTo-Json -Depth 30 -Compress) -cne ($after.($case.Name) | ConvertTo-Json -Depth 30 -Compress)) { $differences += $case.Name }
    }

    $differences | Should -BeNullOrEmpty -Because "v1/v2 foundation differences: $($differences -join ', '); reports: $reports"

    foreach ($collection in @('ArrayList', 'List', 'Array')) {
      foreach ($count in @(0, 1, 3)) {
        $case = $after."operation-log-collection-${collection}-${count}"
        $case.Terminated | Should -BeFalse -Because "$collection with $count results must bind and log"
        $case.Records.Count | Should -Be 1
        $observed = $case.Records[0].Value
        $observed.Count | Should -Be $count
        $observed.EmptyPath | Should -Be ($count -eq 0)
        $observed.DirectoryCreated | Should -Be ($count -gt 0)
        $observed.Unchanged | Should -BeTrue

        for ($index = 0; $index -lt $count; $index++) {
          $entry = $observed.Entries[$index]
          $entry.Target | Should -BeExactly "fixture-$($index + 1)"
          $entry.Source | Should -BeExactly 'MigrationTest'
          $entry.Action | Should -BeExactly 'Read'
          $entry.Status | Should -BeExactly 'Completed'
          $entry.Detail | Should -BeExactly 'Synthetic'
          $entry.Timestamp | Should -BeExactly 'fixed'
          $entry.Script | Should -BeExactly 'MigrationSmoke'
          $entry.RunId | Should -BeExactly 'synthetic-run'
          $entry.Nested.Value | Should -Be ($index + 1)
        }
      }
    }
  }
}
