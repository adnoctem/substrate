---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-DefaultNetworkAdapter
---

# Get-DefaultNetworkAdapter

## SYNOPSIS

Resolves the default IPv4 adapter using the lowest route metric.

## SYNTAX

### __AllParameterSets

```
Get-DefaultNetworkAdapter [[-Type] <string>] [-Required]
```

## ALIASES

None.

## DESCRIPTION

Preserves the original PowerShell arguments and CIM result objects; C# performs the queries and selection.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-DefaultNetworkAdapter -Type Ethernet -Required
```

## PARAMETERS

### -Required

Throws instead of logging and returning no adapter.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: Named
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Type

Filters the selected adapter by Any, WiFi, Ethernet or VPN.

```yaml
Type: System.String
DefaultValue: Any
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

## INPUTS

## OUTPUTS

### System.Object

## NOTES

The compatibility function retains legacy argument binding, including ignored extra arguments. Domain reads and calculations execute in C#; provider failures remain errors rather than evidence of absent configuration.

Selection uses the lowest IPv4 default-route metric among routes with a nonzero next hop, without adding the interface metric or proving Internet reachability. The media filter checks that selected adapter; it does not choose another route. Returned NetAdapter and CimConfig objects are detached CIM instances owned by the caller; dispose them when finished. The wrapper loads NetAdapter type extensions, while C# performs the queries.

## RELATED LINKS
