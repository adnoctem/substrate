---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-DefenderThreat
---

# Get-DefenderThreat

## SYNOPSIS

Retrieves the full Microsoft Defender threat catalog.

## SYNTAX

### __AllParameterSets

```
Get-DefenderThreat [[-OutputPath] <string>] [[-OutputFormat] <string>] [-IncludeURLs]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Queries threat records through the Windows Defender CIM provider.
 -OutputPath and -OutputFormat control whether results
are printed to the terminal or written to a file (TXT or JSON).

## EXAMPLES

### EXAMPLE 1

```powershell
Get-DefenderThreat
```

Prints the threat catalog to the terminal.

### EXAMPLE 2

```powershell
Get-DefenderThreat -OutputPath '.\threats.json' -OutputFormat JSON
```

Writes the threat catalog as JSON.

### EXAMPLE 3

```powershell
Get-DefenderThreat -IncludeURLs -OutputFormat JSON
```

Prints the threat catalog as JSON, each augmented with a ThreatDescriptionURL.

## PARAMETERS

### -IncludeURLs

Augment each threat with a ThreatDescriptionURL property pointing to the
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
  Position: 1
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
  Position: 0
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
