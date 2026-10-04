#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$executable = Join-Path $root 'build/bin/ProcessHost/Release/net48/ProcessHost.exe'
$scratch = Join-Path $root 'build/test-results/foundations/scratch'
$null = [IO.Directory]::CreateDirectory($scratch)
$observations = [ordered]@{}

function Add-Observation {
  param ([string]$Name, [scriptblock]$Action)
  $records = New-Object 'Collections.Generic.List[object]'
  $terminated = $false
  try {
    & $Action *>&1 | ForEach-Object {
      if ($_ -is [Management.Automation.InformationRecord]) {
        $records.Add([ordered]@{ Stream = 'Information'; Text = [string]$_.MessageData; Tags = @($_.Tags); Color = [string]$_.MessageData.ForegroundColor; NoNewLine = $_.MessageData.NoNewLine })
      }
      elseif ($_ -is [Management.Automation.ErrorRecord]) { $records.Add((Convert-Error $_)) }
      else { $records.Add([ordered]@{ Type = $_.GetType().FullName; Value = $_ }) }
    }
  }
  catch { $terminated = $true; $records.Add((Convert-Error $_)) }
  $observations[$Name] = [ordered]@{ Terminated = $terminated; Records = @($records.ToArray()) }
}

function Convert-Error {
  param ([Management.Automation.ErrorRecord]$Record)
  [ordered]@{ Stream = 'Error'; Message = $Record.Exception.Message; Exception = $Record.Exception.GetType().FullName; Category = [string]$Record.CategoryInfo.Category; Id = ($Record.FullyQualifiedErrorId -split ',')[0]; Target = $Record.TargetObject }
}

