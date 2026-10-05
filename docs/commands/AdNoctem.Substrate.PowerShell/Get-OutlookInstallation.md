---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OutlookInstallation
---

# Get-OutlookInstallation

## SYNOPSIS

Finds local Outlook installation directories.

## SYNTAX

### __AllParameterSets

```
Get-OutlookInstallation [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Discovers common Microsoft Office and Microsoft 365 installation roots
that may contain Outlook.exe or Outlook data-file repair tools such as
ScanPST.exe.
The function checks App Paths registry
entries first, then common Office directory layouts under Program Files.

## EXAMPLES

### Example 1

```powershell
Get-OutlookInstallation
```

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
