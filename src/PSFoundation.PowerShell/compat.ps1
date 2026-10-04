#Requires -Version 5.0

function Get-OfficeDeploymentRecovery {
  <#
  .SYNOPSIS
    Reads a protected Office recovery record without resuming it.
  .DESCRIPTION
    Uses the native journal reader to validate schema, identity, scope and fingerprints. A checkpoint does not authorize replay.
  .PARAMETER RunId
    Run identifier returned by an Office operation.
  .PARAMETER LogRoot
    Protected local recovery directory.
  .EXAMPLE
    Get-OfficeDeploymentRecovery -RunId $result.RunId
  #>
  [CmdletBinding()]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-fA-F0-9]{32}$')][string]$RunId,
    [string]$LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office')
  )
  $outcome = [PSFoundation.PowerShell.Office.OfficeCompatibility]::ReadRecovery($RunId, $LogRoot)
  if ($outcome.Failure) { Stop-PSFOfficeNativeFailure $outcome.Failure }
  [PSCustomObject]@{ RunId = $RunId; LogRoot = $LogRoot; Path = Join-Path $LogRoot ($RunId + '.json'); Record = ConvertFrom-Json -InputObject $outcome.Json }
}

function Get-TransportMessageId {
  [CmdletBinding()]
  [OutputType([string])]
  param ([Parameter(Mandatory = $true)][AllowEmptyString()][string]$HeaderText)
  [PSFoundation.Interop.MailHeaderParser]::GetTransportMessageId($HeaderText)
}

function Write-PSFOfficeJson {
  [CmdletBinding()]
  param ([string]$Path, [object]$Value)
  $json = ConvertTo-Json -InputObject $Value -Depth 30
  $failure = [PSFoundation.PowerShell.Office.OfficeCompatibility]::WriteJournal($Path, $json)
  if ($failure) { Stop-PSFOfficeNativeFailure $failure }
}

function Test-PSFOfficeHost {
  [CmdletBinding()]
  [OutputType([bool])]
  param ()
  [PSFoundation.PowerShell.Office.OfficeCompatibility]::SupportedHost()
}

