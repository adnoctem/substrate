#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Repository tooling boundaries' {
  BeforeAll {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    . (Join-Path $root 'tools/vendor-support.ps1')
    . (Join-Path $root 'tools/probe.ps1')
  }
  It 'rejects unsafe vendor origins and archive paths before download or extraction' {
    foreach ($uri in @('http://download.microsoft.com/tool.exe', 'https://example.invalid/tool.exe', 'https://user@download.microsoft.com/tool.exe', 'https://download.microsoft.com:444/tool.exe')) {
      { Assert-MicrosoftDownloadUri ([uri]$uri) } | Should -Throw
    }
    foreach ($path in @('../LGPO.exe', '/LGPO.exe', 'C:/LGPO.exe', 'safe/../../LGPO.exe', 'safe\LGPO.exe')) {
      { Assert-VendorArchivePath $path } | Should -Throw
    }
    { Assert-MicrosoftDownloadUri ([uri]'https://download.microsoft.com/tools/LGPO.zip') } | Should -Not -Throw
    { Assert-VendorArchivePath 'LGPO_30/LGPO.exe' } | Should -Not -Throw
  }
  It 'previews release preparation without generating a release record or changing the staged manifest' {
    $manifest = Join-Path $root 'build/module/PSFoundation/PSFoundation.psd1'
    $before = (Get-FileHash -LiteralPath $manifest).Hash
    $result = Invoke-PSFHostProbe -Engine pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $root 'tools/release.ps1'), '-Prepare', '-Version', '2.0.0', '-DryRun')
    $result.ExitCode | Should -Be 0 -Because $result.Output
    $result.Output | Should -Match 'DRY RUN'
    (Get-FileHash -LiteralPath $manifest).Hash | Should -Be $before
  }
  It 'rejects accidental v1 release preparation' {
    $result = Invoke-PSFHostProbe -Engine pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $root 'tools/release.ps1'), '-Prepare', '-Version', '1.9.0', '-DryRun')
    $result.ExitCode | Should -Not -Be 0
    $result.Output | Should -Match 'v1 is frozen'
  }
}
