---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OSProductName
---

# Get-OSProductName

## SYNOPSIS

Returns the full Windows product name string.

## SYNTAX

### __AllParameterSets

```
Get-OSProductName [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads ProductName from HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion.
Returns e.g.
"Windows 11 Pro" or "Windows 10 Enterprise".

## EXAMPLES

### Example 1

```powershell
Get-OSProductName
```

Windows 11 Pro

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