function Assert-PSFOfficeHost {
  [CmdletBinding()]
  param ()
  $failure = [PSFoundation.PowerShell.Office.OfficeCompatibility]::CheckHost()
  if ($failure) { Stop-PSFOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Enter-PSFOfficeLock {
  [CmdletBinding()]
  param ()
  try { [PSFoundation.Office.OfficeDeploymentLock]::Acquire() }
  catch {
    $errorValue = $_.Exception
    while ($errorValue.InnerException -and -not $errorValue.Data.Contains('OfficeReason')) { $errorValue = $errorValue.InnerException }
    if ($errorValue.Data.Contains('OfficeReason')) { Stop-PSFOfficeOperation $errorValue.Data['OfficeReason'] $errorValue.Message }
    throw
  }
}

function Invoke-PSFOfficeConfiguration {
  [CmdletBinding()]
  param ([string]$OdtPath, [xml]$Document, [string]$Directory, [ValidateSet('/download', '/configure', '/customize')][string]$Mode = '/configure', [Security.SecureString]$ProductKey)
  $outcome = [PSFoundation.PowerShell.Office.OfficeCompatibility]::InvokeConfiguration($OdtPath, $Document, $Directory, $Mode, $ProductKey)
  if ($outcome.Failure) { Stop-PSFOfficeNativeFailure $outcome.Failure }
  $outcome.Result
}

function Stop-PSFOfficeNativeFailure {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Raises an operation failure without changing machine state.')]
  [CmdletBinding()]
  param ([object]$Failure)
  try { Stop-PSFOfficeOperation $Failure.ReasonCode $Failure.Detail $Failure.Diagnostic }
  catch {
    if ($Failure.CleanupError) { $_.Exception.Data['OfficeCleanupError'] = $Failure.CleanupError }
    if ($null -ne $Failure.ExitCode) { $_.Exception.Data['OfficeExitCode'] = $Failure.ExitCode }
    throw
  }
}

function ConvertFrom-PSFOfficeMediaAssessment {
  [CmdletBinding()]
  param ([object]$Assessment)
  $manifest = $null
  $fingerprint = $null
  if ($Assessment.Valid) {
    $manifest = ConvertFrom-Json -InputObject $Assessment.ManifestJson -ErrorAction Stop
    $fingerprint = Get-PSFOfficeFingerprint $manifest
  }
  [PSCustomObject]@{
    Valid = $Assessment.Valid; Path = $Assessment.Path; Manifest = $manifest; Fingerprint = $fingerprint
    ReasonCode = $Assessment.ReasonCode; Error = $Assessment.Error; Diagnostic = $Assessment.Diagnostic
  }
}

function Save-OfficeDeploymentMedia {
  <#
  .SYNOPSIS
    Prepares a verified Office package and publishes its manifest last.
  .DESCRIPTION
    Downloads only during explicit preparation. C# verifies and publishes the package without replacing incomplete or incompatible media.
  .PARAMETER Configuration
    Requested product, ordered languages, channel, architecture and optional build.
  .PARAMETER SourcePath
    New dedicated package directory whose parent already exists.
  .PARAMETER OdtPath
    Existing Microsoft-signed Office Deployment Tool setup.exe.
  .PARAMETER DryRun
    Validate and preview without writes or downloads.
  .EXAMPLE
    Save-OfficeDeploymentMedia -Configuration $target -SourcePath C:\Media\Office -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Media names the deployment payload as a collective noun.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param ([Parameter(Mandatory = $true)][object]$Configuration, [Parameter(Mandatory = $true)][string]$SourcePath, [Parameter(Mandatory = $true)][string]$OdtPath, [switch]$DryRun)
  $target = ConvertTo-PSFOfficeConfiguration $Configuration
  Assert-PSFOfficePath $SourcePath
  if (Test-Path -LiteralPath $SourcePath) {
    $existing = Test-OfficeDeploymentMedia -SourcePath $SourcePath -Configuration $target
    if (-not $existing.Valid) { Stop-PSFOfficeOperation InvalidMedia 'Existing package is incomplete or incompatible; use a new destination.' (Get-PSFOfficeMediaDiagnostic $existing) }
    return $existing
  }
  if (-not (Test-OfficeDeploymentTool $OdtPath).Valid) { Stop-PSFOfficeOperation UntrustedTool 'ODT verification failed.' }
  if ($DryRun -or -not $PSCmdlet.ShouldProcess($SourcePath, 'Download pinned Office media and publish verified package')) {
    return [PSCustomObject]@{ Status = 'Preview'; Path = $SourcePath; Configuration = $target }
  }
  $outcome = [PSFoundation.PowerShell.Office.OfficeCompatibility]::PrepareMedia($target, $SourcePath, $OdtPath)
  if ($outcome.Failure) { Stop-PSFOfficeNativeFailure $outcome.Failure }
  ConvertFrom-PSFOfficeMediaAssessment $outcome.Result
}

function Test-OfficeDeploymentMedia {
  <#
  .SYNOPSIS
    Validates an Office package, manifest, payloads, paths, and write protection.
  .DESCRIPTION
    C# validates schema-2 manifests and protected payloads. The compatibility boundary retains the original JSON object and fingerprint.
    Validation performs no writes, downloads or native Office execution.
  .PARAMETER SourcePath
    Absolute package directory containing psfoundation-office-media.json.
  .PARAMETER Configuration
    Optional target whose requested languages must be available in the package.
  .EXAMPLE
    Test-OfficeDeploymentMedia -SourcePath C:\Media\Office -Configuration $target
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Media names the deployment payload as a collective noun.')]
  [CmdletBinding()]
  [OutputType([PSCustomObject])]
  param ([Parameter(Mandatory = $true)][string]$SourcePath, [object]$Configuration)
  $stage = 'ProtectedPath'
  try {
    Assert-PSFOfficeProtectedPath $SourcePath -ObjectKind MediaDirectory
    $stage = 'Validation'
    $target = $null
    if ($Configuration) { $target = ConvertTo-PSFOfficeConfiguration $Configuration }
    $assessment = [PSFoundation.PowerShell.Office.OfficeCompatibility]::InspectMedia($SourcePath, $target)
    ConvertFrom-PSFOfficeMediaAssessment $assessment
  }
  catch {
    $errorValue = $_.Exception
    while ($errorValue.InnerException -and -not $errorValue.Data.Contains('OfficeReason')) { $errorValue = $errorValue.InnerException }
    $reason = $errorValue.Data['OfficeReason']
    $message = $errorValue.Message
    $diagnostic = $errorValue.Data['OfficeDiagnostic']
    if (-not $reason) { $reason = 'InvalidMedia'; $message = "Office media validation failed at $stage for '$SourcePath'." }
    if ($reason -eq 'InvalidContract') { $message = "Office media contract is invalid at '$SourcePath'." }
    if (-not $diagnostic) {
      $diagnostic = [PSCustomObject]@{
        Stage = $stage; ObjectKind = 'MediaPackage'; Path = $SourcePath; ExceptionType = $errorValue.GetType().FullName
        Function = 'Test-OfficeDeploymentMedia'; ScriptPath = $PSCommandPath; Line = $_.InvocationInfo.ScriptLineNumber
      }
    }
    [PSCustomObject]@{ Valid = $false; Path = $SourcePath; Manifest = $null; Fingerprint = $null; ReasonCode = $reason; Error = $message; Diagnostic = $diagnostic }
  }
}

function Get-PSFOfficeMediaFile {
  [CmdletBinding()]
  param ([string]$Root)
  $outcome = [PSFoundation.PowerShell.Office.OfficeCompatibility]::MediaFiles($Root)
  if ($outcome.Failure) { Stop-PSFOfficeOperation $outcome.Failure.ReasonCode $outcome.Failure.Detail $outcome.Failure.Diagnostic }
  $outcome.Files
}

function Invoke-PSFOfficeTool {
  [CmdletBinding()]
  param ([string]$OdtPath, [ValidateSet('/download', '/configure', '/customize', '/help')][string]$Mode, [string]$ConfigurationPath)
  $outcome = [PSFoundation.PowerShell.Office.OfficeCompatibility]::InvokeTool($OdtPath, $Mode, $ConfigurationPath)
  if ($outcome.Failure) { Stop-PSFOfficeOperation $outcome.Failure.ReasonCode $outcome.Failure.Detail $outcome.Failure.Diagnostic }
  $outcome.Result
}

function Remove-PSFOfficeWorkDirectory {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private cleanup of the operation-owned directory after approval.')]
  [CmdletBinding()]
  param ([string]$Path, [string]$Parent)
  if (-not $Path) { return }
  $failure = [PSFoundation.PowerShell.Office.OfficeCompatibility]::RemoveDeploymentDirectory($Path, $Parent)
  if ($failure) { Stop-PSFOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Assert-PSFOfficePath {
  [CmdletBinding()]
  param ([string]$Path)
  $failure = [PSFoundation.PowerShell.Office.OfficeCompatibility]::CheckDeploymentPath($Path, $false, 'DeploymentFile')
  if ($failure) { Stop-PSFOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Assert-PSFOfficeProtectedPath {
  [CmdletBinding()]
  param ([string]$Path, [string]$ObjectKind = 'DeploymentFile')
  $failure = [PSFoundation.PowerShell.Office.OfficeCompatibility]::CheckDeploymentPath($Path, $true, $ObjectKind)
  if ($failure) { Stop-PSFOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function New-PSFOfficeProtectedDirectory {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private primitive called only after public ShouldProcess approval.')]
  [CmdletBinding()]
  param ([string]$Path)
  $failure = [PSFoundation.PowerShell.Office.OfficeCompatibility]::CreateDeploymentDirectory($Path)
  if ($failure) { Stop-PSFOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Get-OfficeDeploymentPlan {
  <#
  .SYNOPSIS
    Builds a read-only Office operation plan with explicit authority.
  .DESCRIPTION
    C# evaluates the requested scope and observations. PowerShell retains schema validation, media discovery and the existing fingerprint format.
  .PARAMETER Action
    Install, Remove, Migrate, Update or a narrowly scoped maintenance operation.
  .PARAMETER Configuration
    Ordered target configuration. Not required for removal.
  .PARAMETER SourcePath
    Prepared media package, checked without downloading.
  .PARAMETER RemoveProductId
    Exact Click-to-Run products selected for removal.
  .PARAMETER RemoveMsi
    Explicit broad MSI removal consent for migration.
  .PARAMETER Language
    Exact resources selected by a language operation.
  .PARAMETER Settings
    Update settings or application preferences.
  .PARAMETER Inventory
    Optional offline observation, never trusted by execution.
  .EXAMPLE
    Get-OfficeDeploymentPlan -Action Install -Configuration $target -SourcePath C:\Media\Office
  #>
  [CmdletBinding()]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)]
    [ValidateSet('Install', 'Remove', 'Migrate', 'Update', 'SetUpdateConfiguration', 'AddLanguage', 'RemoveLanguage', 'SetApplicationSelection', 'SetApplicationPreference')]
    [string]$Action,
    [object]$Configuration,
    [string]$SourcePath,
    [string[]]$RemoveProductId = @(),
    [switch]$RemoveMsi,
    [string[]]$Language = @(),
    [object]$Settings = @{},
    [object]$Inventory
  )
  if ($null -eq $RemoveProductId) { $RemoveProductId = @() }
  if ($null -eq $Language) { $Language = @() }
  if ($Action -notin @('Remove', 'Migrate') -and ($RemoveProductId.Count -or $RemoveMsi)) {
    Stop-PSFOfficeOperation InvalidAuthority 'This action cannot remove products.'
  }
  if ($Action -notin @('AddLanguage', 'RemoveLanguage') -and $Language.Count) {
    Stop-PSFOfficeOperation InvalidAuthority 'Language selection belongs only to language operations.'
  }
  Assert-PSFOfficeSetting $Action $Settings
  $selection = @(ConvertTo-PSFOfficeList $RemoveProductId)
  $languages = @(ConvertTo-PSFOfficeList $Language -Language)
  $target = $null
  if ($Configuration) { $target = ConvertTo-PSFOfficeConfiguration $Configuration }
  [PSFoundation.PowerShell.Office.OfficeCompatibility]::ValidateRequest($Action, $target, $SourcePath, [string[]]$selection, [bool]$RemoveMsi, [string[]]$languages, $Settings)
  if (-not $Inventory) { $Inventory = Get-OfficeInventory }
  $media = $null
  if ($target -and $SourcePath) {
    $media = Test-OfficeDeploymentMedia -SourcePath $SourcePath -Configuration $target
    if ($media.Valid -and -not $target.Version) { $target.Version = $media.Manifest.Version }
  }
  [PSFoundation.PowerShell.Office.OfficeCompatibility]::CreatePlan($Action, $target, $SourcePath, [string[]]$selection, [bool]$RemoveMsi,
    [string[]]$languages, $Settings, $Inventory, $media, (Get-PSFOfficeFingerprint $Inventory))
}

function New-PSFOfficeXml {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Creates an in-memory XML document.')]
  [CmdletBinding()]
  param (
    [ValidateSet('Download', 'Install', 'Remove', 'Migrate', 'Update', 'AddLanguage', 'RemoveLanguage', 'SetApplicationSelection', 'SetUpdateConfiguration', 'SetApplicationPreference')]
    [string]$Action,
    [object]$Configuration,
    [string]$MediaPath,
    [string[]]$RemoveProductId = @(),
    [bool]$RemoveMsi = $false,
    [string[]]$Language = @(),
    [object]$Settings = @{}
  )
  if ($null -eq $RemoveProductId) { $RemoveProductId = @() }
  if ($null -eq $Language) { $Language = @() }
  if (($Action -notin @('Remove', 'Migrate') -and ($RemoveProductId.Count -or $RemoveMsi)) -or ($Action -eq 'Remove' -and $RemoveMsi)) {
    Stop-PSFOfficeOperation InvalidAuthority 'XML operation cannot contain the requested removal.'
  }
  Assert-PSFOfficeSetting $Action $Settings
  $target = $null
  if ($Configuration) { $target = ConvertTo-PSFOfficeConfiguration $Configuration }
  return , [PSFoundation.PowerShell.Office.OfficeCompatibility]::CreateXml($Action, $target, $MediaPath, $RemoveProductId, $RemoveMsi, $Language, $Settings)
}

function Test-OfficeDeployment {
  <#
  .SYNOPSIS
    Compares observed Office state with an explicit target.
  .DESCRIPTION
    Retains configuration validation and report objects while C# assesses discrepancies and missing evidence.
  .PARAMETER Configuration
    Configuration from New-OfficeDeploymentConfiguration.
  .PARAMETER Inventory
    Optional observation for offline assessment. Execution always rediscovers state.
  .EXAMPLE
    Test-OfficeDeployment -Configuration $configuration
  #>
  [CmdletBinding()]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][object]$Configuration,
    [object]$Inventory
  )
  $target = ConvertTo-PSFOfficeConfiguration $Configuration
  if (-not $Inventory) { $Inventory = Get-OfficeInventory }
  [PSFoundation.PowerShell.Office.OfficeCompatibility]::Assess($target, $Inventory)
}

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

function Import-SecurityEventConfiguration {
  <#
  .SYNOPSIS
    Reads and caches literal security event configuration data.
  .DESCRIPTION
    Keeps the module-local cache while C# parses the data without executing expressions.
  .PARAMETER Path
    Path to the security event data file.
  .PARAMETER Force
    Replaces the cached configuration.
  .EXAMPLE
    Import-SecurityEventConfiguration -Force
  #>
  [CmdletBinding()]
  param ([string]$Path = (Join-Path $PSScriptRoot 'security.psd1'), [switch]$Force)
  $cached = try { $script:SecurityEventConfiguration } catch { $null }
  if ($cached -and -not $Force) { return $cached }
  $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
  $script:SecurityEventConfiguration = [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::Read($resolved)
  $script:SecurityEventConfiguration
}

function Get-SecurityEventGroup {
  <#
  .SYNOPSIS
    Gets a named security event group.
  .DESCRIPTION
    Resolves the group from the supplied or cached configuration through C#.
  .PARAMETER Name
    Group name.
  .PARAMETER Configuration
    Event configuration hashtable.
  .EXAMPLE
    Get-SecurityEventGroup -Name Logon
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true)][string]$Name, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::Group($Name, $Configuration)
}

function Get-SecurityEventDefinition {
  <#
  .SYNOPSIS
    Finds event definitions using the C# event catalog.
  .DESCRIPTION
    Preserves original definition hashtables and optional filters.
  .PARAMETER Group
    Semantic group.
  .PARAMETER LogName
    Event channel.
  .PARAMETER ProviderName
    Event provider.
  .PARAMETER Id
    Event identifiers.
  .PARAMETER Name
    Logical event name.
  .PARAMETER Configuration
    Event configuration hashtable.
  .EXAMPLE
    Get-SecurityEventDefinition -Group Logon -Id 4624
  #>
  [CmdletBinding()]
  param ([string]$Group, [string]$LogName, [string]$ProviderName, [int[]]$Id, [string]$Name, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::Definitions($Configuration, $Group, $LogName, $ProviderName, $Id, $Name)
}

function Resolve-WindowsEventMappedField {
  <#
  .SYNOPSIS
    Resolves an event field's semantic mapping.
  .DESCRIPTION
    Retains integer and string key handling from the original command.
  .PARAMETER MapName
    Field map name.
  .PARAMETER Value
    Field value.
  .PARAMETER Configuration
    Event configuration hashtable.
  .EXAMPLE
    Resolve-WindowsEventMappedField -MapName LogonType -Value 10
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true)][string]$MapName, [Parameter(Mandatory = $true)][object]$Value, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::MappedField($MapName, $Value, $Configuration)
}

function ConvertFrom-WinEvent {
  <#
  .SYNOPSIS
    Converts an event record to the established enriched result shape.
  .DESCRIPTION
    Uses the C# bounded XML parser and retains the supplied record on the result.
  .PARAMETER Event
    An event record with a ToXml method.
  .PARAMETER Configuration
    Event configuration hashtable.
  .EXAMPLE
    Get-WinEvent -LogName System -MaxEvents 1 | ConvertFrom-WinEvent
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true, ValueFromPipeline = $true)][object]$Event, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  process { [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::ConvertEvent($Event, $Configuration) }
}

function Get-WindowsEventByDefinition {
  <#
    .SYNOPSIS
      Generic event query helper wrapping Get-WinEvent -FilterHashtable.
    .DESCRIPTION
      Queries Windows event logs using filter-hashtable-based queries for
      performance. Accepts event definitions, a group name, or ID lists.
      Definitions are grouped by LogName to minimise individual queries.
      Supports remote computers and optional channel skipping.
    .PARAMETER Definition
      Array of event definition hashtables.
    .PARAMETER Group
      Semantic group name resolved from the configuration.
    .PARAMETER Id
      Event IDs to query (bypasses definition lookup).
    .PARAMETER LogName
      Event log channel(s) to query.
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER SkipMissingChannel
      Skip channels that do not exist rather than throwing.
    .PARAMETER MaxEvents
      Maximum events to return per log/channel query.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsEventByDefinition -Group 'Logon' -StartTime (Get-Date).AddHours(-4)
    .EXAMPLE
      PS> Get-WindowsEventByDefinition -Id 4624, 4625 -LogName 'Security' -MaxEvents 100
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
    [hashtable[]] $Definition,

    [Parameter(Mandatory = $false)]
    [string] $Group,

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [string[]] $LogName,

    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [switch] $SkipMissingChannel,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  begin { $definitions = [Collections.Generic.List[hashtable]]::new() }
  process { if ($Definition) { $definitions.AddRange($Definition) } }
  end {
    $queries = [PSFoundation.PowerShell.Security.EventQueryCompatibility]::Prepare($definitions.ToArray(), $Group, $Id, $LogName, $StartTime, $EndTime, $MaxEvents, $Configuration)
    [PSFoundation.PowerShell.Security.EventQueryCompatibility]::Read($queries, $ComputerName, $SkipMissingChannel, { param($message) Write-Error $message })
  }
}

function Get-WindowsLogonEvent {
  <#
    .SYNOPSIS
      Queries and normalises logon-related security events.
    .DESCRIPTION
      Wraps Get-WindowsEventByDefinition for the Logon group. Supports
      filtering by event ID and logon type. Suppresses noisy system accounts
      by default unless -IncludeSystem is supplied.
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER Id
      Specific event IDs to return. Defaults to all Logon group IDs.
    .PARAMETER LogonType
      Filter by numeric logon type(s), e.g. 10 for RDP.
    .PARAMETER IncludeSystem
      Include system and machine accounts normally suppressed.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER MaxEvents
      Maximum events to return.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsLogonEvent -Id 4625
      Returns failed logon events from the past 24 hours.
    .EXAMPLE
      PS> Get-WindowsLogonEvent -Id 4624 -LogonType 10
      Returns successful RDP logons.
    .EXAMPLE
      PS> Get-WindowsLogonEvent -Id 4624, 4800, 4801 -LogonType 2, 7
      Returns local console and unlock activity.
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [int[]] $LogonType,

    [Parameter(Mandatory = $false)]
    [switch] $IncludeSystem,

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  [PSFoundation.PowerShell.Security.EventQueryCompatibility]::ReadGroup('Logon', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ LogonType = $LogonType; IncludeSystem = [bool]$IncludeSystem }, { param($message) Write-Error $message })
}

function Get-WindowsAccountChangeEvent {
  <#
    .SYNOPSIS
      Queries account lifecycle and group membership change events.
    .DESCRIPTION
      Wraps Get-WindowsEventByDefinition for the AccountChange group.
      Supports filtering by target user, subject user, and event ID.
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER Id
      Specific event IDs to return. Defaults to all AccountChange group IDs.
    .PARAMETER TargetUserName
      Filter by the target account name of the change.
    .PARAMETER SubjectUserName
      Filter by the account that performed the change.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER MaxEvents
      Maximum events to return.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsAccountChangeEvent -Id 4720
      Returns user account creation events.
    .EXAMPLE
      PS> Get-WindowsAccountChangeEvent -Id 4728, 4732 -StartTime (Get-Date).AddDays(-7)
      Returns group membership additions for the past 7 days.
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [string] $TargetUserName,

    [Parameter(Mandatory = $false)]
    [string] $SubjectUserName,

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  [PSFoundation.PowerShell.Security.EventQueryCompatibility]::ReadGroup('AccountChange', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ TargetUserName = $TargetUserName; SubjectUserName = $SubjectUserName }, { param($message) Write-Error $message })
}

