#Requires -Version 5.0

# Compatibility policy belongs to this PowerShell module, not the reusable service manager.
$script:ProtectedServiceNames = @('RemoteAccess', 'RemoteRegistry')

function Install-UPFAppxPackage {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
    [string[]]$DependencyPath,
    [string]$LicensePath,
    [switch]$Provisioned,
    [switch]$SkipLicense,
    [switch]$ForceUpdateFromAnyVersion,
    [switch]$DryRun
  )
  $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
  $action = if ($Provisioned) { 'Provision' } else { 'Install' }
  if ($DryRun -or -not $PSCmdlet.ShouldProcess($resolved, "$action UPF AppX/MSIX package")) {
    return New-PackageLifecycleResult -Target $resolved -Source 'UPFAppxPackage' -Action $action -Status 'Skipped' -SkippedReason 'WhatIf'
  }
  $dependencies = @($DependencyPath | ForEach-Object { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($_) })
  $license = if ($LicensePath) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($LicensePath) } else { $null }
  [PSFoundation.PowerShell.Packages.AppxCompatibility]::Install($resolved, $dependencies, $license, $Provisioned, $SkipLicense, $ForceUpdateFromAnyVersion)
}

function Update-UPFAppxPackage {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
    [string[]]$DependencyPath,
    [switch]$DryRun
  )
  Install-UPFAppxPackage -Path $Path -DependencyPath $DependencyPath -ForceUpdateFromAnyVersion -DryRun:$DryRun -WhatIf:$WhatIfPreference
}

function Install-UPFAppxPackageSet {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][string[]]$Path,
    [string[]]$DependencyPath,
    [switch]$Provisioned,
    [switch]$SkipLicense,
    [switch]$ForceUpdateFromAnyVersion,
    [switch]$PassThru,
    [switch]$DryRun
  )
  $results = foreach ($item in $Path) {
    Install-UPFAppxPackage -Path $item -DependencyPath $DependencyPath -Provisioned:$Provisioned -SkipLicense:$SkipLicense -ForceUpdateFromAnyVersion:$ForceUpdateFromAnyVersion -DryRun:$DryRun -WhatIf:$WhatIfPreference
  }
  if ($PassThru) { $results }
}

function Uninstall-UPFAppxPackageSet {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][string[]]$Pattern,
    [switch]$Installed,
    [switch]$Provisioned,
    [switch]$AllUsers,
    [switch]$IncludeProtected,
    [switch]$Force,
    [switch]$PassThru,
    [switch]$DryRun
  )
  $includeInstalled = $Installed -or -not $PSBoundParameters.ContainsKey('Installed')
  $results = @(foreach ($match in Find-UPFAppxPackage -Pattern $Pattern -Installed:$includeInstalled -Provisioned:$Provisioned -AllUsers:$AllUsers -IncludeProtected:$IncludeProtected) {
      if (-not $match.Matched) {
        New-PackageLifecycleResult -Target $match.Pattern -Source 'UPFAppxPackage' -Action 'Resolve' -Status 'Skipped' -SkippedReason 'NoMatch'
      }
      else {
        Uninstall-UPFAppxPackage -InputObject $match.Package -AllUsers:$AllUsers -IncludeProtected:$IncludeProtected -Force:$Force -DryRun:$DryRun -WhatIf:$WhatIfPreference
      }
    })
  if ($PassThru) { return $results }
  $removed = @($results | Where-Object Status -EQ 'Removed').Count
  $skipped = @($results | Where-Object Status -EQ 'Skipped').Count
  $failed = @($results | Where-Object Status -EQ 'Failed').Count
  Write-Log -Message "UPF AppX/MSIX package removal complete. Removed: $removed | Skipped: $skipped | Failed: $failed" -Color $(if ($failed -gt 0) { 'Yellow' } else { 'Green' })
}

function Merge-ObjectArrays {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Preserves the public v1 name.')]
  [OutputType([void])]
  param ([Parameter(Mandatory = $true)][array]$Base, [Parameter(Mandatory = $true)][array]$Overrides)
  [PSFoundation.PowerShell.Core.DataCompatibility]::Merge($Base, $Overrides)
}

function Get-DefaultApp {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [OutputType([string])]
  param ([Parameter(Mandatory = $true)][string]$FileExtension)
  $association = [PSFoundation.Windows.ApplicationAssociationManager]::new().Get($FileExtension)
  if ($null -eq $association) { Write-Error "Could not retrieve default application for '$FileExtension'."; return }
  $association.Command
}

