---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OSBuildNumber
---

# Get-OSBuildNumber

## SYNOPSIS

Returns the Windows build number as an integer.

## SYNTAX

### __AllParameterSets

```
Get-OSBuildNumber [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads CurrentBuild from HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion.
This is a single cheap registry read - far faster than Get-CimInstance
or any WMI-based approach.
 Returns e.g.
22621 (22H2), 22631 (23H2).

## EXAMPLES

### Example 1

```powershell
Get-OSBuildNumber
22621
```

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
