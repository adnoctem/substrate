#Requires -Version 7.0
<#
.SYNOPSIS
  Proposes updates to pinned PowerShell development modules.
.DESCRIPTION
  Writes dependency candidates only. The workflow restores, verifies, and opens a reviewable pull request.

  A dependency with HoldUntil (yyyy-MM-dd) and HoldReason keeps its pin until that date. Newer versions of held
  dependencies are reported but not applied. The report is written to build/dependencies/report.md and, in GitHub
  Actions, to the job summary.
.EXAMPLE
  ./tools/dependencies.ps1
#>
[CmdletBinding()]
param ()

$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'dev-dependencies.json'
$reportPath = Join-Path $PSScriptRoot '../build/dependencies/report.md'
$dependencies = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
$today = [datetime]::UtcNow.Date
$changes = [Collections.Generic.List[string]]::new()
$held = [Collections.Generic.List[string]]::new()
$expired = [Collections.Generic.List[string]]::new()

foreach ($dependency in $dependencies) {
  $holdUntil = $null

  if ($dependency.PSObject.Properties['HoldUntil']) {
    # PowerShell 7 may already convert ISO date strings to DateTime; write the date back in its original form.
    $holdUntil = if ($dependency.HoldUntil -is [datetime]) { $dependency.HoldUntil.Date } else { [datetime]::ParseExact($dependency.HoldUntil, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) }
    $dependency.HoldUntil = $holdUntil.ToString('yyyy-MM-dd')

    if (-not $dependency.PSObject.Properties['HoldReason'] -or [string]::IsNullOrWhiteSpace($dependency.HoldReason)) { throw "Held dependency needs a HoldReason: $($dependency.Name)" }
  }

  $latest = Find-PSResource -Name $dependency.Name -Repository PSGallery -ErrorAction Stop

  if ($holdUntil -and $holdUntil -le $today) { $expired.Add("``$($dependency.Name)`` hold ended on $($dependency.HoldUntil); remove HoldUntil and HoldReason.") }

  if ([version]$latest.Version -le [version]$dependency.RequiredVersion) { continue }

  if ($holdUntil -and $holdUntil -gt $today) {
    $held.Add("| ``$($dependency.Name)`` | $($dependency.RequiredVersion) | $($latest.Version) | $($dependency.HoldUntil) | $($dependency.HoldReason) |")
    continue
  }

  $changes.Add("$($dependency.Name): $($dependency.RequiredVersion) -> $($latest.Version)")
  $dependency.RequiredVersion = $latest.Version.ToString()
}

if ($changes.Count) {
  [IO.File]::WriteAllText($path, (($dependencies | ConvertTo-Json -Depth 4) -replace '\r?\n', "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
}

$report = [Text.StringBuilder]::new()
$null = $report.AppendLine('## PowerShell development tools').AppendLine()

if ($changes.Count) {
  $null = $report.AppendLine('Updated pins, restored and verified with `Verify`. Review regenerated command help before merging:').AppendLine()
  foreach ($change in $changes) { $null = $report.AppendLine("- $change") }
}
else {
  $null = $report.AppendLine('No pinned tool updates.')
}

if ($held.Count) {
  $null = $report.AppendLine().AppendLine('### Held updates').AppendLine().AppendLine('Newer versions exist but stay unapplied until the hold date passes:').AppendLine()
  $null = $report.AppendLine('| Tool | Pinned | Available | Held until | Reason |').AppendLine('| --- | --- | --- | --- | --- |')
  foreach ($row in $held) { $null = $report.AppendLine($row) }
}

if ($expired.Count) {
  $null = $report.AppendLine().AppendLine('### Expired holds').AppendLine()
  foreach ($line in $expired) { $null = $report.AppendLine("- $line") }
}

$null = [IO.Directory]::CreateDirectory((Split-Path $reportPath -Parent))
[IO.File]::WriteAllText($reportPath, $report.ToString(), [Text.UTF8Encoding]::new($false))

if ($env:GITHUB_STEP_SUMMARY) { [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, $report.ToString(), [Text.UTF8Encoding]::new($false)) }

$changes
$held | ForEach-Object { "held: $_" }
