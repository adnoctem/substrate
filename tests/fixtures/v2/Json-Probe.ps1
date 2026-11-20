#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force

# Nested report-shaped data with the characters Windows PowerShell 5.1 escapes and an escaped literal backslash.
$value = [ordered]@{
  Call    = ".\Import.ps1 -SourcePath 'C:\Migration\<user>' -Mode A&B"
  Literal = 'C:\u0027\data'
  Empty   = [ordered]@{}
  None    = @()
  Nested  = [ordered]@{
    Level = [ordered]@{
      Deep = [ordered]@{
        Values = @(1, 2)
      }
    }
  }
}

$json = ConvertTo-FormattedJson -InputObject $value -Depth 10
$lines = $json -split "`r?`n"
$parsed = $json | ConvertFrom-Json
$piped = @('a', 'b') | ConvertTo-FormattedJson

if ($parsed.Call -cne $value.Call) { throw "Call did not round-trip: $($parsed.Call)" }
if ($parsed.Literal -cne 'C:\u0027\data') { throw "Escaped literal text changed: $($parsed.Literal)" }
if ($json -match '(?<!\\)\\u00(27|3c|3e|26)') { throw 'Readable characters remain escaped.' }
if ($lines[1] -cne '  "Call": ".\\Import.ps1 -SourcePath ''C:\\Migration\\<user>'' -Mode A&B",') { throw "Unexpected first property line: $($lines[1])" }
if ($json -notmatch '"Empty": \{\}' -or $json -notmatch '"None": \[\]') { throw 'Empty object or array was expanded.' }

# Indentation grows by exactly two spaces per level; nothing drifts beyond the nesting depth.
$widest = ($lines | ForEach-Object { $_.Length - $_.TrimStart().Length } | Measure-Object -Maximum).Maximum
if ($widest -ne 10) { throw "Deepest indentation is $widest, expected 10." }
if (($piped | ConvertFrom-Json).Count -ne 2) { throw 'Pipeline input was not serialized as an array.' }

[IO.File]::WriteAllText($ReportPath, $json, [Text.UTF8Encoding]::new($false))
Write-Output 'json probe passed'
