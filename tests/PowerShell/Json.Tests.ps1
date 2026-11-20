#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Formatted JSON' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }

  It 'indents by a fixed width and keeps readable characters in <Engine>' -ForEach @(
    @{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }
  ) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $module = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'
    Test-Path -LiteralPath $module | Should -BeTrue -Because 'run the launcher build before package tests'
    $reports = Join-Path $root 'build/test-results/json'
    $null = [IO.Directory]::CreateDirectory($reports)
    $report = Join-Path $reports "$Engine.json"

    $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Json-Probe.ps1'), '-ModulePath', $module, '-ReportPath', $report)

    $result.ExitCode | Should -Be 0 -Because $result.Output
    $result.Output | Should -Match 'json probe passed'
  }

  It 'writes the same text in both engines' {
    $reports = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'build/test-results/json'

    [IO.File]::ReadAllText((Join-Path $reports 'powershell.exe.json')) | Should -BeExactly ([IO.File]::ReadAllText((Join-Path $reports 'pwsh.json')))
  }
}
