#Requires -Version 5.0

# Compatibility policy belongs to this PowerShell module, not the reusable service manager.
$script:ProtectedServiceNames = @('RemoteAccess', 'RemoteRegistry')

function Install-UPFAppxPackage {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Packages.AppxCompatibility]::Install($resolved, $dependencies, $license, $Provisioned, $SkipLicense, $ForceUpdateFromAnyVersion)
}

function Update-UPFAppxPackage {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'Delegates confirmation and WhatIf to Uninstall-UPFAppxPackage for each resolved package; verified by the wrapper delegation test.')]
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
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Preserves the public v1 name.')]
  [OutputType([void])]
  param ([Parameter(Mandatory = $true)][array]$Base, [Parameter(Mandatory = $true)][array]$Overrides)

  [AdNoctem.Substrate.PowerShell.Core.DataCompatibility]::Merge($Base, $Overrides)
}

function Get-DefaultApp {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [OutputType([string])]
  param ([Parameter(Mandatory = $true)][string]$FileExtension)

  $association = [AdNoctem.Substrate.Windows.ApplicationAssociationManager]::new().Get($FileExtension)

  if ($null -eq $association) { Write-Error "Could not retrieve default application for '$FileExtension'."; return }

  $association.Command
}

function Request-AdministratorPrivilege {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    $exitCode = [AdNoctem.Substrate.PowerShell.Security.ElevationCompatibility]::Run($hostExe, $scriptFile, $workingDirectory, $BoundParameters, $ArgumentList)

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
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

    $destination = [AdNoctem.Substrate.IO.FileSystemPath]::Parse($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
    $key = [AdNoctem.Substrate.IO.FileSystemPath]::Parse($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($KeyPath))
    [AdNoctem.Substrate.Security.CredentialFileManager]::new().Write($destination, $key, $Credential.UserName, $Credential.Password, $true)
    New-OperationResult -Target $Path -Source 'CredentialFile' -Action 'Write' -Status 'Completed' -Detail 'Credential stored with a random AES key; key file written separately.'
  }
  finally { if ($null -ne $ownedPassword) { $ownedPassword.Dispose() } }
}

function Get-OfficeDeploymentRecovery {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-fA-F0-9]{32}$')][string]$RunId,
    [string]$LogRoot = (Join-Path $env:ProgramData 'PSFoundation-Office')
  )

  $outcome = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::ReadRecovery($RunId, $LogRoot)

  if ($outcome.Failure) { Stop-SubstrateOfficeNativeFailure $outcome.Failure }

  [PSCustomObject]@{ RunId = $RunId; LogRoot = $LogRoot; Path = Join-Path $LogRoot ($RunId + '.json'); Record = ConvertFrom-Json -InputObject $outcome.Json }
}

function Get-TransportMessageId {
  [CmdletBinding()]
  [OutputType([string])]
  param ([Parameter(Mandatory = $true)][AllowEmptyString()][string]$HeaderText)

  [AdNoctem.Substrate.Interop.MailHeaderParser]::GetTransportMessageId($HeaderText)
}

function Write-SubstrateOfficeJson {
  [CmdletBinding()]
  param ([string]$Path, [object]$Value)

  $json = ConvertTo-Json -InputObject $Value -Depth 30
  $failure = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::WriteJournal($Path, $json)

  if ($failure) { Stop-SubstrateOfficeNativeFailure $failure }
}

