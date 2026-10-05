---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-DefaultGateway
---

# Get-DefaultGateway

## SYNOPSIS

Returns the selected adapter's gateway for the requested address family.

## SYNTAX

### __AllParameterSets

```
Get-DefaultGateway [[-AddressFamily] <string>] [[-Adapter] <psobject>] [-Required]
```

## ALIASES

None.

## DESCRIPTION

Keeps the original arguments and result while C# selects the gateway.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-DefaultGateway -AddressFamily IPv6
```

## PARAMETERS

### -Adapter

A previously resolved adapter; defaults to the default adapter.

```yaml
Type: System.Management.Automation.PSObject
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -AddressFamily

IPv4 or IPv6; defaults to IPv4.

```yaml
Type: System.String
DefaultValue: IPv4
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

### -Required

Throws if the gateway is unavailable.

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

## INPUTS

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
