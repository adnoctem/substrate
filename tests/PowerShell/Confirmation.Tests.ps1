#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

Describe 'Package-set confirmation delegation' {
  BeforeAll {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    . (Join-Path $root 'src/PowerShell/compat.ps1')
    function Find-UPFAppxPackage { throw 'Only mocked discovery is allowed in this test.' }
    function Uninstall-UPFAppxPackage {
      [CmdletBinding(SupportsShouldProcess = $true)]
      param($InputObject, $AllUsers, $IncludeProtected, $Force, $DryRun)
      $null = $AllUsers, $IncludeProtected, $Force # Binding-only arguments in this synthetic provider.
      $status = 'Skipped'
      if (-not $DryRun -and $PSCmdlet.ShouldProcess($InputObject, 'Simulate removal')) { $status = 'Removed' }
      [PSCustomObject]@{ Status = $status; WhatIf = [bool]$WhatIfPreference; DryRun = [bool]$DryRun; Target = $InputObject }
    }
  }
  It 'forwards WhatIf and DryRun to removal without invoking real package operations' {
    Mock Find-UPFAppxPackage { [PSCustomObject]@{ Matched = $true; Package = 'Synthetic.Package' } }
    $preview = Uninstall-UPFAppxPackageSet -Pattern 'Synthetic.*' -WhatIf -PassThru
    $preview.WhatIf | Should -BeTrue
    $preview.Status | Should -Be 'Skipped'
    $preview.Target | Should -Be 'Synthetic.Package'
    $dryRun = Uninstall-UPFAppxPackageSet -Pattern 'Synthetic.*' -DryRun -PassThru
    $dryRun.DryRun | Should -BeTrue
    $dryRun.Status | Should -Be 'Skipped'
  }
}
