@{
  RootModule            = 'AdNoctem.Substrate.PowerShell.psm1'
  ModuleVersion         = '1.0.0'
  GUID                  = '81c6e99d-6a1d-4fc0-b688-2b36c361df05'
  Author                = 'MVProwess'
  CompanyName           = 'Ad Noctem Collective'
  Copyright             = '(c) MVProwess. All rights reserved.'
  Description           = 'Reusable Windows administration APIs with PowerShell commands for registry, networking, security, packages, Office and system operations.'
  PowerShellVersion     = '5.1'
  CompatiblePSEditions  = @('Desktop', 'Core')
  ProcessorArchitecture = 'Amd64'
  RequiredModules       = @()
  FunctionsToExport     = @()
  CmdletsToExport       = @()
  VariablesToExport     = @()
  AliasesToExport       = @('Get-Network', 'Get-Prefix', 'Get-NetworkCIDR', 'Get-PrefixCIDR')
  PrivateData           = @{
    PSData = @{
      Tags                     = @('PowerShell', 'Windows', 'administration', 'Substrate')
      LicenseUri               = 'https://opensource.org/license/mit'
      ProjectUri               = 'https://github.com/adnoctem/substrate'
      ReleaseNotes             = 'https://github.com/adnoctem/substrate/releases'
      Prerelease               = ''
      RequireLicenseAcceptance = $false
    }
  }
}
