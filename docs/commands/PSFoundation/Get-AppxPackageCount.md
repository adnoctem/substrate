---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-AppxPackageCount
---

# Get-AppxPackageCount

## SYNOPSIS

Counts installed UPF AppX/MSIX packages for the current user.

## SYNTAX

### __AllParameterSets

```
Get-AppxPackageCount [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses Get-UPFAppxPackage to count installed AppX/MSIX packages while
excluding framework, resource, and bundle packages by default.
This keeps
fetch-style summaries focused on user-facing applications instead of
runtime dependencies.

## EXAMPLES

### Example 1

```powershell
Get-AppxPackageCount
```

Returns the current user's user-facing UPF AppX/MSIX package count.

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Int32

## NOTES

## RELATED LINKS
