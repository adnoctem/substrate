---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: ConvertFrom-WinEvent
---

# ConvertFrom-WinEvent

## SYNOPSIS

Converts an event record to the established enriched result shape.

## SYNTAX

### __AllParameterSets

```
ConvertFrom-WinEvent [-Event] <Object> [[-Configuration] <hashtable>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses the C# bounded XML parser and retains the supplied record on the result.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-WinEvent -LogName System -MaxEvents 1 | ConvertFrom-WinEvent
```

## PARAMETERS

### -Configuration

Event configuration hashtable.

```yaml
Type: System.Collections.Hashtable
DefaultValue: (Import-SecurityEventConfiguration)
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

### -Event

An event record with a ToXml method.

```yaml
Type: System.Object
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: true
    ValueFromPipeline: true
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

### System.Object

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
