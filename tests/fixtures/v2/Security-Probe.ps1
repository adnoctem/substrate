#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$observations = [ordered]@{}
$configuration = Import-SecurityEventConfiguration -Force
$observations['groups'] = @($configuration.Groups.Keys | Sort-Object)
$observations['group'] = (Get-SecurityEventGroup -Name Logon).EventIds
$observations['definitions'] = @(Get-SecurityEventDefinition -Group Logon | Sort-Object Id | Select-Object Id, Name, LogName, ProviderName, UtilityGroup)
$observations['filtered'] = @(Get-SecurityEventDefinition -LogName Security -ProviderName Microsoft-Windows-Security-Auditing -Id 4624, 4625 | Sort-Object Id | Select-Object Id, Name)
$observations['mapping'] = (Resolve-WindowsEventMappedField -MapName LogonType -Value 10).Name
$observations['missing-mapping'] = Resolve-WindowsEventMappedField -MapName LogonType -Value 999
$observations['channels'] = @(Test-WindowsEventLogChannel -LogName System; Test-WindowsEventLogChannel -LogName 'System*'; Test-WindowsEventLogChannel -LogName PSFoundation.Synthetic.Nonexistent)
$sampleEvent = [PSCustomObject]@{ TimeCreated = [datetime]'2026-01-01'; Id = 4624; ProviderName = 'Synthetic'; LogName = 'Security'; MachineName = 'Synthetic'; RecordId = 12L; LevelDisplayName = 'Information' }
$sampleEvent | Add-Member ScriptMethod ToXml { '<Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event"><EventData><Data Name="TargetUserName">synthetic</Data><Data Name="LogonType">10</Data><Data Name="ImpersonationLevel">%%1833</Data><Data Name="IpAddress">192.0.2.1</Data></EventData></Event>' }
$observations['converted'] = $sampleEvent | ConvertFrom-WinEvent | Select-Object * -ExcludeProperty EventRecord
$directory = Join-Path ([IO.Path]::GetTempPath()) ('PSFoundation-event-probe-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($directory)
try {
  $missing = Join-Path $directory 'missing.txt'
  $observations['missing-export'] = Export-EventLog -LogName PSFoundation.Synthetic.Nonexistent -OutputPath (Join-Path $directory 'never.evtx') -MissingLogPath $missing
  $observations['missing-text'] = [IO.File]::ReadAllText($missing)
  $observations['missing-bytes'] = [Convert]::ToBase64String([IO.File]::ReadAllBytes($missing))
  if ((Get-Command Export-EventLog).CommandType -eq 'Cmdlet') {
    $expectedDefinitions = @(Get-SecurityEventDefinition | ForEach-Object { '{0}|{1}|{2}|{3}|{4}' -f $_.Id, $_.LogName, $_.ProviderName, $_.Name, $_.UtilityGroup } | Sort-Object)
    $actualDefinitions = @([PSFoundation.Security.SecurityEventCatalog]::Default.Definitions | ForEach-Object { '{0}|{1}|{2}|{3}|{4}' -f $_.Id, $_.LogName, $_.ProviderName, $_.Name, $_.Group } | Sort-Object)
    if (Compare-Object $expectedDefinitions $actualDefinitions) { throw 'The C# event catalog differs from the PowerShell configuration.' }
    $latest = Get-WinEvent -LogName System -MaxEvents 1
    try {
      $start = $latest.TimeCreated.AddSeconds(-1)
      $end = $latest.TimeCreated.AddSeconds(1)
      $records = @(Get-WindowsEventByDefinition -Id $latest.Id -LogName System -StartTime $start -EndTime $end -MaxEvents 1)
      try {
        if ($records.Count -ne 1 -or $records[0].Id -ne $latest.Id) { throw 'Native event query did not honor its ID and result limit.' }
        if ([string]::IsNullOrWhiteSpace($records[0].ToXml())) { throw 'Returned event record lost its native data.' }
      }
      finally { foreach ($record in $records) { $record.Dispose() } }
      $synthetic = @{
        FieldMaps = @{}
        Groups    = @{ BootShutdown = @{ EventIds = @($latest.Id); DefaultLogs = @('System') } }
        Events    = @{ System = @{ Synthetic = @(@{ Id = $latest.Id; LogName = 'System'; UtilityGroup = 'BootShutdown' }) } }
      }
      $events = @(Get-WindowsBootEvent -Configuration $synthetic -StartTime $start -EndTime $end -MaxEvents 1)
      try {
        if ($events.Count -ne 1 -or $events[0].Id -ne $latest.Id -or $null -eq $events[0].RawData) { throw 'Semantic event query failed.' }
      }
      finally { foreach ($item in $events) { $item.EventRecord.Dispose() } }
    }
    finally { $latest.Dispose() }
    $unsafe = Join-Path $directory 'unsafe.psd1'
    [IO.File]::WriteAllText($unsafe, '@{ Value = (Get-Process) }')
    $rejected = $false
    try { $null = Import-SecurityEventConfiguration -Path $unsafe -Force } catch { $rejected = $true }
    if (-not $rejected) { throw 'Configuration executed a command expression.' }
    $null = Import-SecurityEventConfiguration -Force
  }
}
finally { [IO.Directory]::Delete($directory, $true) }
[IO.File]::WriteAllText($ReportPath, ($observations | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Write-Output "Security compatibility observations: $($observations.Count)"
