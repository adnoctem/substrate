---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-ScanDevice
---

# Get-ScanDevice

## SYNOPSIS

Gets WIA-compatible scan devices.

## SYNTAX

### __AllParameterSets

```
Get-ScanDevice [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Enumerates Windows Image Acquisition device metadata through
WIA.DeviceManager.
This is discovery-only; Windows does not provide a
universal default scanner setting equivalent to the default printer.

## EXAMPLES

### Example 1

```powershell
Get-ScanDevice
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

WIA discovery runs on an owned STA worker and releases COM references before completion. Cancellation cannot interrupt an in-flight driver call.

## RELATED LINKS
