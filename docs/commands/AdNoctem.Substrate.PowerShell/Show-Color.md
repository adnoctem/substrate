---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Show-Color
---

# Show-Color

## SYNOPSIS

Displays the available console colors.

## SYNTAX

### __AllParameterSets

```
Show-Color [[-ArgumentList] <Object[]>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Displays each ConsoleColor name in its own color.
Standard PowerShell parameters
such as Verbose, ErrorAction and InformationAction are discoverable through tab completion.
ArgumentList accepts unused arguments for compatibility with the original function.

## EXAMPLES

### EXAMPLE 1

```powershell
Show-Color
```

### EXAMPLE 2

```powershell
Show-Color -Verbose
```

## PARAMETERS

### -ArgumentList

Extra arguments accepted and ignored for compatibility.
They do not filter the colors.

```yaml
Type: System.Object[]
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: true
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

## RELATED LINKS
