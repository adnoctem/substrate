---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Resolve-WindowsEventMappedField
---

# Resolve-WindowsEventMappedField

## SYNOPSIS

Resolves an event field's semantic mapping.

## SYNTAX

### __AllParameterSets

```
Resolve-WindowsEventMappedField [-MapName] <string> [-Value] <Object> [[-Configuration] <hashtable>]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Retains integer and string key handling from the original command.

## EXAMPLES

### EXAMPLE 1

```powershell
Resolve-WindowsEventMappedField -MapName LogonType -Value 10
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
  Position: 2
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -MapName

Field map name.

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

### -Value

Field value.

```yaml
Type: System.Object
DefaultValue: ''
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
