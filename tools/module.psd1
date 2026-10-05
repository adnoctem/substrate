@{
  RootModule            = 'PSFoundation.psm1'
  ModuleVersion         = '2.0.0'
  GUID                  = '73aa2e6a-815c-4bcc-b8f8-053e1c07ae7b'
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
      Tags                     = @('PowerShell', 'Windows', 'administration', 'PSFoundation')
      LicenseUri               = 'https://opensource.org/license/mit'
      ProjectUri               = 'https://github.com/adnoctem/PSFoundation'
      ReleaseNotes             = 'https://github.com/adnoctem/PSFoundation/releases'
      Prerelease               = ''
      RequireLicenseAcceptance = $false
    }
  }
}
