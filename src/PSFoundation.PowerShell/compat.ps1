#Requires -Version 5.0

function Install-Win32Program {
  <#
  .SYNOPSIS
    Runs a caller-selected local installer through the C# package API.
  .DESCRIPTION
    Retains PowerShell path validation, confirmation and result fields. Preparation and execution are provided by C#.
  .PARAMETER Path
    Existing executable or MSI installer.
  .PARAMETER ArgumentList
    Literal installer arguments.
  .PARAMETER NoWait
    Starts the installer without claiming completion.
  .PARAMETER PassThru
    Uses the legacy ExitCode status text.
  .PARAMETER DryRun
    Previews execution through WhatIf.
  .PARAMETER SuccessExitCodes
    Exit codes accepted as successful.
  .PARAMETER RebootExitCodes
    Successful exit codes requiring a restart.
  .PARAMETER RunId
    Optional caller correlation identifier.
  .EXAMPLE
    Install-Win32Program -Path '.\setup.exe' -ArgumentList '/quiet' -WhatIf
  #>
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$Path,
    [string[]]$ArgumentList,
    [switch]$NoWait,
    [switch]$PassThru,
    [switch]$DryRun,
    [int[]]$SuccessExitCodes = @(0, 1641, 3010),
    [int[]]$RebootExitCodes = @(1641, 3010),
    [string]$RunId
  )
  if ($DryRun) { $WhatIfPreference = $true }
  $target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
  if (-not $PSCmdlet.ShouldProcess($target, 'Install Win32 program')) {
    return [PSFoundation.PowerShell.Packages.ProgramCompatibility]::SkipInstall($target, $RunId, $PSBoundParameters.ContainsKey('RunId'))
  }
  [PSFoundation.PowerShell.Packages.ProgramCompatibility]::Install($target, $ArgumentList, $NoWait, $PassThru, $SuccessExitCodes, $RebootExitCodes, $RunId, $PSBoundParameters.ContainsKey('RunId'))
}

function Set-ServiceStartupState {
  <#
  .SYNOPSIS
    Sets service startup types with confirmation and protection rules.
  .DESCRIPTION
    Uses native C# service inventory and mutation. Filters and protected names are checked against each resolved service.
  .PARAMETER Name
    Service names or wildcard patterns.
  .PARAMETER StartupType
    Automatic, Manual or Disabled.
  .PARAMETER Filter
    Exact service names to exclude.
  .EXAMPLE
    Set-ServiceStartupState -Name 'Fax' -StartupType Disabled -WhatIf
  #>
  [CmdletBinding(SupportsShouldProcess = $true)]
  param (
    [Parameter(Mandatory = $true)][string[]]$Name,
    [Parameter(Mandatory = $true)][ValidateSet('Automatic', 'Manual', 'Disabled')][string]$StartupType,
    [string[]]$Filter
  )
  foreach ($selection in [PSFoundation.PowerShell.Windows.ServiceCompatibility]::Select($Name, $StartupType, $Filter, $script:ProtectedServiceNames)) {
    if ($selection.SkippedResult) { $selection.SkippedResult; continue }
    if (-not $PSCmdlet.ShouldProcess($selection.Name, "Set startup type to $StartupType")) {
      [PSFoundation.PowerShell.Windows.ServiceCompatibility]::Result($selection.Name, 'Skipped', 'WhatIf')
      continue
    }
    [PSFoundation.PowerShell.Windows.ServiceCompatibility]::Apply($selection.Name, $StartupType)
  }
}

function Get-UserInfo {
  <#
  .SYNOPSIS
    Retrieves the effective Windows identity, administrator status and SID.
  .DESCRIPTION
    Uses the C# identity manager and preserves the original hashtable and extra-argument handling.
  .EXAMPLE
    Get-UserInfo
  #>
  [OutputType([hashtable])]
  param ()
  [PSFoundation.PowerShell.Windows.IdentityCompatibility]::GetUserInfo()
}

function Get-UserSID {
  <#
  .SYNOPSIS
    Resolves a Windows account name to its security identifier.
  .DESCRIPTION
    Uses native account translation through the C# identity manager.
  .PARAMETER UserName
    Local or domain account name to resolve.
  .EXAMPLE
    Get-UserSID -UserName 'DOMAIN\user'
  #>
  [OutputType([string])]
  param ([Parameter(Mandatory = $true)][string]$UserName)
  try { [PSFoundation.PowerShell.Windows.IdentityCompatibility]::GetSecurityIdentifier($UserName) }
  catch {
    Write-Error "Could not find SID for user '$UserName'. $_"
    return $null
  }
}

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
