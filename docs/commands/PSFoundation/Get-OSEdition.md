---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-OSEdition
---

# Get-OSEdition

## SYNOPSIS

Returns the Windows edition SKU (e.g. "Professional", "Enterprise").

## SYNTAX

### __AllParameterSets

```
Get-OSEdition [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads EditionID from HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion.

## EXAMPLES

### Example 1

```powershell
Get-OSEdition
Professional
```

## PARAMETERS

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