function Get-WindowsServiceEvent {
  <#
    .SYNOPSIS
      Queries service lifecycle, failure, and installation events.
    .DESCRIPTION
      Wraps Get-WindowsEventByDefinition for the Service group. Supports
      filtering by service name and event ID.
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER Id
      Specific event IDs to return. Defaults to all Service group IDs.
    .PARAMETER ServiceName
      Filter by the service name involved.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER MaxEvents
      Maximum events to return.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsServiceEvent -Id 7045
      Returns new service installation events (common persistence vector).
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [string] $ServiceName,

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  [PSFoundation.PowerShell.Security.EventQueryCompatibility]::ReadGroup('Service', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ ServiceName = $ServiceName }, { param($message) Write-Error $message })
}

function Get-WindowsBootEvent {
  <#
    .SYNOPSIS
      Queries boot, shutdown, and crash events.
    .DESCRIPTION
      Wraps Get-WindowsEventByDefinition for the BootShutdown group.
      Covers unexpected reboots, BSODs, clean shutdowns, and service
      lifecycle transitions.
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER Id
      Specific event IDs to return. Defaults to all BootShutdown group IDs.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER MaxEvents
      Maximum events to return.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsBootEvent -Id 41, 1001
      Returns unexpected shutdowns and BSOD events.
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  [PSFoundation.PowerShell.Security.EventQueryCompatibility]::ReadGroup('BootShutdown', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{}, { param($message) Write-Error $message })
}