function Test-SubstrateOfficeHost {
  [CmdletBinding()]
  [OutputType([bool])]
  param ()

  [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::SupportedHost()
}

function Assert-SubstrateOfficeHost {
  [CmdletBinding()]
  param ()

  $failure = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::CheckHost()

  if ($failure) { Stop-SubstrateOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Enter-SubstrateOfficeLock {
  [CmdletBinding()]
  param ()

  try { [AdNoctem.Substrate.Office.OfficeDeploymentLock]::Acquire() }
  catch {
    $errorValue = $_.Exception

    while ($errorValue.InnerException -and -not $errorValue.Data.Contains('OfficeReason')) { $errorValue = $errorValue.InnerException }

    if ($errorValue.Data.Contains('OfficeReason')) { Stop-SubstrateOfficeOperation $errorValue.Data['OfficeReason'] $errorValue.Message }

    throw
  }
}

function Invoke-SubstrateOfficeConfiguration {
  [CmdletBinding()]
  param ([string]$OdtPath, [xml]$Document, [string]$Directory, [ValidateSet('/download', '/configure', '/customize')][string]$Mode = '/configure', [Security.SecureString]$ProductKey)

  $outcome = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::InvokeConfiguration($OdtPath, $Document, $Directory, $Mode, $ProductKey)

  if ($outcome.Failure) { Stop-SubstrateOfficeNativeFailure $outcome.Failure }

  $outcome.Result
}

function Stop-SubstrateOfficeNativeFailure {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Raises an operation failure without changing machine state.')]
  [CmdletBinding()]
  param ([object]$Failure)

  try { Stop-SubstrateOfficeOperation $Failure.ReasonCode $Failure.Detail $Failure.Diagnostic }
  catch {
    if ($Failure.CleanupError) { $_.Exception.Data['OfficeCleanupError'] = $Failure.CleanupError }

    if ($null -ne $Failure.ExitCode) { $_.Exception.Data['OfficeExitCode'] = $Failure.ExitCode }

    throw
  }
}

function ConvertFrom-SubstrateOfficeMediaAssessment {
  [CmdletBinding()]
  param ([object]$Assessment)

  $manifest = $null
  $fingerprint = $null

  if ($Assessment.Valid) {
    $manifest = ConvertFrom-Json -InputObject $Assessment.ManifestJson -ErrorAction Stop
    $fingerprint = Get-SubstrateOfficeFingerprint $manifest
  }

  [PSCustomObject]@{
    Valid = $Assessment.Valid; Path = $Assessment.Path; Manifest = $manifest; Fingerprint = $fingerprint
    ReasonCode = $Assessment.ReasonCode; Error = $Assessment.Error; Diagnostic = $Assessment.Diagnostic
  }
}

function Save-OfficeDeploymentMedia {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Media names the deployment payload as a collective noun.')]
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
  [OutputType([PSCustomObject])]
  param ([Parameter(Mandatory = $true)][object]$Configuration, [Parameter(Mandatory = $true)][string]$SourcePath, [Parameter(Mandatory = $true)][string]$OdtPath, [switch]$DryRun)

  $target = ConvertTo-SubstrateOfficeConfiguration $Configuration
  Assert-SubstrateOfficePath $SourcePath

  if (Test-Path -LiteralPath $SourcePath) {
    $existing = Test-OfficeDeploymentMedia -SourcePath $SourcePath -Configuration $target

    if (-not $existing.Valid) { Stop-SubstrateOfficeOperation InvalidMedia 'Existing package is incomplete or incompatible; use a new destination.' (Get-SubstrateOfficeMediaDiagnostic $existing) }

    return $existing
  }

  if (-not (Test-OfficeDeploymentTool $OdtPath).Valid) { Stop-SubstrateOfficeOperation UntrustedTool 'ODT verification failed.' }

  if ($DryRun -or -not $PSCmdlet.ShouldProcess($SourcePath, 'Download pinned Office media and publish verified package')) {
    return [PSCustomObject]@{ Status = 'Preview'; Path = $SourcePath; Configuration = $target }
  }

  $outcome = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::PrepareMedia($target, $SourcePath, $OdtPath)

  if ($outcome.Failure) { Stop-SubstrateOfficeNativeFailure $outcome.Failure }

  ConvertFrom-SubstrateOfficeMediaAssessment $outcome.Result
}

function Test-OfficeDeploymentMedia {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Media names the deployment payload as a collective noun.')]
  [CmdletBinding()]
  [OutputType([PSCustomObject])]
  param ([Parameter(Mandatory = $true)][string]$SourcePath, [object]$Configuration)

  $stage = 'ProtectedPath'

  try {
    Assert-SubstrateOfficeProtectedPath $SourcePath -ObjectKind MediaDirectory
    $stage = 'Validation'
    $target = $null

    if ($Configuration) { $target = ConvertTo-SubstrateOfficeConfiguration $Configuration }

    $assessment = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::InspectMedia($SourcePath, $target)
    ConvertFrom-SubstrateOfficeMediaAssessment $assessment
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

function Get-SubstrateOfficeMediaFile {
  [CmdletBinding()]
  param ([string]$Root)

  $outcome = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::MediaFiles($Root)

  if ($outcome.Failure) { Stop-SubstrateOfficeOperation $outcome.Failure.ReasonCode $outcome.Failure.Detail $outcome.Failure.Diagnostic }

  $outcome.Files
}

function Invoke-SubstrateOfficeTool {
  [CmdletBinding()]
  param ([string]$OdtPath, [ValidateSet('/download', '/configure', '/customize', '/help')][string]$Mode, [string]$ConfigurationPath)

  $outcome = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::InvokeTool($OdtPath, $Mode, $ConfigurationPath)

  if ($outcome.Failure) { Stop-SubstrateOfficeOperation $outcome.Failure.ReasonCode $outcome.Failure.Detail $outcome.Failure.Diagnostic }

  $outcome.Result
}

function Remove-SubstrateOfficeWorkDirectory {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private cleanup of the operation-owned directory after approval.')]
  [CmdletBinding()]
  param ([string]$Path, [string]$Parent)

  if (-not $Path) { return }

  $failure = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::RemoveDeploymentDirectory($Path, $Parent)

  if ($failure) { Stop-SubstrateOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Assert-SubstrateOfficePath {
  [CmdletBinding()]
  param ([string]$Path)

  $failure = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::CheckDeploymentPath($Path, $false, 'DeploymentFile')

  if ($failure) { Stop-SubstrateOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Assert-SubstrateOfficeProtectedPath {
  [CmdletBinding()]
  param ([string]$Path, [string]$ObjectKind = 'DeploymentFile')

  $failure = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::CheckDeploymentPath($Path, $true, $ObjectKind)

  if ($failure) { Stop-SubstrateOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function New-SubstrateOfficeProtectedDirectory {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private primitive called only after public ShouldProcess approval.')]
  [CmdletBinding()]
  param ([string]$Path)

  $failure = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::CreateDeploymentDirectory($Path)

  if ($failure) { Stop-SubstrateOfficeOperation $failure.ReasonCode $failure.Detail $failure.Diagnostic }
}

function Get-OfficeDeploymentPlan {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Stop-SubstrateOfficeOperation InvalidAuthority 'This action cannot remove products.'
  }

  if ($Action -notin @('AddLanguage', 'RemoveLanguage') -and $Language.Count) {
    Stop-SubstrateOfficeOperation InvalidAuthority 'Language selection belongs only to language operations.'
  }

  Assert-SubstrateOfficeSetting $Action $Settings
  $selection = @(ConvertTo-SubstrateOfficeList $RemoveProductId)
  $languages = @(ConvertTo-SubstrateOfficeList $Language -Language)
  $target = $null

  if ($Configuration) { $target = ConvertTo-SubstrateOfficeConfiguration $Configuration }

  [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::ValidateRequest($Action, $target, $SourcePath, [string[]]$selection, [bool]$RemoveMsi, [string[]]$languages, $Settings)

  if (-not $Inventory) { $Inventory = Get-OfficeInventory }

  $media = $null

  if ($target -and $SourcePath) {
    $media = Test-OfficeDeploymentMedia -SourcePath $SourcePath -Configuration $target

    if ($media.Valid -and -not $target.Version) { $target.Version = $media.Manifest.Version }
  }

  [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::CreatePlan($Action, $target, $SourcePath, [string[]]$selection, [bool]$RemoveMsi,
    [string[]]$languages, $Settings, $Inventory, $media, (Get-SubstrateOfficeFingerprint $Inventory))
}

function New-SubstrateOfficeXml {
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
    Stop-SubstrateOfficeOperation InvalidAuthority 'XML operation cannot contain the requested removal.'
  }

  Assert-SubstrateOfficeSetting $Action $Settings
  $target = $null

  if ($Configuration) { $target = ConvertTo-SubstrateOfficeConfiguration $Configuration }

  return , [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::CreateXml($Action, $target, $MediaPath, $RemoveProductId, $RemoveMsi, $Language, $Settings)
}

function Test-OfficeDeployment {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)][object]$Configuration,
    [object]$Inventory
  )

  $target = ConvertTo-SubstrateOfficeConfiguration $Configuration

  if (-not $Inventory) { $Inventory = Get-OfficeInventory }

  [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Assess($target, $Inventory)
}

function Install-Win32Program {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    return [AdNoctem.Substrate.PowerShell.Packages.ProgramCompatibility]::SkipInstall($target, $RunId, $PSBoundParameters.ContainsKey('RunId'))
  }

  [AdNoctem.Substrate.PowerShell.Packages.ProgramCompatibility]::Install($target, $ArgumentList, $NoWait, $PassThru, $SuccessExitCodes, $RebootExitCodes, $RunId, $PSBoundParameters.ContainsKey('RunId'))
}

function Set-ServiceStartupState {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding(SupportsShouldProcess = $true)]
  param (
    [Parameter(Mandatory = $true)][string[]]$Name,
    [Parameter(Mandatory = $true)][ValidateSet('Automatic', 'Manual', 'Disabled')][string]$StartupType,
    [string[]]$Filter
  )

  foreach ($selection in [AdNoctem.Substrate.PowerShell.Windows.ServiceCompatibility]::Select($Name, $StartupType, $Filter, $script:ProtectedServiceNames)) {
    if ($selection.SkippedResult) { $selection.SkippedResult; continue }

    if (-not $PSCmdlet.ShouldProcess($selection.Name, "Set startup type to $StartupType")) {
      [AdNoctem.Substrate.PowerShell.Windows.ServiceCompatibility]::Result($selection.Name, 'Skipped', 'WhatIf')
      continue
    }

    [AdNoctem.Substrate.PowerShell.Windows.ServiceCompatibility]::Apply($selection.Name, $StartupType)
  }
}

function Import-SecurityEventConfiguration {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  param ([string]$Path = (Join-Path $PSScriptRoot 'security.psd1'), [switch]$Force)

  $cached = try { $script:SecurityEventConfiguration } catch { $null }

  if ($cached -and -not $Force) { return $cached }

  $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
  $script:SecurityEventConfiguration = [AdNoctem.Substrate.PowerShell.Security.EventConfigurationCompatibility]::Read($resolved)
  $script:SecurityEventConfiguration
}

function Get-SecurityEventGroup {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true)][string]$Name, [hashtable]$Configuration = (Import-SecurityEventConfiguration))

  [AdNoctem.Substrate.PowerShell.Security.EventConfigurationCompatibility]::Group($Name, $Configuration)
}

function Get-SecurityEventDefinition {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  param ([string]$Group, [string]$LogName, [string]$ProviderName, [int[]]$Id, [string]$Name, [hashtable]$Configuration = (Import-SecurityEventConfiguration))

  [AdNoctem.Substrate.PowerShell.Security.EventConfigurationCompatibility]::Definitions($Configuration, $Group, $LogName, $ProviderName, $Id, $Name)
}

function Resolve-WindowsEventMappedField {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true)][string]$MapName, [Parameter(Mandatory = $true)][object]$Value, [hashtable]$Configuration = (Import-SecurityEventConfiguration))

  [AdNoctem.Substrate.PowerShell.Security.EventConfigurationCompatibility]::MappedField($MapName, $Value, $Configuration)
}

function ConvertFrom-WinEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  param ([Parameter(Mandatory = $true, ValueFromPipeline = $true)][object]$Event, [hashtable]$Configuration = (Import-SecurityEventConfiguration))
  process { [AdNoctem.Substrate.PowerShell.Security.EventConfigurationCompatibility]::ConvertEvent($Event, $Configuration) }
}

function Get-WindowsEventByDefinition {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    $queries = [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::Prepare($definitions.ToArray(), $Group, $Id, $LogName, $StartTime, $EndTime, $MaxEvents, $Configuration)
    [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::Read($queries, $ComputerName, $SkipMissingChannel, { param($message) Write-Error $message })
  }
}

function Get-WindowsLogonEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::ReadGroup('Logon', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ LogonType = $LogonType; IncludeSystem = [bool]$IncludeSystem }, { param($message) Write-Error $message })
}

