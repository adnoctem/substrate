#Requires -Version 5.0

function Test-LGPOInstalled {
  <#
  .SYNOPSIS
    Returns whether the LGPO executable exists at the specified path.
  .DESCRIPTION
    Uses C# for filesystem checks and preserves PowerShell provider and empty-path handling.
  .PARAMETER Path
    Literal executable path. Defaults to the module's ProgramData tools directory.
  .EXAMPLE
    Test-LGPOInstalled
  #>
  [CmdletBinding()]
  [OutputType([bool])]
  param (
    [Parameter(Mandatory = $false)]
    [string]$Path = (Join-Path -Path $env:ProgramData -ChildPath 'PSFoundation\tools\LGPO.exe')
  )
  if ([string]::IsNullOrEmpty($Path)) { return (Test-Path -LiteralPath $Path -PathType Leaf) }
  $provider = $null
  $drive = $null
  try { $filePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref]$provider, [ref]$drive) }
  catch { return (Test-Path -LiteralPath $Path -PathType Leaf) }
  if ($provider.Name -ne 'FileSystem') { return (Test-Path -LiteralPath $Path -PathType Leaf) }
  [PSFoundation.Policies.LgpoTool]::new().IsInstalled([PSFoundation.IO.FileSystemPath]::Parse($filePath))
}

function Invoke-LGPO {
  <#
  .SYNOPSIS
    Applies a policy text file or GPO backup using an existing LGPO executable.
  .DESCRIPTION
    Preserves validation and confirmation in PowerShell. C# executes the process and returns application evidence.
    Policy application is not transactional; cancellation does not interrupt an already started application.
  .PARAMETER PolicyPath
    Existing policy text file or GPO backup directory.
  .PARAMETER LgpoExe
    Trusted executable path. Defaults to the module's ProgramData tools directory.
  .EXAMPLE
    Invoke-LGPO -PolicyPath '.\policy.txt' -WhatIf
  #>
  [CmdletBinding(SupportsShouldProcess)]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ })]
    [string]$PolicyPath,
    [Parameter(Mandatory = $false)]
    [string]$LgpoExe = (Join-Path -Path $env:ProgramData -ChildPath 'PSFoundation\tools\LGPO.exe')
  )
  if (-not (Test-Path -LiteralPath $LgpoExe -PathType Leaf)) {
    throw "LGPO.exe not found at '$LgpoExe'. Run Install-LGPO first."
  }
  $arg = if (Test-Path -LiteralPath $PolicyPath -PathType Container) { '/g' } else { '/t' }
  if (-not $PSCmdlet.ShouldProcess($PolicyPath, "Apply via LGPO.exe ($arg)")) { return }
  $executable = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($LgpoExe)
  $policy = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($PolicyPath)
  try { [PSFoundation.PowerShell.Policies.LgpoCompatibility]::Apply($executable, $policy, $PolicyPath, $PSEdition -eq 'Desktop') }
  catch [ComponentModel.Win32Exception] { $PSCmdlet.ThrowTerminatingError([PSFoundation.PowerShell.Policies.LgpoCompatibility]::LaunchError($_.Exception)) }
}

function Show-Color {
  <#
  .SYNOPSIS
    Displays the available console colors.
  .DESCRIPTION
    Displays each ConsoleColor name in its own color. Standard PowerShell parameters
    such as Verbose, ErrorAction and InformationAction are discoverable through tab completion.
    ArgumentList accepts unused arguments for compatibility with the original function.
  .PARAMETER ArgumentList
    Extra arguments accepted and ignored for compatibility. They do not filter the colors.
  .EXAMPLE
    Show-Color
  .EXAMPLE
    Show-Color -Verbose
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'ArgumentList', Justification = 'Unused extra arguments are intentionally accepted to preserve the original Show-Color API.')]
  [CmdletBinding()]
  param ([Parameter(ValueFromRemainingArguments = $true)][object[]]$ArgumentList)
  foreach ($color in [PSFoundation.PowerShell.Diagnostics.ColorCompatibility]::GetColors()) {
    Write-Host $color -ForegroundColor $color
  }
}

# Keep the networking parameter declarations here deliberately simple: adding CmdletBinding
# would change how these legacy functions accept unused arguments.
function Invoke-PSFNetworkCompatibility {
  [CmdletBinding()]
  param (
    [string]$Operation,
    [PSCustomObject]$Adapter,
    [string]$AddressFamily = 'IPv4',
    [switch]$Required,
    [string]$Type = 'Any'
  )
  # Load Windows' existing CIM display/type definitions, not its networking implementation.
  if ($null -eq $Adapter) { Import-Module NetAdapter -ErrorAction Stop }
  $result = [PSFoundation.PowerShell.Networking.NetworkCompatibility]::Invoke($Operation, $Adapter, $AddressFamily, [bool]$Required, $Type)
  foreach ($message in $result.Messages) { Write-Log -Message $message -Color Red }
  if ($null -ne $result.Error) { $PSCmdlet.ThrowTerminatingError($result.Error) }
  if ($null -ne $result.Failure) { throw $result.Failure }
  $result.Value
}

