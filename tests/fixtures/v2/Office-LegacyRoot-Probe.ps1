#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('substrate-legacy-root-' + [Guid]::NewGuid().ToString('N'))
$legacy = Join-Path $root 'PSFoundation-Office'
$current = Join-Path $root 'winkit-Office'
$runId = '0123456789abcdef0123456789abcdef'
$null = [IO.Directory]::CreateDirectory($legacy)
$env:ProgramData = $root

try {
  $null = Import-Module $ModulePath -Force
  $observations = [ordered]@{}

  try { $null = Get-OfficeDeploymentRecovery -RunId $runId }
  catch {
    $observations.LegacyMessage = $_.Exception.Message
    $observations.LegacyReason = $_.Exception.Data['OfficeReason']
  }

  try { $null = Get-OfficeDeploymentRecovery -RunId $runId -LogRoot $legacy }
  catch {
    $observations.ExplicitLegacyMessage = $_.Exception.Message
    $observations.ExplicitLegacyReason = $_.Exception.Data['OfficeReason']
  }

  $null = [IO.Directory]::CreateDirectory($current)

  try { $null = Get-OfficeDeploymentRecovery -RunId $runId }
  catch {
    $observations.CurrentRootMessage = $_.Exception.Message
    $observations.CurrentRootReason = $_.Exception.Data['OfficeReason']
  }

  $observations | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
}
finally {
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
