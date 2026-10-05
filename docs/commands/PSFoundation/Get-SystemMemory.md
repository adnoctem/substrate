---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-SystemMemory
---

# Get-SystemMemory

## SYNOPSIS

Returns physical memory statistics - total, available, used, and load
percentage.

## SYNTAX

### __AllParameterSets

```
Get-SystemMemory [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses kernel32!GlobalMemoryStatusEx via P/Invoke (no CIM/WMI overhead).
Returns an object with human-readable GiB values and the raw bytes.

## EXAMPLES

### Example 1

```powershell
Get-SystemMemory
```

### Example 2

```powershell
Get-SystemMemory | Format-List
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