function Get-DefaultNetworkAdapter {
  <#
  .SYNOPSIS
    Resolves the default IPv4 adapter using the lowest route metric.
  .DESCRIPTION
    Preserves the original PowerShell arguments and CIM result objects; C# performs the queries and selection.
  .PARAMETER Type
    Filters the selected adapter by Any, WiFi, Ethernet or VPN.
  .PARAMETER Required
    Throws instead of logging and returning no adapter.
  .EXAMPLE
    Get-DefaultNetworkAdapter -Type Ethernet -Required
  #>
  param ([ValidateSet('Any', 'WiFi', 'Ethernet', 'VPN')][string]$Type = 'Any', [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-DefaultNetworkAdapter' @PSBoundParameters
}

function Get-IPAddress {
  <#
  .SYNOPSIS
    Returns an address from the selected adapter.
  .DESCRIPTION
    Prefers a non-link-local IPv6 address and preserves legacy argument handling.
  .PARAMETER AddressFamily
    IPv4 or IPv6; defaults to IPv4.
  .PARAMETER Adapter
    An adapter returned by Get-DefaultNetworkAdapter. Omit to resolve the default adapter.
  .PARAMETER Required
    Throws if the address is unavailable.
  .EXAMPLE
    Get-IPAddress -AddressFamily IPv6
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-IPAddress' @PSBoundParameters
}

function Get-SubnetMask {
  <#
  .SYNOPSIS
    Returns the IPv4 subnet mask paired with the adapter's IPv4 address.
  .DESCRIPTION
    Preserves legacy argument handling while C# reads the supplied adapter data.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if the mask is unavailable.
  .EXAMPLE
    Get-SubnetMask
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-SubnetMask' @PSBoundParameters
}

function Get-DefaultGateway {
  <#
  .SYNOPSIS
    Returns the selected adapter's gateway for the requested address family.
  .DESCRIPTION
    Keeps the original arguments and result while C# selects the gateway.
  .PARAMETER AddressFamily
    IPv4 or IPv6; defaults to IPv4.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if the gateway is unavailable.
  .EXAMPLE
    Get-DefaultGateway -AddressFamily IPv6
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-DefaultGateway' @PSBoundParameters
}

function Get-DNSServer {
  <#
  .SYNOPSIS
    Returns the selected adapter's DNS servers for the requested address family.
  .DESCRIPTION
    Keeps the original arguments, server order and pipeline output.
  .PARAMETER AddressFamily
    IPv4 or IPv6; defaults to IPv4.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if no DNS servers are configured for the family.
  .EXAMPLE
    Get-DNSServer
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-DNSServer' @PSBoundParameters
}

function Get-MACAddress {
  <#
  .SYNOPSIS
    Returns the selected adapter's MAC address.
  .DESCRIPTION
    Retains the original string format and argument handling.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if no MAC address is available.
  .EXAMPLE
    Get-MACAddress
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-MACAddress' @PSBoundParameters
}

function Get-NetworkPrefix {
  <#
  .SYNOPSIS
    Returns the selected adapter's IPv4 network address or IPv6 prefix.
  .DESCRIPTION
    C# performs the calculation while this function preserves the original calling conventions.
  .PARAMETER AddressFamily
    IPv4 or IPv6; defaults to IPv4.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if the required address or mask is unavailable.
  .EXAMPLE
    Get-NetworkPrefix -AddressFamily IPv6
  #>
  [Alias('Get-Network', 'Get-Prefix')]
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-NetworkPrefix' @PSBoundParameters
}

function Get-NetworkPrefixCIDR {
  <#
  .SYNOPSIS
    Returns the selected adapter's network prefix with its prefix length.
  .DESCRIPTION
    Retains the original aliases and CIDR string output.
  .PARAMETER AddressFamily
    IPv4 or IPv6; defaults to IPv4.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if the required address or mask is unavailable.
  .EXAMPLE
    Get-NetworkPrefixCIDR
  #>
  [Alias('Get-NetworkCIDR', 'Get-PrefixCIDR')]
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-NetworkPrefixCIDR' @PSBoundParameters
}

function Get-BroadcastAddress {
  <#
  .SYNOPSIS
    Computes the IPv4 broadcast address from the selected adapter's address and mask.
  .DESCRIPTION
    Retains the original arguments and address string output.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if the address or mask is unavailable.
  .EXAMPLE
    Get-BroadcastAddress
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-BroadcastAddress' @PSBoundParameters
}

function Get-MulticastAddress {
  <#
  .SYNOPSIS
    Computes the solicited-node multicast address for the selected IPv6 address.
  .DESCRIPTION
    Retains the original arguments and address string output.
  .PARAMETER Adapter
    A previously resolved adapter; defaults to the default adapter.
  .PARAMETER Required
    Throws if no IPv6 address is available.
  .EXAMPLE
    Get-MulticastAddress
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-MulticastAddress' @PSBoundParameters
}
