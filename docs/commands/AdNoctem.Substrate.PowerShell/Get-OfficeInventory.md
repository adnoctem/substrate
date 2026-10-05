---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OfficeInventory
---

# Get-OfficeInventory

## SYNOPSIS

Reads Office registrations without launching Office or Windows Installer.

## SYNTAX

### __AllParameterSets

```
Get-OfficeInventory [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Inspects both machine registry views and preserves incomplete or conflicting
evidence.
Language completeness and shell language are not inferred from
ClientCulture alone.
Unknown properties cannot establish compliance.
Installed build prefers documented Click-to-Run inventory, falling back to
agreeing Version values on every active per-product resource when the
documented key or value is absent.
VersionSource and Evidence identify the
observation used; telemetry never establishes the installed build.
AppPathEvidence retains registered targets and present, missing or uncertain
filesystem observations.
Confirmed missing references alone do not block a
clean inventory.
Discovery never deletes stale registrations.

## EXAMPLES

### Example 1

```powershell
Get-OfficeInventory
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
