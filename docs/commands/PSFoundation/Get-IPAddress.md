---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-IPAddress
---

# Get-IPAddress

## SYNOPSIS

Returns an address from the selected adapter.

## SYNTAX

### __AllParameterSets

```
Get-IPAddress [[-AddressFamily] <string>] [[-Adapter] <psobject>] [-Required]
```

## ALIASES

None.

## DESCRIPTION

Prefers a non-link-local IPv6 address and preserves legacy argument handling.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-IPAddress -AddressFamily IPv6
```

## PARAMETERS

### -Adapter

An adapter returned by Get-DefaultNetworkAdapter.
Omit to resolve the default adapter.

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

Throws if the address is unavailable.

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
