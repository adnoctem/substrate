#Requires -Version 5.1
param (
  [string]$ModulePath,
  [string]$ReportPath,
  [ValidatePattern('^[a-fA-F0-9]{32}$')][string]$FixtureName
)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$subkey = 'Software\AdNoctem.Substrate.Tests\' + $FixtureName
$path = 'HKCU:\' + $subkey
$native = 'HKCU\' + $subkey
$scratch = Join-Path (Split-Path $ReportPath -Parent) $FixtureName
$scratch = [IO.Path]::GetFullPath($scratch)
$reportDirectory = [IO.Path]::GetFullPath((Split-Path $ReportPath -Parent)) + [IO.Path]::DirectorySeparatorChar

if (-not $scratch.StartsWith($reportDirectory, [StringComparison]::OrdinalIgnoreCase)) { throw 'Scratch must remain below report directory.' }

$null = [IO.Directory]::CreateDirectory($scratch)
$observations = [ordered]@{}

function Convert-Observation {
  param ([AllowNull()][object]$Value)

  if ($null -eq $Value) { return $null }

  if ($Value -is [Management.Automation.ErrorRecord]) {
    return [ordered]@{
      Stream = 'Error'; Message = $Value.Exception.Message; Exception = $Value.Exception.GetType().FullName
      Category = [string]$Value.CategoryInfo.Category; Id = ($Value.FullyQualifiedErrorId -split ',')[0]
      Target = $Value.TargetObject
    }
  }

  if ($Value -is [Management.Automation.VerboseRecord]) { return [ordered]@{ Stream = 'Verbose'; Message = $Value.Message } }

  if ($Value -is [Management.Automation.InformationRecord]) { return [ordered]@{ Stream = 'Information'; Message = [string]$Value.MessageData; Tags = @($Value.Tags) } }

  if ($Value -is [Array]) {
    return [ordered]@{ Type = $Value.GetType().FullName; Items = @($Value | ForEach-Object { Convert-Observation $_ }) }
  }

  if ($Value -is [System.Management.Automation.PSCustomObject]) {
    $properties = [ordered]@{}

    foreach ($property in $Value.PSObject.Properties) { $properties[$property.Name] = Convert-Observation $property.Value }

    return [ordered]@{ Type = 'PSCustomObject'; Properties = $properties }
  }

  return [ordered]@{ Type = $Value.GetType().FullName; Value = $Value }
}

function Add-Observation {
  param ([string]$CaseName, [scriptblock]$Action)

  $captured = New-Object 'Collections.Generic.List[object]'
  $terminated = $false

  try { & $Action *>&1 | ForEach-Object { $captured.Add((Convert-Observation $_)) } }
  catch { $terminated = $true; $captured.Add((Convert-Observation $_)) }

  $observations[$CaseName] = [ordered]@{ Terminated = $terminated; Output = @($captured.ToArray()) }
}