function Get-WindowsPowerShellEvent {
  <#
    .SYNOPSIS
      Queries PowerShell operational telemetry events.
    .DESCRIPTION
      Wraps Get-WindowsEventByDefinition for the PowerShell group. Supports
      filtering by event ID, executing user, and script block content pattern.
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER Id
      Specific event IDs to return. Defaults to all PowerShell group IDs.
    .PARAMETER UserName
      Filter by the user account that executed the PowerShell code.
    .PARAMETER ScriptBlockText
      Regex pattern to search within captured script block content.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER MaxEvents
      Maximum events to return.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsPowerShellEvent -Id 4104
      Returns script block logging events (highest-value PowerShell event).
    .EXAMPLE
      PS> Get-WindowsPowerShellEvent -ScriptBlockText 'DownloadString|FromBase64'
      Returns PowerShell events matching suspicious download or encoding patterns.
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [string] $UserName,

    [Parameter(Mandatory = $false)]
    [string] $ScriptBlockText,

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  [PSFoundation.PowerShell.Security.EventQueryCompatibility]::ReadGroup('PowerShell', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ UserName = $UserName; ScriptBlockText = $ScriptBlockText }, { param($message) Write-Error $message })
}

function Get-WindowsScheduledTaskEvent {
  <#
    .SYNOPSIS
      Queries scheduled task lifecycle telemetry.
    .DESCRIPTION
      Wraps Get-WindowsEventByDefinition for the ScheduledTask group.
      Supports filtering by event ID and task name.
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER Id
      Specific event IDs to return. Defaults to all ScheduledTask group IDs.
    .PARAMETER TaskName
      Filter by the name of the scheduled task.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER MaxEvents
      Maximum events to return.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsScheduledTaskEvent -Id 106, 140, 141
      Returns task registration, update, and deletion events.
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [string] $TaskName,

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  [PSFoundation.PowerShell.Security.EventQueryCompatibility]::ReadGroup('ScheduledTask', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ TaskName = $TaskName }, { param($message) Write-Error $message })
}

function Get-WindowsSysmonEvent {
  <#
    .SYNOPSIS
      Queries Sysmon telemetry if the channel is available.
    .DESCRIPTION
      Wraps Get-WindowsEventByDefinition for the Sysmon group with
      -SkipMissingChannel enabled by default (Sysmon is optional).
    .PARAMETER StartTime
      Earliest event timestamp. Defaults to 24 hours ago.
    .PARAMETER EndTime
      Latest event timestamp. Defaults to now.
    .PARAMETER Id
      Specific event IDs to return. Defaults to all Sysmon group IDs.
    .PARAMETER ComputerName
      Target remote computer(s).
    .PARAMETER MaxEvents
      Maximum events to return.
    .PARAMETER Configuration
      Configuration hashtable. Defaults to cached.
    .EXAMPLE
      PS> Get-WindowsSysmonEvent -Id 1
      Returns Sysmon process creation events.
    .EXAMPLE
      PS> Get-WindowsSysmonEvent -Id 3, 22
      Returns Sysmon network connection and DNS query events.
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [datetime] $StartTime = (Get-Date).AddDays(-1),

    [Parameter(Mandatory = $false)]
    [datetime] $EndTime = (Get-Date),

    [Parameter(Mandatory = $false)]
    [int[]] $Id,

    [Parameter(Mandatory = $false)]
    [string[]] $ComputerName,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxEvents,

    [Parameter(Mandatory = $false)]
    [hashtable] $Configuration = (Import-SecurityEventConfiguration)
  )
  [PSFoundation.PowerShell.Security.EventQueryCompatibility]::ReadGroup('Sysmon', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{}, { param($message) Write-Error $message })
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

