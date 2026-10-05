---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-MulticastAddress
---

# Get-MulticastAddress

## SYNOPSIS

Computes the solicited-node multicast address for the selected IPv6 address.

## SYNTAX

### __AllParameterSets

```
Get-MulticastAddress [[-Adapter] <psobject>] [-Required]
```

## ALIASES

None.

## DESCRIPTION

Retains the original arguments and address string output.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-MulticastAddress
```

## PARAMETERS

### -Adapter

A previously resolved adapter; defaults to the default adapter.

```yaml
Type: System.Management.Automation.PSObject
DefaultValue: ""
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

### -Required

Throws if no IPv6 address is available.

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

## INPUTS

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
