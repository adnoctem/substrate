---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-SystemUptime
---

# Get-SystemUptime

## SYNOPSIS

Returns the system uptime (time since last boot).

## SYNTAX

### __AllParameterSets

```
Get-SystemUptime [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses kernel32!GetTickCount64 via P/Invoke for a non-wrapping, high-
precision uptime value - no CIM/WMI overhead.
 Returns the raw tick
count, total milliseconds, and a human-readable breakdown.

## EXAMPLES

### Example 1

```powershell
Get-SystemUptime
```

### Example 2

```powershell
Get-SystemUptime | Format-List
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
