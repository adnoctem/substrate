---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-SystemInfo
---

# Get-SystemInfo

## SYNOPSIS

Returns a comprehensive system information snapshot (fetch-style).

## SYNTAX

### __AllParameterSets

```
Get-SystemInfo [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Assembles OS version, memory, disk, hostname, and uptime into a single
structured object.
 Disk data is resolved through physical disk
associations; other data sources use registry reads, .NET APIs, and
lightweight P/Invoke where possible.

## EXAMPLES

### Example 1

```powershell
Get-SystemInfo | Format-List
```

### Example 2

```powershell
Get-SystemInfo
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