function Request-AdministratorPrivilege {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding()]
  [OutputType([void])]
  param (
    [string]$ScriptPath = $MyInvocation.PSCommandPath,
    [System.Collections.IDictionary]$BoundParameters,
    [object[]]$ArgumentList,
    [switch]$IsElevatedRelaunch
  )
  if (Test-Elevation) { return }
  if ($IsElevatedRelaunch) {
    Write-Error -ErrorAction Continue 'Elevation was attempted but the process is still not elevated. Aborting to avoid a re-launch loop.'
    exit 1
  }
  if ([string]::IsNullOrWhiteSpace($ScriptPath)) { throw 'Could not determine the script path to re-launch. Pass -ScriptPath explicitly.' }
  $scriptFile = (Resolve-Path -LiteralPath $ScriptPath -ErrorAction Stop).ProviderPath
  $hostExe = (Get-Process -Id $PID).Path
  $workingDirectory = (Get-Location -PSProvider FileSystem).ProviderPath
  try {
    $exitCode = [PSFoundation.PowerShell.Security.ElevationCompatibility]::Run($hostExe, $scriptFile, $workingDirectory, $BoundParameters, $ArgumentList)
    exit $exitCode
  }
  catch {
    $native = $_.Exception.GetBaseException()
    if ($native -is [System.ComponentModel.Win32Exception] -and $native.NativeErrorCode -eq 1223) {
      Write-Error -ErrorAction Continue 'Elevation was cancelled by the user. Administrator privileges are required to continue.'
      exit 1223
    }
    throw
  }
}

function New-EncryptedCredentialFile {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [OutputType([PSCustomObject])]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param (
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$KeyPath,
    [Parameter(Mandatory = $false)][PSCredential]$Credential,
    [Parameter(Mandatory = $false)][string]$UserName
  )
  if ($null -eq $Credential -and -not $UserName) { throw 'Supply -Credential (programmatic) or -UserName (interactive password prompt).' }
  $ownedPassword = $null
  try {
    if ($null -eq $Credential) {
      $ownedPassword = Read-Host -Prompt "Password for '$UserName'" -AsSecureString
      $Credential = [PSCredential]::new($UserName, $ownedPassword)
    }
    if (-not $PSCmdlet.ShouldProcess("$Path / $KeyPath", 'Write encrypted credential files')) {
      if ($WhatIfPreference) { New-OperationResult -Target $Path -Source 'CredentialFile' -Action 'Write' -Status 'DryRun' -Detail 'No files written.' }
      return
    }
    $destination = [PSFoundation.IO.FileSystemPath]::Parse($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
    $key = [PSFoundation.IO.FileSystemPath]::Parse($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($KeyPath))
    [PSFoundation.Security.CredentialFileManager]::new().Write($destination, $key, $Credential.UserName, $Credential.Password, $true)
    New-OperationResult -Target $Path -Source 'CredentialFile' -Action 'Write' -Status 'Completed' -Detail 'Credential stored with a random AES key; key file written separately.'
  }
  finally { if ($null -ne $ownedPassword) { $ownedPassword.Dispose() } }
}

function Get-OfficeDeploymentRecovery {
  <#
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true)][string]$Name, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::Group($Name, $Configuration)
}

function Get-SecurityEventDefinition {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding()]
  param ([string]$Group, [string]$LogName, [string]$ProviderName, [int[]]$Id, [string]$Name, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::Definitions($Configuration, $Group, $LogName, $ProviderName, $Id, $Name)
}

function Resolve-WindowsEventMappedField {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true)][string]$MapName, [Parameter(Mandatory = $true)][object]$Value, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::MappedField($MapName, $Value, $Configuration)
}

function ConvertFrom-WinEvent {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true, ValueFromPipeline = $true)][object]$Event, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  process { [PSFoundation.PowerShell.Security.EventConfigurationCompatibility]::ConvertEvent($Event, $Configuration) }
}

function Get-WindowsEventByDefinition {
  <#
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [OutputType([hashtable])]
  param ()
  [PSFoundation.PowerShell.Windows.IdentityCompatibility]::GetUserInfo()
}

