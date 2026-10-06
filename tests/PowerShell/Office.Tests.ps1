#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'C# Office assessment compatibility' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }

  It 'preserves target and evidence reports in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $reports = Join-Path $root 'build/test-results/office'
    $null = [IO.Directory]::CreateDirectory($reports)

    foreach ($implementation in @('v1', 'v2')) {
      $module = if ($implementation -eq 'v1') { Join-Path $root 'build/baseline/d2d1498275806684b44169504146302d54b7a084/src/PSFoundation.psd1' } else { Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1' }

      $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Office-Probe.ps1'), '-ModulePath', $module, '-ReportPath', (Join-Path $reports "$Engine-$implementation.json"))
      $result.ExitCode | Should -Be 0 -Because $result.Output
    }

    $before = Get-Content (Join-Path $reports "$Engine-v1.json") -Raw | ConvertFrom-Json
    $after = Get-Content (Join-Path $reports "$Engine-v2.json") -Raw | ConvertFrom-Json
    $differences = @()

    foreach ($case in $before.PSObject.Properties) {
      if (($case.Value | ConvertTo-Json -Depth 30 -Compress) -cne ($after.($case.Name) | ConvertTo-Json -Depth 30 -Compress)) { $differences += $case.Name }
    }

    $differences | Should -BeNullOrEmpty -Because "v1/v2 Office differences: $($differences -join ', '); reports: $reports"
  }
}
