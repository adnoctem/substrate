#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Completed C# package workflows' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }

  It 'runs native reads and synthetic workflows in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Completion-Probe.ps1'), '-ModulePath', (Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'))
    $result.ExitCode | Should -Be 0 -Because $result.Output
    $result.Output | Should -Match 'Completion workflows passed'
  }
}
