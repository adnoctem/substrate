---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-PackageCount
---

# Get-PackageCount

## SYNOPSIS

Returns reusable Win32 and UPF AppX/MSIX package counts for system summaries.

## SYNTAX

### __AllParameterSets

```
Get-PackageCount [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Combines Get-InstalledProgramCount and Get-AppxPackageCount into a single
object with Programs, Appx, and Total properties.
This is intentionally
small and fast enough for fetch-style scripts that should not duplicate
registry or package inventory logic.

## EXAMPLES

### Example 1

```powershell
Get-PackageCount
```

Returns Programs, Appx, and Total package counts.

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