try {
  foreach ($inputPath in @('HKLM', 'hkey_current_user\Software\\', 'Registry::HKEY_USERS', 'HKCC:', 'HKCR\*\shell', 'HKCU:\Software::', 'NONSENSE\Path', '')) {
    Add-Observation "normalize:$inputPath" { ConvertTo-RegistryProviderPath -Path $inputPath -ErrorAction Continue }
  }

  Add-Observation 'invalid-stop' { ConvertTo-RegistryProviderPath 'NONSENSE' -ErrorAction Stop }
  Add-Observation 'root-create' { Set-RegistryKey HKCU: -ErrorAction Continue }
  Add-Observation 'root-delete' { Remove-RegistryKey HKCU: -ErrorAction Continue }
  Add-Observation 'missing-key' { Get-RegistryKey $path -ErrorAction Continue }
  Add-Observation 'missing-read' { Get-RegistryValue $path -ErrorAction Continue }
  Add-Observation 'missing-test' { Test-RegistryPath $path; Test-RegistryValue $path; Get-RegistryValueKind $path -Verbose }
  Add-Observation 'missing-remove' { Remove-RegistryKey $path; Remove-RegistryValue $path }
  Add-Observation 'whatif-parent' { Set-RegistryValue "$path\Preview" -Name X -Value 1 -WhatIf -ErrorAction Continue; Test-RegistryPath "$path\Preview" }
  Add-Observation 'create' { Set-RegistryKey $path -Verbose; Set-RegistryKey $path -Verbose }
  Add-Observation 'empty-key' { Get-RegistryKey $path; Get-RegistryValue $path -Name Missing -Verbose }
  Add-Observation 'default-value' { Set-RegistryValue $path -Value 'default' -Verbose; Get-RegistryValue $path; Test-RegistryValue $path; Get-RegistryValueKind $path }
  $values = [ordered]@{
    Text   = @{ Value = 'Text'; Type = 'String' }
    Expand = @{ Value = '%TEMP%\synthetic'; Type = 'ExpandString' }
    Number = @{ Value = -1; Type = 'DWord' }
    Large  = @{ Value = [long]4294967296; Type = 'QWord' }
    Bytes  = @{ Value = [byte[]]@(0, 255, 1); Type = 'Binary' }
    Multi  = @{ Value = [string[]]@('A', 'b'); Type = 'MultiString' }
    Single = @{ Value = [string[]]@('A'); Type = 'MultiString' }
    Empty  = @{ Value = [string[]]@(); Type = 'MultiString' }
    Null   = @{ Value = $null; Type = 'String' }
  }

  foreach ($name in $values.Keys) {
    $entry = $values[$name]
    Add-Observation "set:$name" { Set-RegistryValue $path -Name $name @entry -Verbose; Set-RegistryValue $path -Name $name @entry -Verbose }
    Add-Observation "get:$name" { Get-RegistryValue $path -Name $name; Get-RegistryValueKind $path -Name $name; Test-RegistryValue $path -Name $name }
  }

  Add-Observation 'case-insensitive-set' { Set-RegistryValue $path -Name Text -Value 'text'; Get-RegistryValue $path -Name Text }
  Add-Observation 'get-key' { Get-RegistryKey $path }
  Add-Observation 'binding-null-name' { Get-RegistryValue $path -Name $null; Test-RegistryValue $path -Name $null }
  Add-Observation 'pipeline-binding' { $path | Get-RegistryValue -ErrorAction Continue }
  Add-Observation 'unknown-type' { Set-RegistryValue $path -Name Unknown -Value 'auto' -Type Unknown; Get-RegistryValue $path -Name Unknown; Get-RegistryValueKind $path -Name Unknown }
  Add-Observation 'inferred-types' {
    Set-RegistryValue $path -Name Inferred -Value ([long]4)
    Get-RegistryValueKind $path -Name Inferred
    Set-RegistryValue $path -Name Inferred -Value ([long]4294967296) -ErrorAction Continue
  }
  Add-Observation 'whatif-values' { Set-RegistryValue $path -Name Text -Value 'MUTATED' -WhatIf; Get-RegistryValue $path -Name Text }
  Add-Observation 'wildcard-key' {
    Set-RegistryKey "$path\ChildB"
    Set-RegistryKey "$path\ChildA"
    Set-RegistryValue "$path\ChildA" -Name Value -Value 'child'
    Get-RegistryKey "$path\Child*" -ErrorAction Continue
    Test-RegistryPath "$path\Child*"
    Get-RegistryValue "$path\Child*" -Name Value -ErrorAction Continue
  }
  Add-Observation 'wildcard-value' { Get-RegistryValue $path -Name 'Te*' -ErrorAction Continue; Test-RegistryValue $path -Name 'Te*' }
  Add-Observation 'wildcard-write' {
    Set-RegistryKey "$path\Child*" -Verbose
    Set-RegistryValue "$path\Child*" -Name Value -Value 'updated' -Verbose
    Get-RegistryValue "$path\Child*" -Name Value
    Set-RegistryValue "$path\Child*" -Name Value -Value 'updated' -Verbose
    Set-RegistryValue "$path\ChildA" -Name 'Va*' -Value 'pattern' -Verbose
    Get-RegistryKey "$path\ChildA"
    Get-RegistryValue "$path\ChildA" -Name Value
  }
  Add-Observation 'whatif-invalid-data' { Set-RegistryValue $path -Name Inferred -Value ([long]4294967296) -WhatIf -ErrorAction Continue }
  Add-Observation 'wildcard-remove' {
    Remove-RegistryValue "$path\Child*" -Name 'Va*' -WhatIf -Verbose
    Remove-RegistryValue "$path\Child*" -Name 'Va*' -Verbose
    Get-RegistryKey "$path\ChildA"
    Remove-RegistryKey "$path\Child*" -Recurse -WhatIf -Verbose
    Remove-RegistryKey "$path\Child*" -Recurse -Verbose
  }
  Add-Observation 'handle' {
    $key = Resolve-RegistryPath $path -Writable -View Registry32

    try { $key.SetValue('Handle', 'owned'); $key.GetValue('Handle') }
    finally { $key.Dispose() }
  }
  $settings = @('', 'Text', 'Expand', 'Number', 'Large', 'Bytes', 'Multi', 'Missing') | ForEach-Object { [PSCustomObject][ordered]@{ Path = $path; Name = $_; Type = 'String'; Group = 'Synthetic' } }
  Add-Observation 'legacy-snapshot' { $settings | Export-RegistrySettingState }
  Add-Observation 'detailed-snapshot' { $settings | Export-RegistrySettingState -Detailed -View Registry32 }
  $before = @($settings | Export-RegistrySettingState -Detailed)
  $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($subkey, $true)

  try { foreach ($setting in $settings) { $key.SetValue($setting.Name, 'changed', [Microsoft.Win32.RegistryValueKind]::String) } }
  finally { $key.Dispose() }

  $expected = @($settings | Export-RegistrySettingState -Detailed)
  Add-Observation 'compare' { Compare-RegistrySettingState $before }
  Add-Observation 'restore-preview' { Restore-RegistrySettingState $before -ExpectedState $expected -WhatIf }
  Add-Observation 'restore-conflict' { Restore-RegistrySettingState $before -ExpectedState @() }
  Add-Observation 'restore-json' { Restore-RegistrySettingState ($before | ConvertTo-Json -Depth 9 | ConvertFrom-Json) -ExpectedState $expected -Confirm:$false }
  Add-Observation 'already-compliant' { Restore-RegistrySettingState $before -Confirm:$false; Compare-RegistrySettingState $before }
  Add-Observation 'invalid-version' { Restore-RegistrySettingState @{ Path = $path; Name = 'Text' } }
  Add-Observation 'invalid-existence' { Compare-RegistrySettingState @{ Path = $path; Name = 'Text'; Exists = 'false' } }
  Add-Observation 'invalid-null' { Compare-RegistrySettingState @{ Path = $path; Name = 'Text'; Type = 'String'; Preferred = $null } }
  Add-Observation 'invalid-settings' { Export-RegistrySettingState @([PSCustomObject]@{ Name = 'MissingPath' }, [PSCustomObject][ordered]@{ Path = $path; Name = 'Text' }) -ErrorAction Continue }
  Add-Observation 'duplicate-expected' { Restore-RegistrySettingState $before -ExpectedState @($expected[0], $expected[0]) }
  Add-Observation 'desired-empty' { Compare-RegistrySettingState @() }
  Add-Observation 'desired-invalid-path' { Compare-RegistrySettingState @{ Path = 'NONSENSE'; Name = 'Text'; Type = 'String'; Preferred = 'A' } -ErrorAction Continue }
  Add-Observation 'snapshot-invalid-path' { Export-RegistrySettingState @{ Path = 'NONSENSE'; Name = 'Text' } -Detailed -ErrorAction Continue }
  Add-Observation 'desired-version' { Compare-RegistrySettingState @{ Path = $path; Name = 'Text'; SnapshotVersion = 2; Type = 'String'; Preferred = 'Text' } }
  Add-Observation 'desired-absence' { Compare-RegistrySettingState @{ Path = $path; Name = 'Text'; Exists = $false } }
  Add-Observation 'restore-missing-key' {
    $missing = [PSCustomObject]@{ SnapshotVersion = 1; Path = "$path\Recreated"; Name = ''; View = 'Registry32'; Exists = $true; Type = 'DWord'; Preferred = -1 }
    Restore-RegistrySettingState $missing -Confirm:$false
    Export-RegistrySettingState $missing -Detailed
  }
  Add-Observation 'audit' {
    ConvertTo-RegistrySettingResult -Settings ([PSCustomObject]@{ Path = $path; Name = 'Text'; Preferred = 'Text'; Description = 'Known text' })
    ConvertTo-RegistrySettingResult -Settings @{ Path = $path; Name = 'Text'; Preferred = 'Text'; Description = 'Ignored hashtable description' }
    ConvertTo-RegistrySettingResult -Settings $settings -DryRun
    ConvertTo-RegistrySettingResult -Settings @{ Path = $path; Name = 'Absent'; Default = $null } -Undo
  }
  Add-Observation 'audit-source-null' { ConvertTo-RegistrySettingResult -Settings @{ Path = $path; Name = 'Text'; Preferred = 'Text' } -Source $null -DryRun }
  $export = Join-Path $scratch 'export with spaces.reg'
  $search = Join-Path $scratch 'search with spaces.txt'
  Add-Observation 'export-file' { Export-RegistryKey -Key $native -OutputPath $export; [IO.File]::ReadAllText($export) }
  Add-Observation 'search-file' { Search-RegistryKey -Root $native -Pattern 'Text' -OutputPath $search; [IO.File]::ReadAllText($search); [int][IO.File]::ReadAllBytes($search)[0] }
  Add-Observation 'quoted-search' {
    Set-RegistryValue $path -Name Quoted -Value 'a "quoted" value & not-a-command'
    Search-RegistryKey -Root $native -Pattern '"quoted"' -OutputPath $search
    [IO.File]::ReadAllText($search)
  }
  Add-Observation 'file-failure' {
    Export-RegistryKey -Key "$native\Absent" -OutputPath $export -ErrorAction Continue
    Search-RegistryKey -Root $native -Pattern 'NoSuchSyntheticValue0000' -OutputPath $search -ErrorAction Continue
    @(Get-ChildItem $scratch -Filter '.psf-reg-*').Count
  }
  Add-Observation 'remove-values' { Remove-RegistryValue $path -Name Text -WhatIf; Test-RegistryValue $path -Name Text; Remove-RegistryValue $path -Name Text -Verbose; Remove-RegistryValue $path -Name Text -Verbose }
  Add-Observation 'hive-missing' { Mount-DefaultUserHive -MountName PSFSyntheticMissing -HivePath (Join-Path $scratch 'missing.dat'); Dismount-DefaultUserHive -MountName PSFSyntheticMissing }
  Add-Observation 'hive-preview' { Mount-DefaultUserHive -MountName PSFSyntheticMissing -HivePath $export -WhatIf }
  Add-Observation 'hive-validation' { Mount-DefaultUserHive -MountName '../invalid' -HivePath $export -WhatIf }
  Add-Observation 'remove-key' { Remove-RegistryKey $path -Recurse -WhatIf; Test-RegistryPath $path; Remove-RegistryKey $path -Recurse -Verbose; Test-RegistryPath $path }
}
finally {
  [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($subkey, $false)

  # Both paths are constructed below the dedicated report directory.
  if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}

[IO.File]::WriteAllText($ReportPath, ($observations | ConvertTo-Json -Depth 40), (New-Object Text.UTF8Encoding($false)))
Write-Output "Registry behavior captured: $($observations.Count) cases"
