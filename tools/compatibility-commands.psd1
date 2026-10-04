@{
  Diagnostics = @('Show-Color')
  Packages    = @('Install-Win32Program')
  Office      = @('Test-OfficeDeployment', 'Get-OfficeDeploymentPlan')
  Windows     = @('Set-ServiceStartupState')
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
