---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-DNSServer
---

# Get-DNSServer

## SYNOPSIS

Returns the selected adapter's DNS servers for the requested address family.

## SYNTAX

### __AllParameterSets

```
Get-DNSServer [[-AddressFamily] <string>] [[-Adapter] <psobject>] [-Required]
```

## ALIASES

None.

## DESCRIPTION

Keeps the original arguments, server order and pipeline output.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-DNSServer
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
    Position: 1
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
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
HelpMessage: ""
```

### -Required

Throws if no DNS servers are configured for the family.

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

The compatibility function retains legacy argument binding, including ignored extra arguments. Domain reads and calculations execute in C#; provider failures remain errors rather than evidence of absent configuration.

## RELATED LINKS
