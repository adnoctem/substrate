@{
  Diagnostics = @('Show-Color')
  Policies    = @('Invoke-LGPO', 'Test-LGPOInstalled')
  Networking  = @(
    'Get-DefaultNetworkAdapter', 'Get-IPAddress', 'Get-SubnetMask', 'Get-DefaultGateway', 'Get-DNSServer',
    'Get-MACAddress', 'Get-NetworkPrefix', 'Get-NetworkPrefixCIDR', 'Get-BroadcastAddress', 'Get-MulticastAddress'
  )
}
