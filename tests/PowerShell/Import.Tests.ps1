#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Incremental binary module imports' {
  It 'imports and exercises compiled commands in a fresh <Engine> process' -ForEach @(
    @{ Engine = 'powershell.exe' }, @{ Engine = 'pwsh' }
  ) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $module = Join-Path $root 'build/module/PSFoundation/PSFoundation.psd1'
    Test-Path -LiteralPath $module | Should -BeTrue -Because 'run the launcher build before package tests'
    $probe = Join-Path $root 'tests/fixtures/v2/Import-Probe.ps1'
    $output = & $Engine -NoProfile -ExecutionPolicy Bypass -File $probe -ModulePath $module 2>&1
    $LASTEXITCODE | Should -Be 0 -Because ($output -join [Environment]::NewLine)
    $output -join [Environment]::NewLine | Should -Match 'smoke tests passed'
  }
}
