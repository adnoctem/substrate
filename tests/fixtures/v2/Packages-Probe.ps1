#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath, [string]$FixtureId)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$root = "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AdNoctem.Substrate.Tests.$FixtureId"
$paths = @($root, "$root-hidden", "$root-blank", "$root-stringflag")
$observations = [ordered]@{}
$createdPaths = @()
try {
  foreach ($path in $paths) {
    $existing = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($path)
    if ($existing) { $existing.Dispose(); throw 'Synthetic fixture already exists.' }
  }
  for ($index = 0; $index -lt $paths.Count; $index++) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($paths[$index])
    $createdPaths += $paths[$index]
    try {
      $displayName = if ($index -eq 2) { ' ' } else { "AdNoctem.Substrate.PowerShell Synthetic $FixtureId $index" }
      $key.SetValue('DisplayName', $displayName)
      $key.SetValue('Publisher', 'Synthetic Publisher')
      $key.SetValue('DisplayVersion', '1.2.3')
      $key.SetValue('InstallDate', '20260228')
      $key.SetValue('InstallLocation', '%TEMP%\AdNoctem.Substrate.PowerShell Synthetic', [Microsoft.Win32.RegistryValueKind]::ExpandString)
      $key.SetValue('UninstallString', '"C:\Synthetic Only\NeverRun.exe" /uninstall')
      $key.SetValue('QuietUninstallString', '"C:\Synthetic Only\NeverRun.exe" /quiet')
      $key.SetValue('EstimatedSize', -1, [Microsoft.Win32.RegistryValueKind]::DWord)
      if ($index -eq 1) { $key.SetValue('SystemComponent', 1, [Microsoft.Win32.RegistryValueKind]::DWord) }
      if ($index -eq 3) { $key.SetValue('SystemComponent', '0', [Microsoft.Win32.RegistryValueKind]::String) }
    }
    finally { $key.Dispose() }
  }
  $pattern = "*Synthetic $FixtureId*"
  $observations['visible'] = @(Get-Win32Program -Name $pattern)
  $observations['all'] = @(Get-Win32Program -Name $pattern -IncludeSystemComponent)
  $observations['find-name'] = @(Find-Win32Program -Name $pattern)
  $observations['find-all'] = @(Find-Win32Program -Name $pattern -IncludeSystemComponent)
  $observations['none'] = @(Get-Win32Program -Name "$FixtureId-no-match")
  $observations['exact-hidden'] = Find-Win32Program -RegistryPath "HKCU:\$root-hidden"
  $observations['exact-long'] = Find-Win32Program -RegistryPath "Registry::HKEY_CURRENT_USER\$root"
  $observations['exact-missing'] = @(Find-Win32Program -RegistryPath "HKCU:\$root-missing")
  $observations['exact-blank'] = @(Find-Win32Program -RegistryPath "HKCU:\$root-blank")
  $observations['exact-invalid'] = @(Find-Win32Program -RegistryPath 'INVALID\key')
  $observations['size-type'] = (Find-Win32Program -RegistryPath "HKCU:\$root").EstimatedSize.GetType().FullName
  $observations['count'] = Get-InstalledProgramCount
  if ($observations['visible'].Count -ne 1 -or $observations['all'].Count -ne 3) { throw 'Synthetic inventory filtering differs.' }
  if ($observations['count'] -ne @(Get-Win32Program).Count) { throw 'Installed program count differs from inventory.' }
  $fixtureExe = Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) 'build/bin/ProcessHost/Release/net48/ProcessHost.exe'
  $observations['install-preview'] = Install-Win32Program -Path $fixtureExe -WhatIf -RunId 'synthetic'
  $observations['install-dry-run'] = Install-Win32Program -Path $fixtureExe -DryRun
  $installed = Install-Win32Program -Path $fixtureExe -ArgumentList 'short-wait' -PassThru -RunId 'synthetic'
  if ($installed.Status -ne 'ExitCode:0' -or -not $installed.Succeeded -or $installed.Duration.TotalSeconds -lt 0) { throw 'Synthetic installer failed.' }
  $observations['install-result'] = $installed | Select-Object * -ExcludeProperty Duration
  $synthetic = [PSCustomObject]@{ DisplayName = 'Synthetic execution'; UninstallString = ('"{0}" short-wait' -f $fixtureExe); QuietUninstallString = '' }
  $observations['uninstall-preview'] = $synthetic | Uninstall-Win32Program -Force -WhatIf
  $observations['uninstall-gated'] = $synthetic | Uninstall-Win32Program -Confirm:$false
  $observations['uninstall-result'] = $synthetic | Uninstall-Win32Program -Force -Confirm:$false
  if ((Get-Command Uninstall-Win32Program).CommandType -eq 'Cmdlet') {
    $fallback = $synthetic | Uninstall-Win32Program -Quiet -Confirm:$false
    if ($fallback.SkippedReason -ne 'ForceRequired') { throw 'Quiet selection authorized an interactive fallback.' }
  }
}
finally {
  foreach ($path in $createdPaths) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($path, $false) }
}
[IO.File]::WriteAllText($ReportPath, ($observations | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
Write-Output "Package compatibility observations: $($observations.Count)"