Add-Observation 'result-empty' { New-OperationResult }
Add-Observation 'result-null-strings' { New-OperationResult -Target $null -Source $null -Action $null -Status $null -Scope $null -Detail $null -SkippedReason $null -ErrorMessage $null -RunId $null }
Add-Observation 'result-explicit' { New-OperationResult -Target $null -Source '' -Action Read -Status Completed -Before $null -After @() -Changed:$false -Property @{ TARGET = 'ignored'; Extra = 5 } }
Add-Observation 'result-append' {
  $list = New-Object Collections.ArrayList
  Add-OperationResult -Results $list
  Add-OperationResult -Results $list -Target 'value' -After $null -Changed:$true -PassThru
  $list | ForEach-Object { $_ }
}
Add-Observation 'operation-log-empty' { Write-OperationResultLog -Results @() -Path (Join-Path $scratch 'empty.jsonl') }
Add-Observation 'operation-log-json' {
  $items = @([PSCustomObject]@{ Timestamp = 'fixed'; Script = 'override'; Message = 'quoted"'; Nested = [PSCustomObject]@{ Values = @(1, 2); Date = [datetime]'2026-01-01'; Kind = [ConsoleColor]::Red } }, [PSCustomObject]@{ Timestamp = 'fixed'; RunId = 'existing' })
  $path = Write-OperationResultLog -Results $items -ScriptName Synthetic -RunId 'new' -Path (Join-Path $scratch 'logs/result.jsonl')
  [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
}
Add-Observation 'operation-log-invalid-directory' { Write-OperationResultLog -Results @([PSCustomObject]@{}) -Path $scratch }
Add-Observation 'operation-log-invalid-provider' { Write-OperationResultLog -Results @([PSCustomObject]@{}) -Path 'HKCU:\Software' }
Add-Observation 'operation-log-string-enumeration' {
  $path = Write-OperationResultLog -Results 'text' -ScriptName Synthetic -Path (Join-Path $scratch 'scalar.jsonl')
  $entries = @(Get-Content -LiteralPath $path | ForEach-Object { $_ | ConvertFrom-Json })
  foreach ($entry in $entries) { $entry.Timestamp = 'fixed' }
  $entries
}
Add-Observation 'result-fixed-size-stop' { Add-OperationResult -Results ([object[]]@()) -Target 'value' -PassThru }
Add-Observation 'result-fixed-size-continue' { Add-OperationResult -Results ([object[]]@()) -Target 'value' -PassThru -ErrorAction Continue }
Add-Observation 'log-default' { Write-Log 'hello' }
Add-Observation 'log-colors' { Write-Log 'first' -Color DarkGreen; Write-Log 'second' -Color Red }
Add-Observation 'log-silenced' { Write-Log 'hidden' -InformationAction Ignore }
foreach ($entry in @(@('Msi', 0), @('Msi', 1602), @('Msi', 1603), @('Msi', 1618), @('Msi', 1638), @('Msi', 1641), @('Msi', 3010), @('Msi', 42), @('Appx', '0x80073D06'), @('Winget', '0x8A150011'), @('Dism', '0x800F081F'), @('appx', '-2147009274'))) {
  Add-Observation "translate:$($entry[0]):$($entry[1])" { Get-ErrorTranslation -Code $entry[1] -Domain $entry[0] }
}
foreach ($code in @('invalid', '4294967296', '-2147483649', '0x123456789', '+3010')) {
  Add-Observation "invalid:$code" { Get-ErrorTranslation -Code $code -Domain Msi }
}
foreach ($text in @('0x80073D06', '0x80073D06 and 0x80073CF0', '0x80073D06 twice 0x80073D06', '3010 records', 'unknown')) {
  Add-Observation "message:$text" {
    $record = New-Object Management.Automation.ErrorRecord ([Exception]::new($text)), 'Synthetic', 'NotSpecified', $null
    $record | Get-ErrorTranslation
  }
}
Add-Observation 'structured-priority' {
  $exception = New-Object Runtime.InteropServices.COMException '0x80073CF0', ([int]-2147009274)
  $record = New-Object Management.Automation.ErrorRecord $exception, 'Synthetic', 'NotSpecified', $null
  Get-ErrorTranslation -ErrorRecord $record
}
Add-Observation 'path-literal' {
  $path = Join-Path $scratch 'literal[1].txt'
  [IO.File]::WriteAllText($path, 'test')
  Resolve-LongPath -LiteralPath $path
  Push-Location $scratch
  try { Resolve-LongPath -LiteralPath 'literal[1].txt' }
  finally { Pop-Location }
}
Add-Observation 'path-missing' { Resolve-LongPath -LiteralPath (Join-Path $scratch 'missing') }
Add-Observation 'path-provider' { Resolve-LongPath -LiteralPath 'HKCU:\Software' }
Add-Observation 'path-missing-provider' { Resolve-LongPath -LiteralPath 'HKCU:\PSFoundation-Synthetic-Missing' }
Add-Observation 'process-arguments' { Invoke-SafeProcess $executable -ArgumentList @('arguments', '', 'space value', 'a"b', 'C:\space end\', 'a&b|c') -PassThru }
Add-Observation 'process-result' {
  $result = Invoke-SafeProcess $executable -ArgumentList @('arguments', 'value') -AsResult
  if ($result.Duration -isnot [timespan] -or $result.Duration.TotalSeconds -lt 0) { throw 'Missing process duration.' }
  $result.Duration = [timespan]::Zero
  $result
}
Add-Observation 'process-file' {
  Push-Location $scratch
  try {
    Invoke-SafeProcess $executable -ArgumentList @('arguments', 'test') -OutputPath 'result.txt'
    [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $scratch 'result.txt')))
  }
  finally { Pop-Location }
}
Add-Observation 'process-error-result' { Invoke-SafeProcess -FilePath 'PSFoundation.synthetic.missing.exe' -AsResult }
Add-Observation 'process-error-text' { Invoke-SafeProcess -FilePath 'PSFoundation.synthetic.missing.exe' -PassThru }
Add-Observation 'process-precancelled' { Invoke-SafeProcess $executable -AsResult -CancellationToken ([Threading.CancellationToken]::new($true)) }
foreach ($address in @('192.0.2.1', '127.1', '0.0.0.0', '255.255.255.255', '256.2.3.4', '+1.2.3.4', ' 1.2.3.4', '01.2.3.4', '-0.2.3.4', '::', '::1', '2001:db8::1', '2001:::1', '::ffff:192.0.2.1', '1:2:3:4:5:6:192.0.2.1', '1:2:3:4:5:6:7:8', 'fe80::1%3', '1::2::3', '[::1]')) {
  Add-Observation "address:$address" { Test-IPv4Address $address; Test-IPv6Address $address }
}
[IO.File]::WriteAllText($ReportPath, ($observations | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false)))