function Get-DefenderThreatDetection {
  <#
    .SYNOPSIS
      Retrieves Microsoft Defender threat detections, optionally filtered by date.
    .DESCRIPTION
      Wraps Get-MpThreatDetection.  -Date sets a cutoff -- only detections with an
      InitialDetectionTime on or after that point are returned.  -OutputPath and
      -OutputFormat control whether results are printed to the terminal or written
      to a file (TXT or JSON).
    .PARAMETER Date
      Cutoff date for detections.  Accepts any value that Get-Date can parse
      (string, DateTime, etc.).  Defaults to right now.
    .PARAMETER OutputPath
      File path to write results to.  When omitted, results are printed to the
      terminal.
    .PARAMETER OutputFormat
      Output format: TXT (Formatted-List) or JSON.  Defaults to TXT.
    .PARAMETER IncludeURLs
      Augment each detection with a ThreatDescriptionURL property pointing to the
      official Microsoft threat encyclopedia entry.
    .EXAMPLE
      Get-DefenderThreatDetection
      Prints all threat detections to the terminal.
    .EXAMPLE
      Get-DefenderThreatDetection -Date '2026-04-01' -OutputPath '.\detections.json' -OutputFormat JSON
      Writes detections since April 1st 2026 as JSON.
    .EXAMPLE
      Get-DefenderThreatDetection -IncludeURLs -OutputPath '.\detections.json' -OutputFormat JSON
      Writes detections as JSON, each augmented with a ThreatDescriptionURL.
  #>
  [CmdletBinding()]
  param (
    [Parameter(Mandatory = $false)]
    [object]
    $Date = (Get-Date),

    [Parameter(Mandatory = $false)]
    [string]
    $OutputPath,

    [Parameter(Mandatory = $false)]
    [ValidateSet('TXT', 'JSON')]
    [string]
    $OutputFormat = 'TXT',

    [Parameter(Mandatory = $false)]
    [switch]
    $IncludeURLs
  )

  $cutoffDate = if ($Date -is [datetime]) { $Date } else { Get-Date $Date }
  Write-Log -Message "Filtering Defender threat detections since $($cutoffDate.ToString('yyyy-MM-dd HH:mm:ss'))" -Color Yellow

  $detections = @([PSFoundation.PowerShell.Security.InspectionCompatibility]::Detections($cutoffDate, $IncludeURLs.IsPresent))

  if ($IncludeURLs -and $detections.Count -gt 0) {
    Write-Log -Message '  -> ThreatDescriptionURL(s) appended' -Color Gray
  }
  Write-Log -Message "  -> $($detections.Count) detection(s) found" -Color Gray

  if ($PSBoundParameters.ContainsKey('OutputPath') -and -not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $_outPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    switch ($OutputFormat) {
      'JSON' {
        $detections | ConvertTo-Json -Depth 3 | Out-File -FilePath $_outPath -Encoding utf8
      }
      'TXT' {
        $detections | Format-List * | Out-String -Width 4096 | Out-File -FilePath $_outPath -Encoding utf8
      }
    }
    Write-Log -Message "  -> Written to: $_outPath" -Color Green
  }
  else {
    $detections | Format-List *
  }
}

function Get-DefenderThreat {
  <#
    .SYNOPSIS
      Retrieves the full Microsoft Defender threat catalog.
    .DESCRIPTION
      Wraps Get-MpThreat.  -OutputPath and -OutputFormat control whether results
      are printed to the terminal or written to a file (TXT or JSON).
    .PARAMETER OutputPath
      File path to write results to.  When omitted, results are printed to the
      terminal.
    .PARAMETER OutputFormat
      Output format: TXT (Formatted-List) or JSON.  Defaults to TXT.
    .PARAMETER IncludeURLs
      Augment each threat with a ThreatDescriptionURL property pointing to the
      official Microsoft threat encyclopedia entry.
    .EXAMPLE
      Get-DefenderThreat
      Prints the threat catalog to the terminal.
    .EXAMPLE
      Get-DefenderThreat -OutputPath '.\threats.json' -OutputFormat JSON
      Writes the threat catalog as JSON.
    .EXAMPLE
      Get-DefenderThreat -IncludeURLs -OutputFormat JSON
      Prints the threat catalog as JSON, each augmented with a ThreatDescriptionURL.
  #>
  [CmdletBinding()]
  param (
    [Parameter(Mandatory = $false)]
    [string]
    $OutputPath,

    [Parameter(Mandatory = $false)]
    [ValidateSet('TXT', 'JSON')]
    [string]
    $OutputFormat = 'TXT',

    [Parameter(Mandatory = $false)]
    [switch]
    $IncludeURLs
  )

  Write-Log -Message 'Retrieving Microsoft Defender threat catalog' -Color Yellow

  $threats = @([PSFoundation.PowerShell.Security.InspectionCompatibility]::Threats($IncludeURLs.IsPresent))

  if ($IncludeURLs -and $threats.Count -gt 0) {
    Write-Log -Message '  -> ThreatDescriptionURL(s) appended' -Color Gray
  }
  Write-Log -Message "  -> $($threats.Count) threat(s) found" -Color Gray

  if ($PSBoundParameters.ContainsKey('OutputPath') -and -not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $_outPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    switch ($OutputFormat) {
      'JSON' {
        $threats | ConvertTo-Json -Depth 3 | Out-File -FilePath $_outPath -Encoding utf8
      }
      'TXT' {
        $threats | Format-List * | Out-String -Width 4096 | Out-File -FilePath $_outPath -Encoding utf8
      }
    }
    Write-Log -Message "  -> Written to: $_outPath" -Color Green
  }
  else {
    $threats | Format-List *
  }
}

function Find-NewlyWrittenObject {
  <#
    .SYNOPSIS
      Finds files written near a point in time (e.g. around a Defender detection).
    .DESCRIPTION
      Recursively scans C:\ (or a custom path) for files whose LastWriteTime falls
      within a configurable window around the supplied -Date.  Designed to help
      identify artifacts dropped by malware at the time of a Defender alert.
      Results can be printed to the terminal or exported as TXT / JSON.
    .PARAMETER Date
      Anchor date/time.  Accepts any value that Get-Date can parse (string,
      DateTime, etc.).  Defaults to right now.
    .PARAMETER Before
      Number of hours before the anchor date to include.  Defaults to 2.
    .PARAMETER After
      Number of hours after the anchor date to include.  Defaults to 1.
    .PARAMETER Path
      Root path to search.  Defaults to the system drive (C:\).
    .PARAMETER OutputPath
      File path to write results to.  When omitted, results are printed to the
      terminal.
    .PARAMETER OutputFormat
      Output format: TXT (Formatted custom table) or JSON.  Defaults to TXT.
    .EXAMPLE
      Find-NewlyWrittenObject -Date '2026-04-30 10:15'
      Searches for files written between 08:15 and 11:15 on 2026-04-30.
    .EXAMPLE
      Find-NewlyWrittenObject -Date '2026-04-30' -Before 4 -After 2 -OutputPath '.\artifacts.json' -OutputFormat JSON
      Wider window, exported as JSON.
  #>
  [CmdletBinding()]
  param (
    [Parameter(Mandatory = $false)]
    [object]
    $Date = (Get-Date),

    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 168)]
    [int]
    $Before = 2,

    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 168)]
    [int]
    $After = 1,

    [Parameter(Mandatory = $false)]
    [string]
    $Path = "$env:SystemDrive\",

    [Parameter(Mandatory = $false)]
    [string]
    $OutputPath,

    [Parameter(Mandatory = $false)]
    [ValidateSet('TXT', 'JSON')]
    [string]
    $OutputFormat = 'TXT'
  )

  $anchorDate = if ($Date -is [datetime]) { $Date } else { Get-Date $Date }
  $windowStart = $anchorDate.AddHours(-$Before)
  $windowEnd = $anchorDate.AddHours($After)

  Write-Log -Message "Searching for files written between $($windowStart.ToString('yyyy-MM-dd HH:mm:ss')) and $($windowEnd.ToString('yyyy-MM-dd HH:mm:ss'))" -Color Yellow
  Write-Log -Message "  Root path: $Path" -Color Gray

  $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
  $items = @([PSFoundation.PowerShell.Security.InspectionCompatibility]::Files($resolvedPath, $windowStart, $windowEnd, ($PSVersionTable.PSEdition -eq 'Desktop'), [Action[string]] { param($message) Write-Verbose $message }))

  Write-Log -Message "  -> $($items.Count) file(s) found" -Color Gray

  if ($items.Count -eq 0) { return }

  if ($PSBoundParameters.ContainsKey('OutputPath') -and -not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $_outPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    switch ($OutputFormat) {
      'JSON' {
        $items | ConvertTo-Json -Depth 2 | Out-File -FilePath $_outPath -Encoding utf8
      }
      'TXT' {
        $items | Format-Table -AutoSize | Out-String -Width 4096 | Out-File -FilePath $_outPath -Encoding utf8
      }
    }
    Write-Log -Message "  -> Written to: $_outPath" -Color Green
  }
  else {
    $items | Format-Table -AutoSize
  }
}



