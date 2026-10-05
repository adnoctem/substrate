#Requires -Version 7.0
<#
.SYNOPSIS
  Checks official LGPO and ODT sources and prepares reviewed-source update candidates without executing them.
.PARAMETER Propose
  Writes verified candidate pins to source files for a reviewed pull request. Never pushes or opens a PR itself.
.EXAMPLE
  ./tools/vendor-updates.ps1
#>
[CmdletBinding()]
param ([switch]$Propose)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'vendor-support.ps1')
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root ('build/vendor-tools/' + [guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($work)
$evidence = [Collections.Generic.List[object]]::new()
$proposals = [Collections.Generic.List[object]]::new()
$changed = $false
foreach ($tool in @('LGPO', 'ODT')) {
  $sourcePath = if ($tool -eq 'LGPO') { 'src/Policies/LgpoSource.cs' } else { 'src/Office/OdtTool.cs' }
  $sourcePath = Join-Path $root $sourcePath
  $text = [IO.File]::ReadAllText($sourcePath)
  $reviewed = [regex]::Match($text, 'public static (?:LgpoSource|OdtSource) (?:Reviewed|Standalone) \{ get; \} = new (?:LgpoSource|OdtSource)\([\s\S]*?\);').Value
  if (-not $reviewed) { throw "Cannot isolate reviewed $tool metadata." }
  $pattern = if ($tool -eq 'LGPO') { 'https://download\.microsoft\.com/[^"\s]+/LGPO\.zip' } else { 'https://download\.microsoft\.com/[^"\s]+/officedeploymenttool_[^"\s]+\.exe' }
  $current = [regex]::Match($text, $pattern).Value
  if (-not $current) { throw "Cannot find current $tool source pin." }
  $page = if ($tool -eq 'LGPO') { 'https://www.microsoft.com/en-us/download/details.aspx?id=55319' } else { 'https://www.microsoft.com/en-us/download/details.aspx?id=49117' }
  $html = [Net.WebUtility]::HtmlDecode((Invoke-WebRequest -Uri $page -MaximumRedirection 5 -TimeoutSec 60).Content).Replace('\/', '/')
  $candidates = @([regex]::Matches($html, $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase) | ForEach-Object { $_.Value } | Sort-Object -Unique)
  if ($candidates.Count -ne 1) { throw "Official $tool page did not expose one unambiguous candidate. Review the download page manually." }
  $uri = [uri]$candidates[0]
  $download = Join-Path $work ([IO.Path]::GetFileName($uri.AbsolutePath))
  Save-MicrosoftDownload -Uri $uri -Destination $download
  $downloadHash = (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash
  $binary = $download
  $entryPath = $null
  if ($tool -eq 'LGPO') {
    $zip = [IO.Compression.ZipFile]::OpenRead($download)
    try {
      $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
      foreach ($entry in $zip.Entries) {
        Assert-VendorArchivePath $entry.FullName
        if (-not $names.Add($entry.FullName)) { throw 'Duplicate archive entry.' }
        if ($entry.Length -gt 67108864) { throw 'Archive entry exceeds the inspection limit.' }
      }
      $entries = @($zip.Entries | Where-Object { $_.Name -ceq 'LGPO.exe' })
      if ($entries.Count -ne 1 -or $entries[0].Length -gt 16777216) { throw 'Expected exactly one bounded LGPO.exe entry.' }
      $entryPath = $entries[0].FullName
      $binary = Join-Path $work 'LGPO.exe'
      $inputStream = $entries[0].Open()
      try {
        $output = [IO.File]::Open($binary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        try {
          $buffer = [byte[]]::new(81920)
          $total = 0L
          while (($count = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $total += $count
            if ($total -gt 16777216) { throw 'Expanded LGPO.exe exceeds its size limit.' }
            $output.Write($buffer, 0, $count)
          }
        }
        finally { $output.Dispose() }
      }
      finally { $inputStream.Dispose() }
    }
    finally { $zip.Dispose() }
  }
  $signature = Get-MicrosoftSignatureEvidence $binary
  if ($tool -eq 'LGPO' -and $signature.OriginalFilename -ne 'LGPO.exe') { throw 'Unexpected LGPO executable identity.' }
  if ($tool -eq 'ODT' -and $signature.Version.Major -ne 16) { throw 'Unexpected ODT distribution version. Review its identity manually.' }
  $replacement = $reviewed.Replace($current, $uri.AbsoluteUri)
  $previousHashes = @()
  $previousVersion = [regex]::Match($text, 'new Version\(([^)]+)\)').Groups[1].Value.Replace('"', '').Replace(', ', '.')
  if ($tool -eq 'LGPO') {
    $pins = @([regex]::Matches($text, '"([A-Fa-f0-9]{64})"') | ForEach-Object { $_.Groups[1].Value })
    if ($pins.Count -ne 2) { throw 'Expected exactly two LGPO digest pins.' }
    $previousHashes = $pins
    $oldEntry = [regex]::Match($text, '"([^"\r\n]+/LGPO\.exe)"').Groups[1].Value
    $replacement = $replacement.Replace($pins[0], $downloadHash).Replace($pins[1], $signature.Sha256).Replace($oldEntry, $entryPath)
    $v = $signature.Version
    $replacement = $replacement -replace 'new Version\(\d+, \d+, \d+, \d+\)', "new Version($($v.Major), $($v.Minor), $($v.Build), $($v.Revision))"
  }
  else {
    $replacement = $replacement -replace 'new Version\("[0-9.]+"\)', ('new Version("' + $signature.Version + '")')
  }
  $toolChanged = $replacement -cne $reviewed
  if ($toolChanged) {
    $changed = $true
    $now = [DateTime]::UtcNow
    $replacement = $replacement -replace 'new DateTime\(\d+, \d+, \d+, 0, 0, 0, DateTimeKind.Utc\)', "new DateTime($($now.Year), $($now.Month), $($now.Day), 0, 0, 0, DateTimeKind.Utc)"
    $proposals.Add(@{ Path = $sourcePath; Content = $text.Replace($reviewed, $replacement) })
  }
  $evidence.Add([ordered]@{ Tool = $tool; Changed = $toolChanged; PreviousUri = $current; PreviousVersion = $previousVersion; PreviousSha256Pins = $previousHashes; CandidateUri = $uri.AbsoluteUri; DownloadSha256 = $downloadHash; Entry = $entryPath; Signature = $signature })
}
if ($Propose) {
  foreach ($proposal in $proposals) { [IO.File]::WriteAllText($proposal.Path, $proposal.Content, [Text.UTF8Encoding]::new($false)) }
}
$reportRoot = Join-Path $root 'build/vendor-tools'
$json = $evidence | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Join-Path $reportRoot 'evidence.json'), $json)
$body = "Verified candidates from official Microsoft download pages. No candidate executable was run.`n`n"
$body += "LGPO archive and extracted executable hashes are proposed together. ODT evidence covers the signed distribution; extraction and setup.exe validation remain runtime checks.`n`n"
$body += "Review all changes before merging. There is no automatic acceptance of new hashes.`n`n" + '```json' + "`n" + $json + "`n" + '```' + "`n"
[IO.File]::WriteAllText((Join-Path $reportRoot 'report.md'), $body)
Write-Output "Vendor checks completed. Candidate changes: $changed. Evidence: $reportRoot"
