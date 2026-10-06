---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-PrintDevice
---

# Get-PrintDevice

## SYNOPSIS

Gets installed local print devices.

## SYNTAX

### __AllParameterSets

```
Get-PrintDevice [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses the PrintManagement module when available and enriches results with
default-printer state from Win32_Printer.
If PrintManagement is
unavailable or fails, falls back to Win32_Printer directly.

## EXAMPLES

### Example 1

```powershell
Get-PrintDevice
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

Discovery uses native CIM. Printer fallback errors remain available through verbose output instead of being interpreted as an empty inventory.

## RELATED LINKS