function Invoke-PSFOfficeWorkflow {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'Uses the public command PSCmdlet for confirmation before native execution.')]
  [CmdletBinding()]
  param ([object]$Plan, [string]$ExpectedAction, [System.Management.Automation.PSCmdlet]$Caller,
    [string]$OdtPath, [string]$LogRoot, [bool]$DryRun, [bool]$ForceCloseApps, [Security.SecureString]$ProductKey)

  $metadata = Get-PSFOfficeExecutionContext | ConvertTo-Json -Depth 30 -Compress
  $response = [PSFoundation.PowerShell.Office.OfficeCompatibility]::Deployment(($Plan | ConvertTo-Json -Depth 30 -Compress), $ExpectedAction, $OdtPath, $LogRoot, $ForceCloseApps, $ProductKey, $true, $metadata)
  if ($response.Failure) { Stop-PSFOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }
  $result = $response.Json | ConvertFrom-Json
  foreach ($warning in $result.Plan.Warnings) { Write-Warning $warning }
  if ($result.Status -ne 'Preview' -or $DryRun) { return $result }
  $fresh = $result.Plan
  $description = "$ExpectedAction; remove [$($fresh.RemoveProductId -join ',')]; ALL supported MSI: $($fresh.RemoveMsi); force-close apps: $ForceCloseApps"
  if ($fresh.Configuration) { $description += "; target $($fresh.Configuration.TargetProductId) $($fresh.Configuration.Version); languages [$($fresh.Configuration.Language -join ',')]" }
  if (-not $Caller.ShouldProcess($fresh.MachineId, $description)) { return $result }
  $response = [PSFoundation.PowerShell.Office.OfficeCompatibility]::Deployment(($fresh | ConvertTo-Json -Depth 30 -Compress), $ExpectedAction, $OdtPath, $LogRoot, $ForceCloseApps, $ProductKey, $false, $metadata)
  if ($response.Failure) { Stop-PSFOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }
  $response.Json | ConvertFrom-Json
}

function Invoke-PSFOfficeRecovery {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'Uses the public command PSCmdlet for confirmation before reopening evidence and executing.')]
  [CmdletBinding()]
  param ([object]$Recovery, [string]$OriginalAction, [System.Management.Automation.PSCmdlet]$Caller,
    [string]$OdtPath, [bool]$DryRun, [bool]$ForceCloseApps, [Security.SecureString]$ProductKey)

  Assert-PSFOfficeField $Recovery @('RunId', 'LogRoot', 'Path', 'Record') @('RunId', 'LogRoot')
  $metadata = Get-PSFOfficeExecutionContext | ConvertTo-Json -Depth 30 -Compress
  $response = [PSFoundation.PowerShell.Office.OfficeCompatibility]::Recover($Recovery.RunId, $OriginalAction, $OdtPath, $Recovery.LogRoot, $ForceCloseApps, $ProductKey, $true, $metadata)
  if ($response.Failure) { Stop-PSFOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }
  $result = $response.Json | ConvertFrom-Json
  foreach ($warning in $result.Plan.Warnings) { Write-Warning $warning }
  if ($result.Status -ne 'Preview' -or $DryRun) { return $result }
  $plan = $result.Plan
  $description = "Resume $OriginalAction; remove [$($plan.RemoveProductId -join ',')]; ALL supported MSI: $($plan.RemoveMsi); force-close apps: $ForceCloseApps; target $($plan.Configuration.TargetProductId) $($plan.Configuration.Version); languages [$($plan.Configuration.Language -join ',')]"
  if (-not $Caller.ShouldProcess($plan.MachineId, $description)) { return $result }
  # The native manager reopens and validates the journal; caller-supplied Record is never authority.
  $response = [PSFoundation.PowerShell.Office.OfficeCompatibility]::Recover($Recovery.RunId, $OriginalAction, $OdtPath, $Recovery.LogRoot, $ForceCloseApps, $ProductKey, $false, $metadata)
  if ($response.Failure) { Stop-PSFOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }
  $response.Json | ConvertFrom-Json
}

