---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-DefaultPrintDevice
---

# Get-DefaultPrintDevice

## SYNOPSIS

Gets the default local print device.

## SYNTAX

### __AllParameterSets

```
Get-DefaultPrintDevice [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Queries Win32_Printer for the printer where Default is true and returns
the same normalized object shape as Get-PrintDevice.

## EXAMPLES

### Example 1

```powershell
Get-DefaultPrintDevice
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
