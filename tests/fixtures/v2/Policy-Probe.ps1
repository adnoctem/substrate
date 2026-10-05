#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$scratch = Join-Path $root 'build/test-results/policies/scratch'
$null = [IO.Directory]::CreateDirectory($scratch)
$path = Join-Path $scratch 'registry.pol'
$observations = [ordered]@{}

function Convert-Error {
  param ([Management.Automation.ErrorRecord]$Record)
  [ordered]@{ Stream = 'Error'; Message = $Record.Exception.Message; Exception = $Record.Exception.GetType().FullName; Category = [string]$Record.CategoryInfo.Category; Id = ($Record.FullyQualifiedErrorId -split ',')[0]; Target = $Record.TargetObject }
}

function Add-Observation {
  param ([string]$Name, [scriptblock]$Action)
  [IO.File]::WriteAllText($ReportPath + '.progress', $Name)
  $records = New-Object 'Collections.Generic.List[object]'
  $terminated = $false
  try {
    & $Action *>&1 | ForEach-Object {
      if ($_ -is [Management.Automation.ErrorRecord]) { $records.Add((Convert-Error $_)) }
      elseif ($_ -is [Management.Automation.VerboseRecord]) { $records.Add([ordered]@{ Stream = 'Verbose'; Message = $_.Message }) }
      else {
        $dataType = if ($_.PSObject.Properties['Data'] -and $null -ne $_.Data) { $_.Data.GetType().FullName } else { $null }
        $records.Add([ordered]@{ Type = $_.GetType().FullName; Names = @($_.PSObject.TypeNames); DataType = $dataType; Value = $_ })
      }
    }
  }
  catch { $terminated = $true; $records.Add((Convert-Error $_)) }
  $observations[$Name] = [ordered]@{ Terminated = $terminated; Records = @($records.ToArray()) }
}