function Install-Office {
  <#
    .SYNOPSIS
      Installs Office only on a clean machine or returns a verified compliant no-op.
    .DESCRIPTION
      Accepts only an Install plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .PARAMETER ProductKey
      Optional SecureString volume key; never serialized or passed on a command line.
    .EXAMPLE
      $plan | Install-Office -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun,

    [Security.SecureString]
    $ProductKey
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'Install'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
      ProductKey     = $ProductKey
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Uninstall-Office {
  <#
    .SYNOPSIS
      Removes only explicitly selected Click-to-Run products.
    .DESCRIPTION
      Accepts only a Remove plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .EXAMPLE
      $plan | Uninstall-Office -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'Remove'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Switch-OfficeDeployment {
  <#
    .SYNOPSIS
      Executes an approved replacement after staging and verifying all destination media.
    .DESCRIPTION
      Accepts only a Migrate plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .PARAMETER ProductKey
      Optional SecureString volume key; never serialized or passed on a command line.
    .EXAMPLE
      $plan | Switch-OfficeDeployment -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun,

    [Security.SecureString]
    $ProductKey
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'Migrate'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
      ProductKey     = $ProductKey
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Update-Office {
  <#
    .SYNOPSIS
      Updates a pinned Office build while preserving other deployment dimensions.
    .DESCRIPTION
      Accepts only an Update plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .EXAMPLE
      $plan | Update-Office -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'Update'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Set-OfficeUpdateConfiguration {
  <#
    .SYNOPSIS
      Applies explicitly selected update settings without installing Office.
    .DESCRIPTION
      Accepts only a SetUpdateConfiguration plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .EXAMPLE
      $plan | Set-OfficeUpdateConfiguration -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'SetUpdateConfiguration'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Add-OfficeLanguage {
  <#
    .SYNOPSIS
      Adds selected full-UI languages while preserving primary language and existing resources.
    .DESCRIPTION
      Accepts only an AddLanguage plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .EXAMPLE
      $plan | Add-OfficeLanguage -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'AddLanguage'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Remove-OfficeLanguage {
  <#
    .SYNOPSIS
      Removes selected non-primary languages without removing the suite.
    .DESCRIPTION
      Accepts only a RemoveLanguage plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .EXAMPLE
      $plan | Remove-OfficeLanguage -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'RemoveLanguage'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Set-OfficeApplicationSelection {
  <#
    .SYNOPSIS
      Changes application exclusions while preserving product, build, architecture, and languages.
    .DESCRIPTION
      Accepts only a SetApplicationSelection plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .EXAMPLE
      $plan | Set-OfficeApplicationSelection -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'SetApplicationSelection'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Set-OfficeApplicationPreference {
  <#
    .SYNOPSIS
      Applies validated Office preferences to existing and future users through ODT customize.
    .DESCRIPTION
      Accepts only a SetApplicationPreference plan. Revalidates current inventory and media before
      confirmation and again under the shared deployment lock. Returns one final
      result after cleanup. Native failures may have changed machine state.
    .PARAMETER Plan
      Matching plan from Get-OfficeDeploymentPlan; pipeline input is supported.
    .PARAMETER OdtPath
      Existing Microsoft-signed ODT setup.exe; never downloaded automatically.
    .PARAMETER LogRoot
      Local Administrators/SYSTEM-only journal and log directory.
    .PARAMETER ForceCloseApps
      Explicitly authorize closing Office applications across sessions.
    .PARAMETER DryRun
      Return a read-only preview without journals, staging, or installer invocation.
    .EXAMPLE
      $plan | Set-OfficeApplicationPreference -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'The shared lifecycle calls ShouldProcess on the supplied PSCmdlet before any mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Plan,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [string]
    $LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office'),

    [switch]
    $ForceCloseApps,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Plan           = $Plan
      ExpectedAction = 'SetApplicationPreference'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      LogRoot        = $LogRoot
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
    }
    Invoke-PSFOfficeWorkflow @parameters
  }
}

function Stop-PSFOfficeOperation {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Throws a structured exception without changing machine state.')]
  [CmdletBinding()]
  param (
    [string]
    $Reason,

    [string]
    $Message,

    [object]
    $Diagnostic
  )

  $exception = New-Object InvalidOperationException($Message)
  $exception.Data['OfficeReason'] = $Reason
  if ($Diagnostic) { $exception.Data['OfficeDiagnostic'] = $Diagnostic }
  throw $exception
}

function Get-PSFOfficeMediaDiagnostic {
  [CmdletBinding()]
  param (
    [object]
    $Assessment
  )

  # A valid assessment with a changed fingerprint is not a validation failure.
  # Older/minimal assessments need not contain the optional diagnostic field.
  if ($null -ne $Assessment -and -not $Assessment.Valid) {
    if ($Assessment -is [Collections.IDictionary]) { return $Assessment['Diagnostic'] }
    $property = $Assessment.PSObject.Properties['Diagnostic']
    if ($property) { return $property.Value }
  }
}

function Assert-PSFOfficeField {
  [CmdletBinding()]
  param (
    [object]
    $InputObject,

    [string[]]
    $Allowed,

    [string[]]
    $Required = @()
  )

  if ($null -eq $InputObject -or $InputObject -is [string]) {
    Stop-PSFOfficeOperation InvalidContract 'An Office contract must be a data object.'
  }
  if ($InputObject -is [Collections.IDictionary]) {
    $names = @($InputObject.Keys)
  }
  else {
    $names = @($InputObject.PSObject.Properties | ForEach-Object { $_.Name })
  }
  foreach ($name in $names) {
    if ($name -notin $Allowed) {
      Stop-PSFOfficeOperation InvalidContract "Unexpected Office contract field: $name."
    }
  }
  foreach ($name in $Required) {
    if ($name -notin $names) {
      Stop-PSFOfficeOperation InvalidContract "Missing Office contract field: $name."
    }
  }
}

function ConvertTo-PSFOfficeList {
  [CmdletBinding()]
  param (
    [AllowEmptyCollection()]
    [string[]]
    $Value,

    [switch]
    $Language
  )

  $seen = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
  foreach ($item in $Value) {
    if ([string]::IsNullOrWhiteSpace($item)) {
      Stop-PSFOfficeOperation InvalidConfiguration 'Empty identifiers are not allowed.'
    }
    $normalized = $item.Trim()
    if ($Language) {
      $normalized = $normalized.ToLowerInvariant()
      # Deliberately bounded full-UI language support; no inferred proofing/LIP conversion.
      if ($normalized -notin @(
          'en-us',
          'de-de',
          'fr-fr',
          'es-es',
          'it-it',
          'nl-nl',
          'pt-br',
          'pt-pt',
          'ja-jp',
          'ko-kr',
          'zh-cn',
          'zh-tw',
          'pl-pl',
          'cs-cz',
          'da-dk',
          'fi-fi',
          'sv-se',
          'nb-no',
          'hu-hu',
          'tr-tr',
          'el-gr',
          'ro-ro',
          'sk-sk',
          'sl-si',
          'hr-hr',
          'bg-bg',
          'et-ee',
          'lv-lv',
          'lt-lt'
        )) {
        Stop-PSFOfficeOperation Unsupported "Language '$normalized' is outside the supported full-UI language catalog."
      }
    }
    elseif ($normalized -notmatch '^[A-Za-z0-9]+$') {
      Stop-PSFOfficeOperation InvalidConfiguration 'Product/application identifiers must be alphanumeric.'
    }
    if ($seen.Add($normalized)) {
      $normalized
    }
  }
}

function Get-PSFOfficeFingerprint {
  [CmdletBinding()]
  param (
    [object]
    $InputObject
  )

  $json = ConvertTo-Json -InputObject $InputObject -Depth 30 -Compress
  $sha = [Security.Cryptography.SHA256]::Create()
  try {
    ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json)))).Replace('-', '').ToLowerInvariant()
  }
  finally {
    $sha.Dispose()
  }
}

function ConvertTo-PSFOfficeConfiguration {
  [CmdletBinding()]
  param (
    [object]
    $Configuration
  )

  $fields = @(
    'SchemaVersion',
    'TargetProductId',
    'Architecture',
    'Channel',
    'Language',
    'PrimaryLanguage',
    'Version',
    'ExcludeApp',
    'RequestedLanguages',
    'LocaleSource',
    'LocaleEvidence'
  )
  Assert-PSFOfficeField $Configuration $fields $fields
  if ($Configuration.SchemaVersion -ne 1) {
    Stop-PSFOfficeOperation InvalidContract 'Unsupported configuration schema.'
  }
  $parameters = @{
    TargetProductId = $Configuration.TargetProductId
    Architecture    = $Configuration.Architecture
    Channel         = $Configuration.Channel
    Language        = @($Configuration.Language)
    ExcludeApp      = @($Configuration.ExcludeApp)
  }
  if ($Configuration.Version) {
    $parameters.Version = $Configuration.Version
  }
  $normalized = New-OfficeDeploymentConfiguration @parameters
  if ($Configuration.PrimaryLanguage -ne $normalized.PrimaryLanguage) {
    Stop-PSFOfficeOperation InvalidContract 'PrimaryLanguage must equal the first ordered language.'
  }
  if ($Configuration.LocaleSource -notin @('Default', 'Explicit', 'InstalledOffice', 'OperatingSystem', 'Recovery')) {
    Stop-PSFOfficeOperation InvalidContract 'Invalid locale source.'
  }
  $normalized.RequestedLanguages = @($Configuration.RequestedLanguages | ForEach-Object { [string]$_ })
  $normalized.LocaleSource = [string]$Configuration.LocaleSource
  $normalized.LocaleEvidence = @($Configuration.LocaleEvidence | ForEach-Object { [string]$_ })
  $normalized
}

function Assert-PSFOfficeSetting {
  [CmdletBinding()]
  param (
    [string]
    $Action,

    [object]
    $Settings
  )

  $allowed = @()
  switch ($Action) {
    'SetUpdateConfiguration' { $allowed = @('Enabled', 'UpdatePath', 'TargetVersion', 'Channel') }
    'SetApplicationPreference' { $allowed = @('Preferences') }
  }
  Assert-PSFOfficeField $Settings $allowed
  if ($Settings -is [Collections.IDictionary]) {
    $names = @($Settings.Keys)
  }
  else {
    $names = @($Settings.PSObject.Properties | ForEach-Object { $_.Name })
  }
  if ($Action -eq 'SetUpdateConfiguration') {
    if (-not $names.Count) {
      Stop-PSFOfficeOperation InvalidConfiguration 'Specify at least one update setting.'
    }
    if ('Enabled' -in $names -and $Settings.Enabled -isnot [bool]) {
      Stop-PSFOfficeOperation InvalidConfiguration 'Enabled must be a Boolean.'
    }
    if ('UpdatePath' -in $names -and [string]$Settings.UpdatePath -notmatch '^(https://|[A-Za-z]:\\|\\\\)') {
      Stop-PSFOfficeOperation InvalidConfiguration 'UpdatePath must be HTTPS or an absolute local/UNC path.'
    }
    if ('TargetVersion' -in $names -and [string]$Settings.TargetVersion -notmatch '^16\.0\.\d+\.\d+$') {
      Stop-PSFOfficeOperation InvalidConfiguration 'TargetVersion must be an exact Office build.'
    }
    if ('Channel' -in $names -and $Settings.Channel -notin @(
        'Current',
        'MonthlyEnterprise',
        'SemiAnnual',
        'PerpetualVL2019',
        'PerpetualVL2021',
        'PerpetualVL2024'
      )) {
      Stop-PSFOfficeOperation InvalidConfiguration 'Unsupported update channel.'
    }
  }
  elseif ($Action -eq 'SetApplicationPreference') {
    if ('Preferences' -notin $names -or -not @($Settings.Preferences).Count) {
      Stop-PSFOfficeOperation InvalidConfiguration 'At least one application preference is required.'
    }
    foreach ($preference in $Settings.Preferences) {
      Assert-PSFOfficeField $preference @('Key', 'Name', 'Value', 'Type', 'App', 'Id') @('Key', 'Name', 'Value', 'Type', 'App', 'Id')
      if ($preference.Key -notmatch '^software\\microsoft\\office\\16\.0\\(word|excel|powerpoint|outlook|access|onenote)(\\[a-z0-9 _-]+)+$' -or
        $preference.Type -notin @('REG_DWORD', 'REG_SZ') -or
        $preference.App -notin @('word16', 'excel16', 'ppt16', 'outlook16', 'access16', 'onenote16') -or
        $preference.Name -notmatch '^[a-zA-Z0-9 _-]+$' -or $preference.Id -notmatch '^[a-zA-Z0-9_-]+$') {
        Stop-PSFOfficeOperation InvalidConfiguration 'Application preference is outside the supported Office preference schema.'
      }
      if ($preference.Value -isnot [string] -and $preference.Value -isnot [int] -and $preference.Value -isnot [long]) {
        Stop-PSFOfficeOperation InvalidConfiguration 'Preference values must be strings or integers.'
      }
      if ($preference.Type -eq 'REG_DWORD' -and [string]$preference.Value -notmatch '^\d{1,10}$') {
        Stop-PSFOfficeOperation InvalidConfiguration 'REG_DWORD requires an unsigned decimal value.'
      }
      if ($preference.Type -eq 'REG_DWORD' -and [long]$preference.Value -gt [uint32]::MaxValue) {
        Stop-PSFOfficeOperation InvalidConfiguration 'REG_DWORD exceeds the unsigned 32-bit range.'
      }
    }
  }
}

function Get-PSFOfficeExecutionContext {
  [CmdletBinding()]
  param ()

  $module = $ExecutionContext.SessionState.Module
  [PSCustomObject]@{
    ModuleVersion     = if ($module -and $module.Name -eq 'PSFoundation') { [string]$module.Version } else { $null }
    ModulePath        = if ($module -and $module.Name -eq 'PSFoundation') { $module.Path } else { $PSCommandPath }
    PowerShellVersion = [string]$PSVersionTable.PSVersion
    PowerShellEdition = [string]$PSVersionTable.PSEdition
    ProcessBitness    = [IntPtr]::Size * 8
  }
}



function Resume-OfficeInstallation {
  <#
    .SYNOPSIS
      Verifies or continues the same interrupted Office installation.
    .DESCRIPTION
      Reopens a protected Install journal. Fully compliant targets are verified
      without reinstalling. Pre-launch work may continue. Uncertain partial ODT
      installations return UnsupportedRecoveryState pending disposable-VM validation.
      This command cannot inherit migration or removal authority.
    .PARAMETER Recovery
      Recovery descriptor from Get-OfficeDeploymentRecovery.
    .PARAMETER OdtPath
      Existing verified ODT setup.exe for supported continuation.
    .PARAMETER ForceCloseApps
      Explicitly authorize application closure during continuation.
    .PARAMETER ProductKey
      Fresh SecureString volume key when needed; never loaded from a journal.
    .PARAMETER DryRun
      Preview continuation without writes or process invocation.
    .EXAMPLE
      $recovery | Resume-OfficeInstallation -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'Shared lifecycle calls the supplied PSCmdlet ShouldProcess before mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Recovery,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [switch]
    $ForceCloseApps,

    [Security.SecureString]
    $ProductKey,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Recovery       = $Recovery
      OriginalAction = 'Install'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
      ProductKey     = $ProductKey
    }
    Invoke-PSFOfficeRecovery @parameters
  }
}

