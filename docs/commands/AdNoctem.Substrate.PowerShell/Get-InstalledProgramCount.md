---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-InstalledProgramCount
---

# Get-InstalledProgramCount

## SYNOPSIS

Counts visible Win32 programs registered in the Windows uninstall registry keys.

## SYNTAX

### __AllParameterSets

```
Get-InstalledProgramCount [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses Get-Win32Program to count unique, user-visible classic Win32
application registrations from HKLM 64-bit, HKLM 32-bit, and HKCU
uninstall locations.
Registry entries marked as SystemComponent are
excluded by default through the inventory helper.

## EXAMPLES

### Example 1

```powershell
Get-InstalledProgramCount
```

Returns the number of registered Win32 programs visible to normal
uninstall inventory views.

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
