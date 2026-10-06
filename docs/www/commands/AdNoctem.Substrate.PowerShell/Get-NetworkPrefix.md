---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-NetworkPrefix
---

# Get-NetworkPrefix

## SYNOPSIS

Returns the selected adapter's IPv4 network address or IPv6 prefix.

## SYNTAX

### __AllParameterSets

```
Get-NetworkPrefix [[-AddressFamily] <string>] [[-Adapter] <psobject>] [-Required]
```

## ALIASES

Get-Network, Get-Prefix

## DESCRIPTION

C# performs the calculation while this function preserves the original calling conventions.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-NetworkPrefix -AddressFamily IPv6
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

Throws if the required address or mask is unavailable.

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

Legacy PowerShell calculations retain historical acceptance and failure rules, including noncontiguous-mask behavior and non-byte-aligned IPv6 prefix failures where applicable. The reusable C# NetworkManager and NetworkValidator provide stricter calculations. Prefix selection uses the first address with prefix information and can differ from the preferred IPv6 address returned by Get-IPAddress.

## RELATED LINKS