function Resume-OfficeMigration {
  <#
    .SYNOPSIS
      Verifies or continues an explicitly authorized Office migration.
    .DESCRIPTION
      Reopens a protected Migrate journal, checks current products against its
      original scope, and confirms the revised outstanding plan. Never expands
      removal to newly discovered products or resumes across a pending reboot.
    .PARAMETER Recovery
      Recovery descriptor from Get-OfficeDeploymentRecovery.
    .PARAMETER OdtPath
      Existing verified ODT setup.exe for supported continuation.
    .PARAMETER ForceCloseApps
      Explicitly authorize application closure during continuation.
    .PARAMETER ProductKey
      Fresh SecureString volume key when needed; never persisted.
    .PARAMETER DryRun
      Preview continuation without writes or process invocation.
    .EXAMPLE
      $recovery | Resume-OfficeMigration -OdtPath C:\ODT\setup.exe -WhatIf
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'Shared lifecycle calls the supplied PSCmdlet ShouldProcess before mutation.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
    [object]
    $Recovery,

    [Parameter(Mandatory = $true)]
    [string]
    $OdtPath,

    [switch]
    $ForceCloseApps,

    [Security.SecureString]
    $ProductKey,

    [switch]
    $DryRun
  )

  process {
    $parameters = @{
      Recovery       = $Recovery
      OriginalAction = 'Migrate'
      Caller         = $PSCmdlet
      OdtPath        = $OdtPath
      DryRun         = [bool]$DryRun
      ForceCloseApps = [bool]$ForceCloseApps
      ProductKey     = $ProductKey
    }
    Invoke-PSFOfficeRecovery @parameters
  }
}
