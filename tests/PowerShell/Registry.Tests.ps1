#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Compiled command contracts and registry compatibility' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }
  It 'preserves parameter contracts and packages help in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $probe = Join-Path $root 'tests/fixtures/v2/Registry-Contract.ps1'
    $reportRoot = Join-Path $root 'build/test-results/registry'
    $null = [IO.Directory]::CreateDirectory($reportRoot)
    foreach ($implementation in @('v1', 'v2')) {
      $module = if ($implementation -eq 'v1') { Join-Path $root 'build/baseline/d2d1498275806684b44169504146302d54b7a084/src/PSFoundation.psd1' } else { Join-Path $root 'build/module/PSFoundation/PSFoundation.psd1' }
      $result = Invoke-PSFHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probe, '-ModulePath', $module, '-ReportPath', (Join-Path $reportRoot "$Engine-contract-$implementation.json"))
      $result.ExitCode | Should -Be 0 -Because $result.Output
    }
    $before = Get-Content (Join-Path $reportRoot "$Engine-contract-v1.json") -Raw | ConvertFrom-Json
    $after = Get-Content (Join-Path $reportRoot "$Engine-contract-v2.json") -Raw | ConvertFrom-Json
    $differences = @()
    for ($index = 0; $index -lt $before.Commands.Count; $index++) {
      if (($before.Commands[$index] | ConvertTo-Json -Depth 35 -Compress) -cne ($after.Commands[$index] | ConvertTo-Json -Depth 35 -Compress)) { $differences += $before.Commands[$index].Name }
    }
    $differences | Should -BeNullOrEmpty -Because "parameter metadata differs: $($differences -join ', ')"
  }

  It 'matches the pinned v1 behavior in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $baseline = Join-Path $root 'build/baseline/d2d1498275806684b44169504146302d54b7a084/src/PSFoundation.psd1'
    $stage = Join-Path $root 'build/module/PSFoundation/PSFoundation.psd1'
    $probe = Join-Path $root 'tests/fixtures/v2/Registry-Probe.ps1'
    $reportRoot = Join-Path $root 'build/test-results/registry'
    $null = [IO.Directory]::CreateDirectory($reportRoot)
    $fixture = [guid]::NewGuid().ToString('N')
    foreach ($implementation in @('v1', 'v2')) {
      $module = if ($implementation -eq 'v1') { $baseline } else { $stage }
      $report = Join-Path $reportRoot "$Engine-$implementation.json"
      $result = Invoke-PSFHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probe, '-ModulePath', $module, '-ReportPath', $report, '-FixtureName', $fixture)
      $result.ExitCode | Should -Be 0 -Because $result.Output
    }
    $before = Get-Content (Join-Path $reportRoot "$Engine-v1.json") -Raw | ConvertFrom-Json
    $after = Get-Content (Join-Path $reportRoot "$Engine-v2.json") -Raw | ConvertFrom-Json
    $differences = @()
    foreach ($case in $before.PSObject.Properties) {
      $expected = $case.Value | ConvertTo-Json -Depth 40 -Compress
      $actual = $after.($case.Name) | ConvertTo-Json -Depth 40 -Compress
      if ($actual -cne $expected) { $differences += $case.Name }
    }
    $differences | Should -BeNullOrEmpty -Because "v1/v2 differences: $($differences -join ', '); reports: $reportRoot"
  }
}
