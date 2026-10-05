---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OutlookRepairToolInfo
---

# Get-OutlookRepairToolInfo

## SYNOPSIS

Reads repair-tool metadata and identifies supported ScanPST file targeting.

## SYNTAX

### __AllParameterSets

```
Get-OutlookRepairToolInfo [-LiteralPath] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Inspects an existing executable without launching it.
Only ScanPST from
the Office 16 family at build 16.0.10325.20082 or later is classified as
supporting the documented file argument and rescan execution mode.
Older, unknown, and explicitly selected other executables remain interactive.
SupportsFileArgument is a conservative inference from the executable's
version resource, not a runtime capability probe.
MSI and Click-to-Run
file versions can differ; a false value selects the interactive fallback.
The directory name alone is not evidence of command-line support.

## EXAMPLES

### Example 1

```powershell
Get-OutlookRepairToolInfo -LiteralPath 'C:\Program Files\Microsoft Office\root\Office16\SCANPST.EXE'
```

## PARAMETERS

### -LiteralPath

Literal path to the repair executable, including a legacy explicit override.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
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
