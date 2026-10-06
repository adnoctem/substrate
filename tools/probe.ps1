#Requires -Version 5.1

function Invoke-SubstrateHostProbe {
  <#
  .SYNOPSIS
    Runs a fresh-host test probe with bounded memory, output and lifetime.
  .PARAMETER Engine
    PowerShell executable to run.
  .PARAMETER ArgumentList
    Separate process arguments; no shell evaluation occurs.
  .PARAMETER TimeoutSeconds
    Maximum probe duration, excluding a short cleanup wait.
  .PARAMETER MemoryLimitMiB
    Windows job commit limit for the probe and its descendants together.
  .EXAMPLE
    Invoke-SubstrateHostProbe -Engine pwsh -ArgumentList @('-NoProfile', '-File', '.\probe.ps1')
  #>
  [CmdletBinding()]
  param (
    [Parameter(Mandatory = $true)][string]$Engine,
    [Parameter(Mandatory = $true)][string[]]$ArgumentList,
    [ValidateRange(1, 600)][int]$TimeoutSeconds = 120,
    [ValidateRange(32, 1024)][int]$MemoryLimitMiB = 512
  )

  if ($null -eq ('AdNoctem.Substrate.DevTools.ProbeProcess' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'ProbeProcess.cs') -ErrorAction Stop
  }

  $executable = (Get-Command -Name $Engine -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
  [AdNoctem.Substrate.DevTools.ProbeProcess]::Run($executable, $ArgumentList, ($TimeoutSeconds * 1000), $MemoryLimitMiB)
}
