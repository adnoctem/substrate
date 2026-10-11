#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Repository tooling boundaries' {
  BeforeAll {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    . (Join-Path $root 'tools/vendor-support.ps1')
    . (Join-Path $root 'tools/probe.ps1')
    . (Join-Path $root 'tools/compatibility.ps1')
  }

  It 'ignores validator spacing while detecting changes to logic, literals and statement boundaries' {
    $modulePath = Join-Path $TestDrive 'SubstrateValidationFixture.psm1'

    function Get-ValidationContract {
      param ([string]$Validator)

      $source = 'function Test-SyntheticValue { [CmdletBinding()] param ([ValidateScript({' + $Validator + '})][string]$Value) $Value }'
      [IO.File]::WriteAllText($modulePath, $source)

      try {
        $contract = Get-SubstrateApiContract -ModulePath $modulePath

        return ($contract.Commands | ConvertTo-Json -Depth 35 -Compress)
      }
      finally { Get-Module | Where-Object Path -EQ $modulePath | Remove-Module -Force }
    }

    $validator = @'
if ($_ -eq 'blocked') {
  throw 'not allowed'
}
$true
'@
    $expected = Get-ValidationContract $validator
    $spaced = "`n`n" + ($validator -replace '\r?\n', "`r`n`r`n").Replace('  throw', '    throw') + "`r`n`r`n"
    Get-ValidationContract $spaced | Should -BeExactly $expected
    Get-ValidationContract $validator.Replace('$true', '$false') | Should -Not -BeExactly $expected
    Get-ValidationContract $validator.Replace('not allowed', 'not  allowed') | Should -Not -BeExactly $expected
    Get-ValidationContract "Write-Output 'value'`n`$true" | Should -Not -BeExactly (Get-ValidationContract "Write-Output 'value' `$true")

    $hereString = "`$text = @'`nfirst`n`nlast`n'@`n`$true"
    Get-ValidationContract $hereString | Should -Not -BeExactly (Get-ValidationContract $hereString.Replace("first`n`nlast", "first`nlast"))
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

  It 'previews allowed 0.x preparation and publication without changing prepared files' {
    $manifest = Join-Path $root 'build/module/AdNoctem.Substrate.PowerShell/AdNoctem.Substrate.PowerShell.psd1'
    $before = (Get-FileHash -LiteralPath $manifest).Hash
    $record = Join-Path $root 'build/release.json'
    $recordBefore = if (Test-Path -LiteralPath $record) { (Get-FileHash -LiteralPath $record).Hash } else { $null }

    foreach ($version in @('0.1.0', '0.1.1', '0.2.0')) {
      foreach ($mode in @('-Prepare', '-Publish')) {
        $result = Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $root 'tools/release.ps1'), $mode, '-Version', $version, '-DryRun')
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $result.Output | Should -Match ('DRY RUN: .* substrate ' + [regex]::Escape($version))
      }
    }

    (Get-FileHash -LiteralPath $manifest).Hash | Should -Be $before
    $recordAfter = if (Test-Path -LiteralPath $record) { (Get-FileHash -LiteralPath $record).Hash } else { $null }
    $recordAfter | Should -Be $recordBefore
  }

  It 'rejects versions outside the pre-1.0 release policy before preparation or publication' {
    foreach ($version in @('0.0.1', '1.0.0', '2.0.0', '0.1.0-beta.1', '00.1.0')) {
      foreach ($mode in @('-Prepare', '-Publish')) {
        $result = Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $root 'tools/release.ps1'), $mode, '-Version', $version, '-DryRun')
        $result.ExitCode | Should -Not -Be 0 -Because $version
        $result.Output | Should -Not -Match 'DRY RUN'
        $result.Output | Should -Match 'Substrate releases must be|Cannot validate argument on parameter .Version.'
      }
    }
  }

  It 'validates relocated release artifacts and rejects altered, added, missing or mismatched artifacts' {
    $fixture = Join-Path $TestDrive 'relocated-release'
    $null = New-Item -ItemType Directory -Path "$fixture/tools", "$fixture/build/module/AdNoctem.Substrate.PowerShell", "$fixture/dist/nuget" -Force
    $script = Join-Path $fixture 'tools/release.ps1'
    Copy-Item -LiteralPath (Join-Path $root 'tools/release.ps1') -Destination $script
    $paths = @('build/module/AdNoctem.Substrate.PowerShell/module.txt', 'dist/nuget/library.nupkg')

    foreach ($path in $paths) { Set-Content -LiteralPath (Join-Path $fixture $path) -Value 'synthetic artifact' }

    $record = @{ Schema = 2; Version = '0.1.0'; Files = @($paths | ForEach-Object {
          @{ Path = $_; Sha256 = (Get-FileHash -LiteralPath (Join-Path $fixture $_)).Hash }
        })
    }
    $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$fixture/build/release.json"
    $arguments = @('-NoProfile', '-File', $script, '-ValidateOnly', '-Version', '0.1.0')
    $result = Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList $arguments
    $result.ExitCode | Should -Be 0 -Because $result.Output
    $result.Output | Should -Match 'Validated prepared'
    $result = Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList @('-NoProfile', '-File', $script, '-ValidateOnly', '-Version', '0.1.1')
    $result.ExitCode | Should -Not -Be 0
    $result.Output | Should -Match 'version does not match'
    $package = Join-Path $fixture $paths[1]
    Set-Content -LiteralPath $package -Value 'changed artifact'
    $result = Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList $arguments
    $result.ExitCode | Should -Not -Be 0
    $result.Output | Should -Match 'Prepared package changed'
    Set-Content -LiteralPath $package -Value 'synthetic artifact'
    $extra = Join-Path $fixture 'dist/extra.txt'
    Set-Content -LiteralPath $extra -Value 'extra artifact'
    $result = Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList $arguments
    $result.ExitCode | Should -Not -Be 0
    $result.Output | Should -Match 'file set changed'
    Remove-Item -LiteralPath $extra, $package
    $result = Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList $arguments
    $result.ExitCode | Should -Not -Be 0
    $result.Output | Should -Match 'file set changed'
  }
}
