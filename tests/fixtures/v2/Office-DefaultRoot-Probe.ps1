#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('substrate-office-default-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($root)
$originalProgramData = $env:ProgramData
$env:ProgramData = $root

try {
  $null = Import-Module $ModulePath -Force
  $observations = [ordered]@{}

  try { $null = Get-OfficeDeploymentRecovery -RunId '0123456789abcdef0123456789abcdef' }
  catch {
    $observations.Message = $_.Exception.Message
    $observations.Reason = $_.Exception.Data['OfficeReason']
    $diagnostic = $_.Exception.Data['OfficeDiagnostic']
    $observations.DiagnosticPath = if ($diagnostic) { $diagnostic.Path } else { '' }
  }

  $observations | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
}
finally {
  $env:ProgramData = $originalProgramData
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
