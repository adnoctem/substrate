@{
  Registry     = @(
    'ConvertTo-RegistryProviderPath', 'Resolve-RegistryPath',
    'Compare-RegistrySettingState', 'Restore-RegistrySettingState',
    'Get-RegistryKey', 'Set-RegistryKey', 'Remove-RegistryKey',
    'Get-RegistryValue', 'Set-RegistryValue', 'Remove-RegistryValue',
    'Test-RegistryPath', 'Test-RegistryValue', 'Get-RegistryValueKind',
    'Mount-DefaultUserHive', 'Dismount-DefaultUserHive', 'Export-RegistryKey', 'Search-RegistryKey',
    'Export-RegistrySettingState', 'ConvertTo-RegistrySettingResult'
  )
  Core         = @('New-OperationResult', 'Add-OperationResult', 'Write-OperationResultLog', 'Resolve-LongPath')
  Diagnostics  = @('Get-ErrorTranslation', 'Write-Log', 'Invoke-SafeProcess')
  Networking   = @('Test-IPv4Address', 'Test-IPv6Address')
  Policies     = @('ConvertFrom-RegistryPolicy', 'ConvertTo-RegistryPolicy', 'Resolve-LGPOSource', 'Install-LGPO', 'Test-LGPOSourceAvailability')
  Windows      = @(
    'Get-OSBuildNumber', 'Get-OSDisplayVersion', 'Get-OSEdition', 'Get-OSProductName', 'Get-OSVersionInfo',
    'Get-SystemMemory', 'Get-SystemDisk', 'Get-Hostname', 'Get-SystemUptime', 'Get-SystemInfo', 'Get-SystemPaths',
    'Test-HostApplicability', 'Test-Elevation', 'Convert-RobocopyExitCode', 'Test-PendingReboot', 'Get-FileLockProcess', 'Set-ScheduledTaskState', 'Get-DotNetVersion'
  )
  Interop      = @('Remove-ComObject', 'Invoke-ComGarbageCollection', 'Get-OutlookInstallation', 'Get-OutlookRepairToolInfo', 'Find-OutlookRepairTool',
    'Connect-Outlook', 'Get-OutlookStoreRoot', 'Add-OutlookStoreRoot', 'Get-OutlookSubFolder',
    'Get-OutlookStandardFolderIdentity', 'Get-OutlookFolderPlan', 'Open-OutlookPstStore', 'Close-OutlookPstStore')
  Security     = @('Test-WindowsEventLogChannel', 'Export-EventLog', 'Get-DefenderThreatDescriptionURL', 'Add-DefenderExclusion', 'Get-ScheduledTaskAction', 'Get-WMIPersistence')
  Devices      = @('Get-PrintDevice', 'Get-DefaultPrintDevice', 'Set-DefaultPrintDevice', 'Get-ScanDevice')
  Provisioning = @('New-OfflineDomainJoinBlob', 'New-DjoinFile')
  Office       = @('New-OfficeDeploymentConfiguration', 'Get-OfficeInventory', 'Get-OfficeActivationStatus',
    'Resolve-OfficeDeploymentToolSource', 'Test-OfficeDeploymentToolSourceAvailability', 'Test-OfficeDeploymentTool', 'Get-OfficeDeploymentToolHelp', 'Install-OfficeDeploymentTool')
  Packages     = @('Get-Win32Program', 'Find-Win32Program', 'Get-InstalledProgramCount', 'Uninstall-Win32Program')
}
