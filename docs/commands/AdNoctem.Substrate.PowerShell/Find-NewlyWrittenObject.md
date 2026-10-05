---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Find-NewlyWrittenObject
---

# Find-NewlyWrittenObject

## SYNOPSIS

Finds files written near a point in time (e.g. around a Defender detection).

## SYNTAX

### __AllParameterSets

```
Find-NewlyWrittenObject [[-Date] <Object>] [[-Before] <int>] [[-After] <int>] [[-Path] <string>]
 [[-OutputPath] <string>] [[-OutputFormat] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Recursively scans C:\ (or a custom path) for files whose LastWriteTime falls
within a configurable window around the supplied -Date.
 Designed to help
identify artifacts dropped by malware at the time of a Defender alert.
Results can be printed to the terminal or exported as TXT / JSON.

## EXAMPLES

### EXAMPLE 1

```powershell
Find-NewlyWrittenObject -Date '2026-04-30 10:15'
```

Searches for files written between 08:15 and 11:15 on 2026-04-30.

### EXAMPLE 2

```powershell
Find-NewlyWrittenObject -Date '2026-04-30' -Before 4 -After 2 -OutputPath '.\artifacts.json' -OutputFormat JSON
```

Wider window, exported as JSON.

## PARAMETERS

### -After

Number of hours after the anchor date to include.
 Defaults to 1.

```yaml
Type: System.Int32
DefaultValue: 1
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 2
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Before

Number of hours before the anchor date to include.
 Defaults to 2.

```yaml
Type: System.Int32
DefaultValue: 2
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Date

Anchor date/time.
 Accepts any value that Get-Date can parse (string,
DateTime, etc.).
 Defaults to right now.

```yaml
Type: System.Object
DefaultValue: (Get-Date)
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
HelpMessage: ''
```

### -OutputFormat

Output format: TXT (Formatted custom table) or JSON.
 Defaults to TXT.

```yaml
Type: System.String
DefaultValue: TXT
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 5
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -OutputPath

File path to write results to.
 When omitted, results are printed to the
terminal.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 4
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Path

Root path to search.
 Defaults to the system drive (C:\).

```yaml
Type: System.String
DefaultValue: '"$env:SystemDrive\"'
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 3
  IsRequired: false
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

### System.Object

## NOTES

## RELATED LINKS
