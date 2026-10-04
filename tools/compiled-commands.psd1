@{
  Registry    = @(
    'ConvertTo-RegistryProviderPath', 'Resolve-RegistryPath',
    'Compare-RegistrySettingState', 'Restore-RegistrySettingState',
    'Get-RegistryKey', 'Set-RegistryKey', 'Remove-RegistryKey',
    'Get-RegistryValue', 'Set-RegistryValue', 'Remove-RegistryValue',
    'Test-RegistryPath', 'Test-RegistryValue', 'Get-RegistryValueKind',
    'Mount-DefaultUserHive', 'Dismount-DefaultUserHive', 'Export-RegistryKey', 'Search-RegistryKey',
    'Export-RegistrySettingState', 'ConvertTo-RegistrySettingResult'
  )
  Core        = @('New-OperationResult', 'Add-OperationResult', 'Write-OperationResultLog', 'Resolve-LongPath')
  Diagnostics = @('Get-ErrorTranslation', 'Write-Log', 'Invoke-SafeProcess')
  Networking  = @('Test-IPv4Address', 'Test-IPv6Address')
  Policies    = @('ConvertFrom-RegistryPolicy', 'ConvertTo-RegistryPolicy', 'Resolve-LGPOSource', 'Install-LGPO', 'Test-LGPOSourceAvailability')
  Windows     = @(
    'Get-OSBuildNumber', 'Get-OSDisplayVersion', 'Get-OSEdition', 'Get-OSProductName', 'Get-OSVersionInfo',
    'Get-SystemMemory', 'Get-SystemDisk', 'Get-Hostname', 'Get-SystemUptime', 'Get-SystemInfo', 'Get-SystemPaths',
    'Test-HostApplicability', 'Test-Elevation', 'Convert-RobocopyExitCode'
  )
  Interop     = @('Remove-ComObject', 'Invoke-ComGarbageCollection')
}