function Get-UserSID {
  <#
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([ValidateSet('Any', 'WiFi', 'Ethernet', 'VPN')][string]$Type = 'Any', [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-DefaultNetworkAdapter' @PSBoundParameters
}

function Get-IPAddress {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-IPAddress' @PSBoundParameters
}

function Get-SubnetMask {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-SubnetMask' @PSBoundParameters
}

function Get-DefaultGateway {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-DefaultGateway' @PSBoundParameters
}

function Get-DNSServer {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-DNSServer' @PSBoundParameters
}

function Get-MACAddress {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-MACAddress' @PSBoundParameters
}

function Get-NetworkPrefix {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [Alias('Get-Network', 'Get-Prefix')]
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-NetworkPrefix' @PSBoundParameters
}

function Get-NetworkPrefixCIDR {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  [Alias('Get-NetworkCIDR', 'Get-PrefixCIDR')]
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-NetworkPrefixCIDR' @PSBoundParameters
}

function Get-BroadcastAddress {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-BroadcastAddress' @PSBoundParameters
}

function Get-MulticastAddress {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)
  Invoke-PSFNetworkCompatibility -Operation 'Get-MulticastAddress' @PSBoundParameters
}

function Get-DefenderThreatDetection {
  <#
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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
  .EXTERNALHELP PSFoundation-help.xml
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

function Convert-Quote {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>

  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Function merges two object arrays; existing public name is intentionally plural.')]
  [OutputType([void])]
  param (
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Single", "Double")]
    [string]$To = "Double"
  )
  $content = Get-Content -Path $Path -Raw
  $converted = [PSFoundation.Core.TextConverter]::ConvertQuotes([string]$content, [PSFoundation.Core.QuoteStyle]$To)
  Set-Content -Path $Path -Value $converted

}

function New-DriveMapping {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>

  [OutputType([PSCustomObject])]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param (
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateLength(1, 1)]
    [ValidatePattern('[A-Z]')]
    [ValidateScript({
        if (($null -ne (Get-Volume $_ -ErrorAction SilentlyContinue))) {
          throw 'DriveLetter cannot be a physical volume'
        }
        $true
      })]
    [string]
    $DriveLetter,

    [Parameter(Mandatory = $true, Position = 1)]
    [ValidateScript({
        if ((Test-Path -LiteralPath $_) -and ((Split-Path -Path $_ -Leaf) -ne $_)) {
          return $true
        }
        throw 'Path does not exist or is a root level folder'
      })]
    [string]
    $Path,

    [Parameter(Mandatory = $false)]
    [string]
    $SourceDriveLabel,

    [Parameter(Mandatory = $false)]
    [string]
    $DriveLabel,

    [Parameter(Mandatory = $false)]
    [switch]
    $Restart,

    [Parameter(Mandatory = $false)]
    [switch]
    $Force
  )
  if (-not (Test-Elevation)) { throw 'New-DriveMapping requires an elevated process (administrator token).' }
  $manager = [PSFoundation.Windows.DriveMappingManager]::new()
  $directory = [PSFoundation.IO.FileSystemPath]::Parse([IO.Path]::GetFullPath($Path))
  $plan = $manager.Prepare([char]$DriveLetter.ToUpperInvariant(), $directory, $DriveLabel, $SourceDriveLabel, [bool]$Force)
  if (-not $PSCmdlet.ShouldProcess("$DriveLetter`:", "Map folder '$Path' to drive letter")) {
    if ($WhatIfPreference) { New-OperationResult -Target "$DriveLetter`:" -Source 'DOS Devices' -Action CreateMapping -Status DryRun -Detail "Would map '$Path' to drive letter '$DriveLetter`:'." }
    return
  }
  $result = $manager.Create($plan.DriveLetter, $plan.Directory, $plan.Label, $plan.SourceLabel, [bool]$Force, [Threading.CancellationToken]::None)
  if ($Restart) { Restart-Computer -Force }
  New-OperationResult -Target "$($result.DriveLetter):" -Source 'DOS Devices' -Action CreateMapping -Status Completed -Detail "Mapped '$Path' to drive letter '$($result.DriveLetter):'." -Property @{ Path = $result.Directory.Value; DriveLabel = $result.Label }

}

function Remove-DriveMapping {
  <#
  .EXTERNALHELP PSFoundation-help.xml
  #>

  [OutputType([PSCustomObject])]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param (
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateLength(1, 1)]
    [ValidatePattern('[A-Z]')]
    [ValidateScript({
        $value = Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices' -Name "$_`:" -ErrorAction SilentlyContinue
        if ($null -eq $value) {
          throw 'Drive letter not found or does not represent a mapped drive'
        }
        $true
      })]
    [string]
    $DriveLetter,

    [Parameter(Mandatory = $false)]
    [string]
    $SourceDriveLabel,

    [Parameter(Mandatory = $false)]
    [switch]
    $Restart,

    [Parameter(Mandatory = $false)]
    [switch]
    $Force
  )
  if (-not (Test-Elevation)) { throw 'Remove-DriveMapping requires an elevated process (administrator token).' }
  $manager = [PSFoundation.Windows.DriveMappingManager]::new()
  $mapping = $manager.Get([char]$DriveLetter.ToUpperInvariant())
  if (-not $mapping) { throw "Drive letter '$DriveLetter`:' is not a mapped drive" }
  if (-not $PSCmdlet.ShouldProcess("$DriveLetter`:", 'Remove drive letter mapping')) {
    if ($WhatIfPreference) { New-OperationResult -Target "$DriveLetter`:" -Source 'DOS Devices' -Action RemoveMapping -Status DryRun -Detail "Would remove mapping for drive letter '$DriveLetter`:'." }
    return
  }
  $label = $manager.Remove($mapping.DriveLetter, $SourceDriveLabel, [bool]$Force, [Threading.CancellationToken]::None)
  if ($Restart) { Restart-Computer -Force }
  New-OperationResult -Target "$($mapping.DriveLetter):" -Source 'DOS Devices' -Action RemoveMapping -Status Completed -Detail "Removed mapping for drive letter '$($mapping.DriveLetter):'." -Property @{ SourceDriveLabel = $label }

}
