---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-IPv6Address
---

# Test-IPv6Address

## SYNOPSIS

Returns $true if the input is a valid IPv6 address per RFC 4291.

## SYNTAX

### __AllParameterSets

```
Test-IPv6Address [-Address] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Validates via structural decomposition: handles full form, :: compression,
and IPv4-mapped addresses (::ffff:x.x.x.x).
The embedded IPv4 portion of
mapped addresses is delegated to Test-IPv4Address.

## EXAMPLES

### Example 1

```powershell
Test-IPv6Address -Address '2001:db8::ff00:42:8329'
```

## PARAMETERS

### -Address

The string to validate.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
