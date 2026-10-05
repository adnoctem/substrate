---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Remove-ComObject
---

# Remove-ComObject

## SYNOPSIS

Releases one or more COM objects.

## SYNTAX

### __AllParameterSets

```
Remove-ComObject [[-InputObject] <Object[]>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Wraps Marshal.ReleaseComObject with null checks and best-effort error
handling so scripts can safely clean up Outlook and Office interop
objects from finally blocks.

## EXAMPLES

### Example 1

```powershell
Remove-ComObject $items $folder
```

## PARAMETERS

### -InputObject

COM objects to release. Accepts multiple positional arguments; null values are ignored.

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
