#Requires -Version 5.1
param([string]$ModulePath)
$ErrorActionPreference = 'Stop'
Import-Module $ModulePath -Force
$root = Join-Path ([IO.Path]::GetTempPath()) ('psf-completion-' + [guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($root)
try {
  $exports = @(Get-Command -Module AdNoctem.Substrate.PowerShell)
  if ($exports.Count -ne 192) { throw 'Incomplete module exports.' }
  if (@(Get-ChildItem (Split-Path $ModulePath -Parent) -Filter *.ps1).Count -ne 1) { throw 'Legacy scripts remain staged.' }
  $report = Get-HostPrerequisiteReport -RequiredCommands 'Get-Item', 'PSF-SyntheticMissingCommand' -RequiredModules @{ 'Microsoft.PowerShell.Utility' = '1.0' }
  if ($report.Applicable -or $report.Checks.Count -ne 3 -or @($report.Checks | Where-Object Satisfied).Count -ne 2) { throw 'Prerequisite aggregation failed.' }
  $certificates = @(Get-CertificateInventory -StorePath 'Cert:\LocalMachine\Root')
  foreach ($certificate in $certificates) { $certificate.Dispose() }
  $apps = [AdNoctem.Substrate.Packages.AppxPackageManager]::new().GetInstalled()
  if ($null -eq $apps) { throw 'Native AppX inventory returned null.' }
  $password = [Security.SecureString]::new()
  foreach ($character in 'synthetic-ä-password'.ToCharArray()) { $password.AppendChar($character) }
  $password.MakeReadOnly()
  $key = [byte[]](1..32)
  $manager = [AdNoctem.Substrate.Security.CredentialFileManager]::new()
  $native = $manager.Encrypt($password, $key)
  $hostDecoded = ConvertTo-SecureString $native -Key $key
  $nativeDecoded = $manager.Decrypt((ConvertFrom-SecureString $password -Key $key), $key)
  try {
    if ([Net.NetworkCredential]::new('', $hostDecoded).Password -cne 'synthetic-ä-password' -or [Net.NetworkCredential]::new('', $nativeDecoded).Password -cne 'synthetic-ä-password') { throw 'Credential codec incompatibility.' }
  }
  finally { $password.Dispose(); $hostDecoded.Dispose(); $nativeDecoded.Dispose(); [Array]::Clear($key, 0, $key.Length) }
  foreach ($version in @('1.0.0', '2.0.0')) {
    $folder = Join-Path $root "Fixture/$version"
    $null = [IO.Directory]::CreateDirectory($folder)
    [IO.File]::WriteAllText((Join-Path $folder 'Fixture.psd1'), "@{ ModuleVersion = '$version'; GUID = 'd3c9bc36-314d-492a-a006-8e3a677a4194' }")
  }
  Remove-PSModule -Path $root -Name '^Fixture$' -LatestToKeep 1 -WhatIf
  if (-not (Test-Path (Join-Path $root 'Fixture/1.0.0'))) { throw 'WhatIf removed a module.' }
  Remove-PSModule -Path $root -Name '^Fixture$' -LatestToKeep 1 -Confirm:$false
  if ((Test-Path (Join-Path $root 'Fixture/1.0.0')) -or -not (Test-Path (Join-Path $root 'Fixture/2.0.0'))) { throw 'Module version cleanup failed.' }
  Add-PSModule -Name PSF-SyntheticMissingModule -WhatIf
  [PSCustomObject]@{ UpdateID = 'cb24c0c8-efb4-4f14-b982-e5cece678852' } | Install-WindowsUpdate -WhatIf
  [PSCustomObject]@{ UpdateID = 'cb24c0c8-efb4-4f14-b982-e5cece678852' } | Hide-WindowsUpdate -WhatIf
  Write-Output 'Completion workflows passed'
}
finally {
  # Only the uniquely created synthetic fixture root is removed.
  if ($root -and (Split-Path $root -Leaf) -like 'psf-completion-*') { Remove-Item -LiteralPath $root -Recurse -Force }
}
