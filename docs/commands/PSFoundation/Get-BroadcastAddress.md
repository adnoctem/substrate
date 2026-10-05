---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-BroadcastAddress
---

# Get-BroadcastAddress

## SYNOPSIS

Computes the IPv4 broadcast address from the selected adapter's address and mask.

## SYNTAX

### __AllParameterSets

```
Get-BroadcastAddress [[-Adapter] <psobject>] [-Required]
```

## ALIASES

None.

## DESCRIPTION

Retains the original arguments and address string output.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-BroadcastAddress
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

Throws if the address or mask is unavailable.

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
