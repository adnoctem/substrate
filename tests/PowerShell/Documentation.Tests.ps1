#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Packaged documentation' {
  BeforeAll { . (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tools/probe.ps1') }

  It 'loads complete help in <Engine>' -ForEach @(@{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $result = Invoke-SubstrateHostProbe -Engine $Engine -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'tests/fixtures/v2/Documentation-Probe.ps1'), '-ModulePath', (Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'))
    $result.ExitCode | Should -Be 0 -Because $result.Output
  }
}