function Get-WindowsAccountChangeEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::ReadGroup('AccountChange', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ TargetUserName = $TargetUserName; SubjectUserName = $SubjectUserName }, { param($message) Write-Error $message })
}

function Get-WindowsServiceEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::ReadGroup('Service', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ ServiceName = $ServiceName }, { param($message) Write-Error $message })
}

function Get-WindowsBootEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::ReadGroup('BootShutdown', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{}, { param($message) Write-Error $message })
}

function Get-WindowsPowerShellEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::ReadGroup('PowerShell', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ UserName = $UserName; ScriptBlockText = $ScriptBlockText }, { param($message) Write-Error $message })
}

function Get-WindowsScheduledTaskEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::ReadGroup('ScheduledTask', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{ TaskName = $TaskName }, { param($message) Write-Error $message })
}

function Get-WindowsSysmonEvent {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  [AdNoctem.Substrate.PowerShell.Security.EventQueryCompatibility]::ReadGroup('Sysmon', $StartTime, $EndTime, $Id, $ComputerName, $MaxEvents, $Configuration, @{}, { param($message) Write-Error $message })
}

function Get-UserInfo {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [OutputType([hashtable])]
  param ()

  [AdNoctem.Substrate.PowerShell.Windows.IdentityCompatibility]::GetUserInfo()
}

