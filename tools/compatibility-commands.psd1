@{
  Diagnostics = @('Show-Color')
  Packages    = @('Install-Win32Program', 'Install-UPFAppxPackage', 'Update-UPFAppxPackage', 'Install-UPFAppxPackageSet', 'Uninstall-UPFAppxPackageSet')
  Office      = @('Test-OfficeDeployment', 'Get-OfficeDeploymentPlan', 'Test-OfficeDeploymentMedia', 'Save-OfficeDeploymentMedia', 'Get-OfficeDeploymentRecovery',
    'Install-Office', 'Uninstall-Office', 'Switch-OfficeDeployment', 'Update-Office', 'Set-OfficeUpdateConfiguration', 'Add-OfficeLanguage', 'Remove-OfficeLanguage',
    'Set-OfficeApplicationSelection', 'Set-OfficeApplicationPreference', 'Resume-OfficeInstallation', 'Resume-OfficeMigration')
  Windows     = @('Set-ServiceStartupState', 'New-DriveMapping', 'Remove-DriveMapping')
  Data        = @('Convert-Quote', 'Merge-ObjectArrays', 'Get-DefaultApp')
  Credentials = @('New-EncryptedCredentialFile', 'Request-AdministratorPrivilege')
  Security    = @('Import-SecurityEventConfiguration', 'Get-SecurityEventGroup', 'Get-SecurityEventDefinition', 'Resolve-WindowsEventMappedField', 'ConvertFrom-WinEvent',
    'Get-WindowsEventByDefinition', 'Get-WindowsLogonEvent', 'Get-WindowsAccountChangeEvent', 'Get-WindowsServiceEvent', 'Get-WindowsBootEvent',
    'Get-WindowsPowerShellEvent', 'Get-WindowsScheduledTaskEvent', 'Get-WindowsSysmonEvent', 'Get-DefenderThreat', 'Get-DefenderThreatDetection', 'Find-NewlyWrittenObject')
  Identity    = @('Get-UserInfo', 'Get-UserSID')
  Policies    = @('Invoke-LGPO', 'Test-LGPOInstalled')
  Networking  = @(
    'Get-DefaultNetworkAdapter', 'Get-IPAddress', 'Get-SubnetMask', 'Get-DefaultGateway', 'Get-DNSServer',
    'Get-MACAddress', 'Get-NetworkPrefix', 'Get-NetworkPrefixCIDR', 'Get-BroadcastAddress', 'Get-MulticastAddress'
  )
}
