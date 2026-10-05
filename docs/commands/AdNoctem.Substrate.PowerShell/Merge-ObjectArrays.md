---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Merge-ObjectArrays
---

# Merge-ObjectArrays

## SYNOPSIS

Merges matching override properties into the base array in place.

## SYNTAX

### __AllParameterSets

```
Merge-ObjectArrays [-Base] <array> [-Overrides] <array> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Matches Name and, when supplied, Path.
Changes only existing base keys.

## EXAMPLES

### EXAMPLE 1

```powershell
Merge-ObjectArrays -Base $base -Overrides $overrides
```

## PARAMETERS

### -Base

Base records to modify.

```yaml
Type: System.Array
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

### -Overrides

Override records containing matching identity and replacement properties.

```yaml
Type: System.Array
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 1
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

### System.Void

## NOTES

## RELATED LINKS
