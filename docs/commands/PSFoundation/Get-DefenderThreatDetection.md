---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-DefenderThreatDetection
---

# Get-DefenderThreatDetection

## SYNOPSIS

Retrieves Microsoft Defender threat detections, optionally filtered by date.

## SYNTAX

### __AllParameterSets

```
Get-DefenderThreatDetection [[-Date] <Object>] [[-OutputPath] <string>] [[-OutputFormat] <string>]
 [-IncludeURLs] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Queries detection records through the Windows Defender CIM provider.
 -Date sets a cutoff -- only detections with an
InitialDetectionTime on or after that point are returned.
 -OutputPath and
-OutputFormat control whether results are printed to the terminal or written
to a file (TXT or JSON).

## EXAMPLES

### EXAMPLE 1

```powershell
Get-DefenderThreatDetection
```

Prints all threat detections to the terminal.

### EXAMPLE 2

```powershell
Get-DefenderThreatDetection -Date '2026-04-01' -OutputPath '.\detections.json' -OutputFormat JSON
```

Writes detections since April 1st 2026 as JSON.

### EXAMPLE 3

```powershell
Get-DefenderThreatDetection -IncludeURLs -OutputPath '.\detections.json' -OutputFormat JSON
```

Writes detections as JSON, each augmented with a ThreatDescriptionURL.

## PARAMETERS

### -Date

Cutoff date for detections.
 Accepts any value that Get-Date can parse
(string, DateTime, etc.).
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

### -IncludeURLs

Augment each detection with a ThreatDescriptionURL property pointing to the
official Microsoft threat encyclopedia entry.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -OutputFormat

Output format: TXT (Formatted-List) or JSON.
 Defaults to TXT.

```yaml
Type: System.String
DefaultValue: TXT
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
  Position: 1
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
