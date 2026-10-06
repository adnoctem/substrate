---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Find-OutlookRepairTool
---

# Find-OutlookRepairTool

## SYNOPSIS

Finds installed ScanPST repair tools and their targeting capabilities.

## SYNTAX

### __AllParameterSets

```
Find-OutlookRepairTool [[-Name] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Searches Outlook installations, then application executables on PATH.
Returned paths use long names and include executable version metadata.
Legacy alternatives can be inspected explicitly with Get-OutlookRepairToolInfo.

## EXAMPLES

### Example 1

```powershell
Find-OutlookRepairTool -Name ScanPST
```

## PARAMETERS

### -Name

ScanPST is the only automatically discovered repair tool.

```yaml
Type: System.String
DefaultValue: "'ScanPST'"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

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
