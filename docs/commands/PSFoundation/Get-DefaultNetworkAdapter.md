---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
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
HelpMessage: ''
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
HelpMessage: ''
```

## INPUTS

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