function Get-UserSID {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [OutputType([string])]
  param ([Parameter(Mandatory = $true)][string]$UserName)

  try { [AdNoctem.Substrate.PowerShell.Windows.IdentityCompatibility]::GetSecurityIdentifier($UserName) }
  catch {
    Write-Error "Could not find SID for user '$UserName'. $_"

    return $null
  }
}

function Test-LGPOInstalled {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding()]
  [OutputType([bool])]
  param (
    [Parameter(Mandatory = $false)]
    [string]$Path = (Join-Path -Path $env:ProgramData -ChildPath 'AdNoctem.Substrate.PowerShell\tools\LGPO.exe')
  )

  if ([string]::IsNullOrEmpty($Path)) { return (Test-Path -LiteralPath $Path -PathType Leaf) }

  $provider = $null
  $drive = $null

  try { $filePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref]$provider, [ref]$drive) }
  catch { return (Test-Path -LiteralPath $Path -PathType Leaf) }

  if ($provider.Name -ne 'FileSystem') { return (Test-Path -LiteralPath $Path -PathType Leaf) }

  [AdNoctem.Substrate.Policies.LgpoTool]::new().IsInstalled([AdNoctem.Substrate.IO.FileSystemPath]::Parse($filePath))
}

function Invoke-LGPO {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [CmdletBinding(SupportsShouldProcess)]
  [OutputType([PSCustomObject])]
  param (
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ })]
    [string]$PolicyPath,
    [Parameter(Mandatory = $false)]
    [string]$LgpoExe = (Join-Path -Path $env:ProgramData -ChildPath 'AdNoctem.Substrate.PowerShell\tools\LGPO.exe')
  )

  if (-not (Test-Path -LiteralPath $LgpoExe -PathType Leaf)) {
    throw "LGPO.exe not found at '$LgpoExe'. Run Install-LGPO first."
  }

  $arg = if (Test-Path -LiteralPath $PolicyPath -PathType Container) { '/g' } else { '/t' }

  if (-not $PSCmdlet.ShouldProcess($PolicyPath, "Apply via LGPO.exe ($arg)")) { return }

  $executable = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($LgpoExe)
  $policy = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($PolicyPath)

  try { [AdNoctem.Substrate.PowerShell.Policies.LgpoCompatibility]::Apply($executable, $policy, $PolicyPath, $PSEdition -eq 'Desktop') }
  catch [ComponentModel.Win32Exception] { $PSCmdlet.ThrowTerminatingError([AdNoctem.Substrate.PowerShell.Policies.LgpoCompatibility]::LaunchError($_.Exception)) }
}

