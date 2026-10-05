---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-DefaultApp
---

# Get-DefaultApp

## SYNOPSIS

Retrieves the current user's default application command for an extension.

## SYNTAX

### __AllParameterSets

```
Get-DefaultApp [-FileExtension] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Resolves the UserChoice ProgId and its registered open command without executing it.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-DefaultApp -FileExtension '.txt'
```

## PARAMETERS

### -FileExtension

File extension including its leading dot.

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

### System.String

## NOTES

## RELATED LINKS
