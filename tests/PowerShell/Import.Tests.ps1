#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Incremental binary module imports' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }
  It 'imports and exercises compiled commands in a fresh <Engine> process' -ForEach @(
    @{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }
  ) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $module = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'
    Test-Path -LiteralPath $module | Should -BeTrue -Because 'run the launcher build before package tests'
    $probe = Join-Path $root 'tests/fixtures/v2/Import-Probe.ps1'
    $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probe, '-ModulePath', $module)
    $result.ExitCode | Should -Be 0 -Because $result.Output
    $result.Output | Should -Match 'smoke tests passed'
  }
}