function Show-Color {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'ArgumentList', Justification = 'Unused extra arguments are intentionally accepted to preserve the original Show-Color API.')]
  [CmdletBinding()]
  param ([Parameter(ValueFromRemainingArguments = $true)][object[]]$ArgumentList)

  foreach ($color in [AdNoctem.Substrate.PowerShell.Diagnostics.ColorCompatibility]::GetColors()) {
    Write-Host $color -ForegroundColor $color
  }
}

# Keep the networking parameter declarations here deliberately simple: adding CmdletBinding
# would change how these legacy functions accept unused arguments.
function Invoke-SubstrateNetworkCompatibility {
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

  $result = [AdNoctem.Substrate.PowerShell.Networking.NetworkCompatibility]::Invoke($Operation, $Adapter, $AddressFamily, [bool]$Required, $Type)

  foreach ($message in $result.Messages) { Write-Log -Message $message -Color Red }

  if ($null -ne $result.Error) { $PSCmdlet.ThrowTerminatingError($result.Error) }

  if ($null -ne $result.Failure) { throw $result.Failure }

  $result.Value
}

function Get-DefaultNetworkAdapter {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([ValidateSet('Any', 'WiFi', 'Ethernet', 'VPN')][string]$Type = 'Any', [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-DefaultNetworkAdapter' @PSBoundParameters
}

function Get-IPAddress {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-IPAddress' @PSBoundParameters
}

function Get-SubnetMask {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-SubnetMask' @PSBoundParameters
}

function Get-DefaultGateway {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-DefaultGateway' @PSBoundParameters
}

function Get-DNSServer {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-DNSServer' @PSBoundParameters
}

function Get-MACAddress {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-MACAddress' @PSBoundParameters
}

function Get-NetworkPrefix {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [Alias('Get-Network', 'Get-Prefix')]
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-NetworkPrefix' @PSBoundParameters
}

function Get-NetworkPrefixCIDR {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  [Alias('Get-NetworkCIDR', 'Get-PrefixCIDR')]
  param ([ValidateSet('IPv4', 'IPv6')][string]$AddressFamily = 'IPv4', [PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-NetworkPrefixCIDR' @PSBoundParameters
}

function Get-BroadcastAddress {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-BroadcastAddress' @PSBoundParameters
}

function Get-MulticastAddress {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
  #>
  param ([PSCustomObject]$Adapter, [switch]$Required)

  Invoke-SubstrateNetworkCompatibility -Operation 'Get-MulticastAddress' @PSBoundParameters
}

function Get-DefenderThreatDetection {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  $detections = @([AdNoctem.Substrate.PowerShell.Security.InspectionCompatibility]::Detections($cutoffDate, $IncludeURLs.IsPresent))

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
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  $threats = @([AdNoctem.Substrate.PowerShell.Security.InspectionCompatibility]::Threats($IncludeURLs.IsPresent))

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
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
  $items = @([AdNoctem.Substrate.PowerShell.Security.InspectionCompatibility]::Files($resolvedPath, $windowStart, $windowEnd, ($PSVersionTable.PSEdition -eq 'Desktop'), [Action[string]] { param($message) Write-Verbose $message }))

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



function Invoke-SubstrateOfficeWorkflow {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'Uses the public command PSCmdlet for confirmation before native execution.')]
  [CmdletBinding()]
  param ([object]$Plan, [string]$ExpectedAction, [System.Management.Automation.PSCmdlet]$Caller,
    [string]$OdtPath, [string]$LogRoot, [bool]$DryRun, [bool]$ForceCloseApps, [Security.SecureString]$ProductKey)

  $metadata = Get-SubstrateOfficeExecutionContext | ConvertTo-Json -Depth 30 -Compress
  $response = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Deployment(($Plan | ConvertTo-Json -Depth 30 -Compress), $ExpectedAction, $OdtPath, $LogRoot, $ForceCloseApps, $ProductKey, $true, $metadata)

  if ($response.Failure) { Stop-SubstrateOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }

  $result = $response.Json | ConvertFrom-Json

  foreach ($warning in $result.Plan.Warnings) { Write-Warning $warning }

  if ($result.Status -ne 'Preview' -or $DryRun) { return $result }

  $fresh = $result.Plan
  $description = "$ExpectedAction; remove [$($fresh.RemoveProductId -join ',')]; ALL supported MSI: $($fresh.RemoveMsi); force-close apps: $ForceCloseApps"

  if ($fresh.Configuration) { $description += "; target $($fresh.Configuration.TargetProductId) $($fresh.Configuration.Version); languages [$($fresh.Configuration.Language -join ',')]" }

  if (-not $Caller.ShouldProcess($fresh.MachineId, $description)) { return $result }

  $response = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Deployment(($fresh | ConvertTo-Json -Depth 30 -Compress), $ExpectedAction, $OdtPath, $LogRoot, $ForceCloseApps, $ProductKey, $false, $metadata)

  if ($response.Failure) { Stop-SubstrateOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }

  $response.Json | ConvertFrom-Json
}

function Invoke-SubstrateOfficeRecovery {
  [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '', Justification = 'Uses the public command PSCmdlet for confirmation before reopening evidence and executing.')]
  [CmdletBinding()]
  param ([object]$Recovery, [string]$OriginalAction, [System.Management.Automation.PSCmdlet]$Caller,
    [string]$OdtPath, [bool]$DryRun, [bool]$ForceCloseApps, [Security.SecureString]$ProductKey)

  Assert-SubstrateOfficeField $Recovery @('RunId', 'LogRoot', 'Path', 'Record') @('RunId', 'LogRoot')
  $metadata = Get-SubstrateOfficeExecutionContext | ConvertTo-Json -Depth 30 -Compress
  $response = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Recover($Recovery.RunId, $OriginalAction, $OdtPath, $Recovery.LogRoot, $ForceCloseApps, $ProductKey, $true, $metadata)

  if ($response.Failure) { Stop-SubstrateOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }

  $result = $response.Json | ConvertFrom-Json

  foreach ($warning in $result.Plan.Warnings) { Write-Warning $warning }

  if ($result.Status -ne 'Preview' -or $DryRun) { return $result }

  $plan = $result.Plan
  $description = "Resume $OriginalAction; remove [$($plan.RemoveProductId -join ',')]; ALL supported MSI: $($plan.RemoveMsi); force-close apps: $ForceCloseApps; target $($plan.Configuration.TargetProductId) $($plan.Configuration.Version); languages [$($plan.Configuration.Language -join ',')]"

  if (-not $Caller.ShouldProcess($plan.MachineId, $description)) { return $result }

  # The native manager reopens and validates the journal; caller-supplied Record is never authority.
  $response = [AdNoctem.Substrate.PowerShell.Office.OfficeCompatibility]::Recover($Recovery.RunId, $OriginalAction, $OdtPath, $Recovery.LogRoot, $ForceCloseApps, $ProductKey, $false, $metadata)

  if ($response.Failure) { Stop-SubstrateOfficeOperation $response.Failure.ReasonCode $response.Failure.Detail $response.Failure.Diagnostic }

  $response.Json | ConvertFrom-Json
}

function Install-Office {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Uninstall-Office {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Switch-OfficeDeployment {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Update-Office {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Set-OfficeUpdateConfiguration {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Add-OfficeLanguage {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Remove-OfficeLanguage {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Set-OfficeApplicationSelection {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Set-OfficeApplicationPreference {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeWorkflow @parameters
  }
}

function Stop-SubstrateOfficeOperation {
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

function Get-SubstrateOfficeMediaDiagnostic {
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

function Assert-SubstrateOfficeField {
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
    Stop-SubstrateOfficeOperation InvalidContract 'An Office contract must be a data object.'
  }

  if ($InputObject -is [Collections.IDictionary]) {
    $names = @($InputObject.Keys)
  }
  else {
    $names = @($InputObject.PSObject.Properties | ForEach-Object { $_.Name })
  }

  foreach ($name in $names) {
    if ($name -notin $Allowed) {
      Stop-SubstrateOfficeOperation InvalidContract "Unexpected Office contract field: $name."
    }
  }

  foreach ($name in $Required) {
    if ($name -notin $names) {
      Stop-SubstrateOfficeOperation InvalidContract "Missing Office contract field: $name."
    }
  }
}

function ConvertTo-SubstrateOfficeList {
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
      Stop-SubstrateOfficeOperation InvalidConfiguration 'Empty identifiers are not allowed.'
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
        Stop-SubstrateOfficeOperation Unsupported "Language '$normalized' is outside the supported full-UI language catalog."
      }
    }
    elseif ($normalized -notmatch '^[A-Za-z0-9]+$') {
      Stop-SubstrateOfficeOperation InvalidConfiguration 'Product/application identifiers must be alphanumeric.'
    }

    if ($seen.Add($normalized)) {
      $normalized
    }
  }
}

function Get-SubstrateOfficeFingerprint {
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

function ConvertTo-SubstrateOfficeConfiguration {
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
  Assert-SubstrateOfficeField $Configuration $fields $fields

  if ($Configuration.SchemaVersion -ne 1) {
    Stop-SubstrateOfficeOperation InvalidContract 'Unsupported configuration schema.'
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
    Stop-SubstrateOfficeOperation InvalidContract 'PrimaryLanguage must equal the first ordered language.'
  }

  if ($Configuration.LocaleSource -notin @('Default', 'Explicit', 'InstalledOffice', 'OperatingSystem', 'Recovery')) {
    Stop-SubstrateOfficeOperation InvalidContract 'Invalid locale source.'
  }

  $normalized.RequestedLanguages = @($Configuration.RequestedLanguages | ForEach-Object { [string]$_ })
  $normalized.LocaleSource = [string]$Configuration.LocaleSource
  $normalized.LocaleEvidence = @($Configuration.LocaleEvidence | ForEach-Object { [string]$_ })
  $normalized
}

function Assert-SubstrateOfficeSetting {
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

  Assert-SubstrateOfficeField $Settings $allowed

  if ($Settings -is [Collections.IDictionary]) {
    $names = @($Settings.Keys)
  }
  else {
    $names = @($Settings.PSObject.Properties | ForEach-Object { $_.Name })
  }

  if ($Action -eq 'SetUpdateConfiguration') {
    if (-not $names.Count) {
      Stop-SubstrateOfficeOperation InvalidConfiguration 'Specify at least one update setting.'
    }

    if ('Enabled' -in $names -and $Settings.Enabled -isnot [bool]) {
      Stop-SubstrateOfficeOperation InvalidConfiguration 'Enabled must be a Boolean.'
    }

    if ('UpdatePath' -in $names -and [string]$Settings.UpdatePath -notmatch '^(https://|[A-Za-z]:\\|\\\\)') {
      Stop-SubstrateOfficeOperation InvalidConfiguration 'UpdatePath must be HTTPS or an absolute local/UNC path.'
    }

    if ('TargetVersion' -in $names -and [string]$Settings.TargetVersion -notmatch '^16\.0\.\d+\.\d+$') {
      Stop-SubstrateOfficeOperation InvalidConfiguration 'TargetVersion must be an exact Office build.'
    }

    if ('Channel' -in $names -and $Settings.Channel -notin @(
        'Current',
        'MonthlyEnterprise',
        'SemiAnnual',
        'PerpetualVL2019',
        'PerpetualVL2021',
        'PerpetualVL2024'
      )) {
      Stop-SubstrateOfficeOperation InvalidConfiguration 'Unsupported update channel.'
    }
  }
  elseif ($Action -eq 'SetApplicationPreference') {
    if ('Preferences' -notin $names -or -not @($Settings.Preferences).Count) {
      Stop-SubstrateOfficeOperation InvalidConfiguration 'At least one application preference is required.'
    }

    foreach ($preference in $Settings.Preferences) {
      Assert-SubstrateOfficeField $preference @('Key', 'Name', 'Value', 'Type', 'App', 'Id') @('Key', 'Name', 'Value', 'Type', 'App', 'Id')

      if ($preference.Key -notmatch '^software\\microsoft\\office\\16\.0\\(word|excel|powerpoint|outlook|access|onenote)(\\[a-z0-9 _-]+)+$' -or
        $preference.Type -notin @('REG_DWORD', 'REG_SZ') -or
        $preference.App -notin @('word16', 'excel16', 'ppt16', 'outlook16', 'access16', 'onenote16') -or
        $preference.Name -notmatch '^[a-zA-Z0-9 _-]+$' -or $preference.Id -notmatch '^[a-zA-Z0-9_-]+$') {
        Stop-SubstrateOfficeOperation InvalidConfiguration 'Application preference is outside the supported Office preference schema.'
      }

      if ($preference.Value -isnot [string] -and $preference.Value -isnot [int] -and $preference.Value -isnot [long]) {
        Stop-SubstrateOfficeOperation InvalidConfiguration 'Preference values must be strings or integers.'
      }

      if ($preference.Type -eq 'REG_DWORD' -and [string]$preference.Value -notmatch '^\d{1,10}$') {
        Stop-SubstrateOfficeOperation InvalidConfiguration 'REG_DWORD requires an unsigned decimal value.'
      }

      if ($preference.Type -eq 'REG_DWORD' -and [long]$preference.Value -gt [uint32]::MaxValue) {
        Stop-SubstrateOfficeOperation InvalidConfiguration 'REG_DWORD exceeds the unsigned 32-bit range.'
      }
    }
  }
}

function Get-SubstrateOfficeExecutionContext {
  [CmdletBinding()]
  param ()

  $module = $ExecutionContext.SessionState.Module
  [PSCustomObject]@{
    ModuleVersion     = if ($module -and $module.Name -eq 'AdNoctem.Substrate.PowerShell') { [string]$module.Version } else { $null }
    ModulePath        = if ($module -and $module.Name -eq 'AdNoctem.Substrate.PowerShell') { $module.Path } else { $PSCommandPath }
    PowerShellVersion = [string]$PSVersionTable.PSVersion
    PowerShellEdition = [string]$PSVersionTable.PSEdition
    ProcessBitness    = [IntPtr]::Size * 8
  }
}



function Resume-OfficeInstallation {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeRecovery @parameters
  }
}

function Resume-OfficeMigration {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
    Invoke-SubstrateOfficeRecovery @parameters
  }
}

function Convert-Quote {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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
  $converted = [AdNoctem.Substrate.Core.TextConverter]::ConvertQuotes([string]$content, [AdNoctem.Substrate.Core.QuoteStyle]$To)
  Set-Content -Path $Path -Value $converted

}

function New-DriveMapping {
  <#
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  $manager = [AdNoctem.Substrate.Windows.DriveMappingManager]::new()
  $directory = [AdNoctem.Substrate.IO.FileSystemPath]::Parse([IO.Path]::GetFullPath($Path))
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
  .EXTERNALHELP AdNoctem.Substrate.PowerShell-help.xml
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

  $manager = [AdNoctem.Substrate.Windows.DriveMappingManager]::new()
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
