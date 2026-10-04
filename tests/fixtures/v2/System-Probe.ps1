#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$observations = [ordered]@{}
function Get-Shape {
  param ($Value)
  @($Value.PSObject.Properties | ForEach-Object {
      [ordered]@{ Name = $_.Name; Type = if ($null -eq $_.Value) { 'null' } else { $_.Value.GetType().FullName } }
    })
}
foreach ($command in @('Get-OSBuildNumber', 'Get-OSDisplayVersion', 'Get-OSEdition', 'Get-OSProductName', 'Get-OSVersionInfo', 'Get-Hostname', 'Get-SystemPaths')) {
  $observations[$command] = & $command
}
$observations['named-paths'] = Get-SystemPaths 'synthetic-product'
$user = Get-UserInfo
$observations['identity'] = @($user.GetType().FullName, $user.UserName, $user.IsAdministrator, $user.SID, (Get-UserSID $user.UserName), (Test-Elevation), (& (Get-Module PSFoundation) { Read-ProcessElevation }))
$observations['identity-extra-arguments'] = (Get-UserInfo -Unused extra).SID
$observations['applicability'] = @(Test-HostApplicability; Test-HostApplicability -MinBuild 0; Test-HostApplicability -MaxBuild 0; Test-HostApplicability -Edition @(Get-OSEdition); Test-HostApplicability -Edition 'synthetic-edition'; Test-HostApplicability -Bitness x64; Test-HostApplicability -Bitness x86; Test-HostApplicability -Edition @())
$observations['robocopy'] = @(-1..33 | ForEach-Object { Convert-RobocopyExitCode $_ })
$observations['com-managed'] = @(Remove-ComObject $null ([PSCustomObject]@{ Synthetic = 1 }) 'text'; Invoke-ComGarbageCollection)
$oldCulture = [Threading.Thread]::CurrentThread.CurrentCulture
try {
  [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('de-DE')
  $memory = Get-SystemMemory
  if ($memory.TotalBytes -le 0 -or $memory.AvailableBytes -gt $memory.TotalBytes -or $memory.LoadPercent -gt 100) { throw 'Invalid memory snapshot.' }
  if ($memory.TotalGiB -match ',') { throw 'Memory formatting depends on culture.' }
  $observations['memory-shape'] = Get-Shape $memory
  $observations['memory-total'] = @($memory.TotalBytes, $memory.TotalGiB)
  $uptime = Get-SystemUptime
  if ($uptime.TotalMilliseconds -le 0 -or $uptime.Display -notmatch '^\d+d \d{2}h \d{2}m \d{2}s$') { throw 'Invalid uptime snapshot.' }
  $observations['uptime-shape'] = Get-Shape $uptime
  $disks = @(Get-SystemDisk)
  $observations['disk-shapes'] = @($disks | ForEach-Object { , (Get-Shape $_) })
  $observations['disk-stable'] = @($disks | Select-Object Name, Label, Type, FileSystem, TotalBytes)
  foreach ($disk in $disks) {
    if ($disk.FreeBytes -gt $disk.TotalBytes -or $disk.TotalGiB -match ',' -or $disk.Type -ne 'Fixed') { throw 'Invalid disk snapshot.' }
  }
  $observations['all-disks'] = @(Get-SystemDisk -All | Select-Object Name, Label, Type, FileSystem, TotalBytes)
  $summary = Get-SystemInfo
  $observations['system-shape'] = Get-Shape $summary
  if ($summary.OSBuild -ne (Get-OSBuildNumber) -or $summary.OSProductName -ne (Get-OSProductName)) { throw 'System aggregate differs from individual readers.' }
}
finally { [Threading.Thread]::CurrentThread.CurrentCulture = $oldCulture }
if ((Get-Command Get-SystemPaths).CommandType -eq 'Cmdlet') {
  foreach ($name in @('.', '..', 'trailing.')) {
    $rejected = $false
    try { $null = Get-SystemPaths $name } catch { $rejected = $true }
    if (-not $rejected) { throw 'Product path traversal was accepted.' }
  }
}
[IO.File]::WriteAllText($ReportPath, ($observations | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false)))
Write-Output "System compatibility observations: $($observations.Count)"
