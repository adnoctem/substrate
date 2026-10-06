---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-IPv4Address
---

# Test-IPv4Address

## SYNOPSIS

Returns $true if the input is a valid IPv4 address per RFC 791.

## SYNTAX

### __AllParameterSets

```
Test-IPv4Address [-Address] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Validates via arithmetic decomposition: four dot-separated decimal octets,
each in 0-255, no leading zeros.
Does not use regex.

## EXAMPLES

### Example 1

```powershell
Test-IPv4Address -Address '192.168.1.1'
```

## PARAMETERS

### -Address

The string to validate.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
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

This command retains legacy PowerShell acceptance rules. Applications needing strict address-literal parsing should use the reusable C# NetworkValidator. The command does not perform DNS resolution or establish reachability.

## RELATED LINKS