$entries = @(
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = ''; Type = 1; Data = '' }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Text'; Type = 1; Data = 'Grüße 日本語' }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Expand'; Type = 2; Data = '%TEMP%\literal' }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Bytes'; Type = 3; Data = [byte[]]@(0, 255, 59, 93) }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Dword'; Type = 4; Data = [uint32]::MaxValue }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Big'; Type = 5; Data = [uint32]305419896 }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Multi'; Type = 7; Data = [string[]]@('one', 'two') }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Multi'; Type = 7; Data = [string[]]@() }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Qword'; Type = 11; Data = [uint64]::MaxValue }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = 'Opaque'; Type = 42; Data = [byte[]]@(1, 2) }
  [PSCustomObject]@{ Key = 'Software\Synthetic'; ValueName = '**Del.Flag'; Type = 1; Data = $null }
)
Add-Observation 'write-read-decoded' {
  $entries | ConvertTo-RegistryPolicy -Path $path -Force
  [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
  ConvertFrom-RegistryPolicy $path
}
$valid = [IO.File]::ReadAllBytes($path)
Add-Observation 'raw-roundtrip' {
  $raw = @(ConvertFrom-RegistryPolicy $path -Raw)
  $raw
  $raw | ConvertTo-RegistryPolicy -Path $path -Force
  [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
}
Add-Observation 'empty' { ConvertTo-RegistryPolicy -InputObject @() -Path $path -Force; ConvertFrom-RegistryPolicy $path; [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) }
Add-Observation 'empty-pipeline' { @() | ConvertTo-RegistryPolicy -Path $path -Force; [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) }
Add-Observation 'hashtable' { ConvertTo-RegistryPolicy @{ Key = 'K'; ValueName = ''; Type = 4; Data = 1 } $path -Force; ConvertFrom-RegistryPolicy $path }
Add-Observation 'existing' { ConvertTo-RegistryPolicy -Path $path }
Add-Observation 'missing' { ConvertFrom-RegistryPolicy (Join-Path $scratch 'missing.pol') }
Add-Observation 'directory-read' { ConvertFrom-RegistryPolicy $scratch }
Add-Observation 'whatif' {
  $before = [IO.File]::ReadAllBytes($path)
  $entries | ConvertTo-RegistryPolicy -Path $path -Force -WhatIf
  [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -eq [Convert]::ToBase64String($before)
}
foreach ($bad in @(
    @{ Name = 'missing-field'; Entry = @{ Key = 'K' } }
    @{ Name = 'null-entry'; Entry = $null }
    @{ Name = 'empty-key'; Entry = @{ Key = ''; ValueName = 'V'; Type = 1; Data = '' } }
    @{ Name = 'rooted-key'; Entry = @{ Key = 'HKLM\Software'; ValueName = 'V'; Type = 1; Data = '' } }
    @{ Name = 'null-name'; Entry = @{ Key = 'K'; ValueName = $null; Type = 1; Data = '' } }
    @{ Name = 'negative-type'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = -1; Data = '' } }
    @{ Name = 'overflow-type'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = '4294967296'; Data = '' } }
    @{ Name = 'embedded-null'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 1; Data = "a`0b" } }
    @{ Name = 'empty-multi-item'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 7; Data = @('') } }
    @{ Name = 'negative-integer'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 4; Data = -1 } }
    @{ Name = 'overflow-integer'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 4; Data = '4294967296' } }
    @{ Name = 'overflow-large-integer'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 11; Data = '18446744073709551616' } }
    @{ Name = 'overflow-huge-dword'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 4; Data = '18446744073709551616' } }
    @{ Name = 'non-ascii-integer'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 4; Data = '١' } }
    @{ Name = 'opaque-string'; Entry = @{ Key = 'K'; ValueName = 'V'; Type = 42; Data = 'a' } }
  )) {
  Add-Observation $bad.Name { ConvertTo-RegistryPolicy -InputObject @($entries[0], $bad.Entry) -Path $path -Force }
}
Add-Observation 'invalid-preserves-destination' {
  $before = [IO.File]::ReadAllBytes($path)
  try { ConvertTo-RegistryPolicy @{ Key = 'K' } $path -Force } catch { $null = $_ }
  [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -eq [Convert]::ToBase64String($before)
}
Add-Observation 'locked-destination' {
  $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
  try { $entries | ConvertTo-RegistryPolicy -Path $path -Force }
  finally { $stream.Dispose() }
}
Add-Observation 'staging-cleanup' { @(Get-ChildItem -LiteralPath $scratch -File).Count }
$header = [byte[]]@(80, 82, 101, 103, 1, 0, 0, 0)
foreach ($bad in @(
    @{ Name = 'empty-file'; Bytes = [byte[]]@() }
    @{ Name = 'short-signature'; Bytes = [byte[]]@(80) }
    @{ Name = 'bad-signature'; Bytes = [byte[]]@(0, 0, 0, 0, 1, 0, 0, 0) }
    @{ Name = 'bad-version'; Bytes = [byte[]]@(80, 82, 101, 103, 2, 0, 0, 0) }
    @{ Name = 'truncated-record'; Bytes = [byte[]]($header + @(91, 0, 65)) }
    @{ Name = 'bad-delimiter'; Bytes = [byte[]]($header + @(93, 0)) }
    @{ Name = 'truncated-payload'; Bytes = [byte[]]$valid[0..($valid.Length - 4)] }
  )) {
  Add-Observation $bad.Name { [IO.File]::WriteAllBytes($path, $bad.Bytes); ConvertFrom-RegistryPolicy $path }
}
foreach ($bytes in @([byte[]]@(65), [byte[]]@(65, 0), [byte[]]@(0, 216, 0, 0))) {
  $raw = [PSCustomObject]@{ Key = 'K'; ValueName = 'V'; Type = 1; Data = $bytes }
  $raw.PSObject.TypeNames.Insert(0, 'PSFoundation.RegistryPolicy.RawEntry')
  ConvertTo-RegistryPolicy $raw $path -Force
  Add-Observation "malformed-string:$([Convert]::ToBase64String($bytes))" { ConvertFrom-RegistryPolicy $path }
  Add-Observation "raw-string:$([Convert]::ToBase64String($bytes))" { ConvertFrom-RegistryPolicy $path -Raw }
}
Add-Observation 'malformed-short-multi' {
  $raw = [PSCustomObject]@{ Key = 'K'; ValueName = 'V'; Type = 7; Data = [byte[]]@(65, 0) }
  $raw.PSObject.TypeNames.Insert(0, 'adnoctem.substrate.registrypolicy.rawentry')
  ConvertTo-RegistryPolicy $raw $path -Force
  ConvertFrom-RegistryPolicy $path
}
Add-Observation 'lgpo-source' {
  foreach ($source in @((Resolve-LGPOSource), (Resolve-LGPOSource -Source 'sct-lgpo-standalone'))) {
    if ((Get-Command Resolve-LGPOSource).CommandType -eq 'Cmdlet') {
      if ($source.Sha256 -cne 'CB7159D134A0A1E7B1ED2ADA9A3CE8CE8F4DE391D14403D55438AF824247CC55' -or $source.LastVerified -cne '2026-10-04') {
        throw 'LGPO source must expose the reviewed ZIP pin and verification date.'
      }
    }
    # The reviewed source pin/date intentionally replace the frozen placeholder. Compare all other fields and types normally.
    $source.Sha256 = '<reviewed-source-pin>'
    $source.LastVerified = '<reviewed-source-date>'
    $source
  }
}
Add-Observation 'lgpo-installed' {
  Test-LGPOInstalled -Path $path
  Test-LGPOInstalled -Path $scratch
  Test-LGPOInstalled -Path (Join-Path $scratch 'missing.exe')
  Test-LGPOInstalled -Path ''
  Test-LGPOInstalled -Path 'UnknownDrive:\missing.exe'
}
Add-Observation 'lgpo-provider-paths' { Test-LGPOInstalled -Path 'Env:PATH'; Test-LGPOInstalled -Path 'UnknownDrive:\missing.exe' }
$executable = Join-Path $root 'build/bin/ProcessHost/Release/net48/ProcessHost.exe'
$policyInput = Join-Path $scratch 'lgpo.txt'
[IO.File]::WriteAllText($policyInput, 'ok')
Add-Observation 'lgpo-whatif' { Invoke-LGPO -PolicyPath $policyInput -LgpoExe $executable -WhatIf }
Add-Observation 'lgpo-missing-executable' { Invoke-LGPO -PolicyPath $policyInput -LgpoExe (Join-Path $scratch 'missing.exe') }
Add-Observation 'lgpo-invalid-policy' { Invoke-LGPO -PolicyPath (Join-Path $scratch 'missing.txt') -LgpoExe $executable }
Add-Observation 'lgpo-invalid-executable' { Invoke-LGPO -PolicyPath $policyInput -LgpoExe $policyInput }
foreach ($mode in @('text', 'backup', 'failure')) {
  if ($mode -eq 'failure') { [IO.File]::WriteAllText($policyInput, 'fail') }
  $inputPath = if ($mode -eq 'backup') { $scratch } else { $policyInput }
  Add-Observation "lgpo-apply:$mode" {
    $result = Invoke-LGPO -PolicyPath $inputPath -LgpoExe $executable
    # Get-Content in v1 attaches provider objects to strings. Capture text separately to avoid serializing provider object graphs.
    if ($null -ne $result.StdOut) { $result.StdOut = [string]::Concat('', [string]$result.StdOut) }
    if ($null -ne $result.StdErr) { $result.StdErr = [string]::Concat('', [string]$result.StdErr) }
    if ($result.AppliedAt.Kind -ne [DateTimeKind]::Utc) { throw 'LGPO timestamp must be UTC.' }
    $result.AppliedAt = [datetime]'2026-01-01T00:00:00Z'
    $result
  }
}
Remove-Item -LiteralPath $policyInput
$installDirectory = Join-Path $scratch 'tool-install'
Add-Observation 'lgpo-install-whatif' { Install-LGPO -Destination $installDirectory -WhatIf; Test-Path -LiteralPath $installDirectory }
$null = [IO.Directory]::CreateDirectory($installDirectory)
[IO.File]::WriteAllText((Join-Path $installDirectory 'LGPO.exe'), 'synthetic existing executable; never run')
Add-Observation 'lgpo-install-existing' { Install-LGPO -Destination $installDirectory -Verbose }
Add-Observation 'lgpo-install-force-whatif' { Install-LGPO -Destination $installDirectory -Force -WhatIf -Verbose }
Remove-Item -LiteralPath (Join-Path $installDirectory 'LGPO.exe')
Remove-Item -LiteralPath $installDirectory
$observations | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Remove-Item -LiteralPath ($ReportPath + '.progress')
