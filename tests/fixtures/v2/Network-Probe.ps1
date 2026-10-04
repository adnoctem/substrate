#Requires -Version 5.1
param ([string]$ModulePath, [string]$ReportPath)

$ErrorActionPreference = 'Stop'
$null = Import-Module $ModulePath -Force
$observations = [ordered]@{}
function Convert-Error {
  param ([Management.Automation.ErrorRecord]$Record)
  [ordered]@{ Stream = 'Error'; Message = $Record.Exception.Message; Exception = $Record.Exception.GetType().FullName; Category = [string]$Record.CategoryInfo.Category; Id = ($Record.FullyQualifiedErrorId -split ',')[0]; Target = $Record.TargetObject }
}
function Add-Observation {
  param ([string]$Name, [scriptblock]$Action)
  $records = New-Object 'Collections.Generic.List[object]'
  $terminated = $false
  try {
    & $Action *>&1 | ForEach-Object {
      if ($null -eq $_) { $records.Add([ordered]@{ Type = 'null'; Value = $null }) }
      elseif ($_ -is [Management.Automation.ErrorRecord]) { $records.Add((Convert-Error $_)) }
      elseif ($_ -is [Management.Automation.InformationRecord]) {
        $records.Add([ordered]@{ Stream = 'Information'; Text = [string]$_.MessageData; Color = [string]$_.MessageData.ForegroundColor; Tags = @($_.Tags) })
      }
      else { $records.Add([ordered]@{ Type = $_.GetType().FullName; Value = $_ }) }
    }
  }
  catch { $terminated = $true; $records.Add((Convert-Error $_)) }
  $observations[$Name] = [ordered]@{ Terminated = $terminated; Records = @($records.ToArray()) }
}
$commands = @('Get-IPAddress', 'Get-SubnetMask', 'Get-DefaultGateway', 'Get-DNSServer', 'Get-MACAddress', 'Get-NetworkPrefix', 'Get-NetworkPrefixCIDR', 'Get-BroadcastAddress', 'Get-MulticastAddress')
$familyCommands = @('Get-IPAddress', 'Get-DefaultGateway', 'Get-DNSServer', 'Get-NetworkPrefix', 'Get-NetworkPrefixCIDR')
foreach ($case in @(
    @{ Name = 'dual'; Addresses = @('192.0.2.129', 'fe80::1234%7', '2001:db8::abcd'); Masks = @('255.255.255.128', '64', '64') }
    @{ Name = 'single'; Addresses = '192.0.2.10'; Masks = '255.255.255.0' }
    @{ Name = 'empty'; Addresses = @(); Masks = @() }
    @{ Name = 'null'; Addresses = $null; Masks = $null }
    @{ Name = 'null-slot'; Addresses = @($null, '192.0.2.10'); Masks = @('255.0.0.0', '255.255.255.0') }
    @{ Name = 'unpaired'; Addresses = @('192.0.2.10', '2001:db8::1'); Masks = @() }
    @{ Name = 'zero-prefix'; Addresses = '2001:db8::1'; Masks = 0 }
    @{ Name = 'partial-prefix'; Addresses = '2001:db8::abcd'; Masks = 65 }
    @{ Name = 'mask-holes'; Addresses = '192.0.2.10'; Masks = '255.0.255.0' }
    @{ Name = 'malformed'; Addresses = 'broken'; Masks = '255.255.255.0' }
  )) {
  $configuration = [PSCustomObject]@{
    IPAddress = $case.Addresses; IPSubnet = $case.Masks; MACAddress = '00:11:22:33:44:55'
    DefaultIPGateway = @('192.0.2.254', 'fe80::1%7'); DNSServerSearchOrder = @('192.0.2.53', '2001:db8::53', '192.0.2.54')
  }
  $adapter = [PSCustomObject]@{ Name = 'Synthetic'; CimConfig = $configuration }
  foreach ($command in $commands) {
    foreach ($required in @($false, $true)) {
      $arguments = @{ Adapter = $adapter; Required = $required }
      Add-Observation "$($case.Name):${command}:${required}" { & $command @arguments }
      if ($command -in $familyCommands) {
        Add-Observation "$($case.Name):${command}:${required}:IPv6" { & $command @arguments -AddressFamily IPv6 }
      }
    }
  }
}
$adapter = [PSCustomObject]@{ Name = 'Synthetic'; CimConfig = [PSCustomObject]@{ IPAddress = '192.0.2.10'; IPSubnet = '255.255.255.0' } }
Add-Observation 'extra-arguments' { Get-IPAddress -Adapter $adapter -Unknown extra -Verbose }
Add-Observation 'positional' { Get-IPAddress IPv4 $adapter }
Add-Observation 'aliases' { Get-Network -Adapter $adapter; Get-Prefix -Adapter $adapter; Get-NetworkCIDR -Adapter $adapter; Get-PrefixCIDR -Adapter $adapter }
Add-Observation 'pipeline' { $adapter | Get-IPAddress -Adapter $adapter }
foreach ($command in @('Get-IPAddress', 'Get-MACAddress', 'Get-DNSServer')) {
  Add-Observation "missing-config:${command}" { & $command -Adapter ([PSCustomObject]@{ Name = 'Synthetic' }) }
  Add-Observation "missing-property:${command}" { & $command -Adapter ([PSCustomObject]@{ Name = 'Synthetic'; CimConfig = [PSCustomObject]@{} }) }
  Add-Observation "missing-name:${command}" { & $command -Adapter ([PSCustomObject]@{ CimConfig = [PSCustomObject]@{ IPAddress = '192.0.2.1'; MACAddress = '00:11:22:33:44:55'; DNSServerSearchOrder = @('192.0.2.53') } }) }
}
Add-Observation 'default-adapter-shape' {
  $actual = Get-DefaultNetworkAdapter
  if ($null -eq $actual) { 'NoDefaultAdapter' }
  else {
    [PSCustomObject]@{
      Properties        = @($actual.PSObject.Properties.Name)
      NameType          = $actual.Name.GetType().FullName
      IndexType         = $actual.ifIndex.GetType().FullName
      NativeType        = $actual.NetAdapter.GetType().FullName
      ConfigurationType = $actual.CimConfig.GetType().FullName
      NameMatches       = $actual.Name -eq $actual.NetAdapter.Name
      MediaMatches      = $actual.PhysicalMedia -eq $actual.NetAdapter.PhysicalMediaType
      IndexMatches      = $actual.ifIndex -eq $actual.CimConfig.InterfaceIndex
    }
    $actual.NetAdapter.Dispose()
    $actual.CimConfig.Dispose()
  }
}
Add-Observation 'colors' { Show-Color }
Add-Observation 'colors-unused' { Show-Color -Typo extra }
Add-Observation 'colors-verbose' { Show-Color -Verbose }
if ((Get-Command Show-Color).ScriptBlock.File -like '*compat.ps1') {
  $completions = [Management.Automation.CommandCompletion]::CompleteInput('Show-Color -Ver', 15, $null)
  if ($completions.CompletionMatches.CompletionText -notcontains '-Verbose') { throw 'Show-Color does not complete -Verbose.' }
  if ((Get-Command Show-Color).Parameters.Keys -notcontains 'ArgumentList') { throw 'Show-Color must declare its compatibility argument list.' }
  $commonNames = @((Get-Command Write-Log).Parameters.Keys | Where-Object { $_ -notin @('Message', 'Color', 'Timestamps') })
  $expectedNames = @($commonNames + 'ArgumentList' | Sort-Object)
  $actualNames = @((Get-Command Show-Color).Parameters.Keys | Sort-Object)
  if (($actualNames -join ',') -ne ($expectedNames -join ',')) { throw 'Unexpected Show-Color parameters.' }
  $argument = (Get-Command Show-Color).Parameters['ArgumentList']
  if ($argument.ParameterType -ne [object[]] -or -not ($argument.Attributes | Where-Object { $_ -is [Management.Automation.ParameterAttribute] -and $_.ValueFromRemainingArguments })) { throw 'Show-Color must accept remaining arguments explicitly.' }
}
$observations | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
