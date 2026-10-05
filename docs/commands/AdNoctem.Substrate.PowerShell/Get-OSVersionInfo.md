---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OSVersionInfo
---

# Get-OSVersionInfo

## SYNOPSIS

Returns a complete snapshot of Windows version metadata from the registry.

## SYNTAX

### __AllParameterSets

```
Get-OSVersionInfo [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Performs a single Get-ItemProperty call against HKLM:\SOFTWARE\Microsoft\
Windows NT\CurrentVersion and returns all relevant fields in one object.
Far cheaper than calling the individual Get-OS* functions when you need
multiple values.

## EXAMPLES

### Example 1

```powershell
Get-OSVersionInfo
```

### Example 2

```powershell
Get-OSVersionInfo | Format-List
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
