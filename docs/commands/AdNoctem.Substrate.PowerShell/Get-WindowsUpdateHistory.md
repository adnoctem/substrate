---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-WindowsUpdateHistory
---

# Get-WindowsUpdateHistory

## SYNOPSIS

Returns installed Windows update history.

## SYNTAX

### __AllParameterSets

```
Get-WindowsUpdateHistory [[-Last] <int>] [[-KBArticleID] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads Windows Update Agent history without requiring PSWindowsUpdate.
Optionally filtered by -Last N recent entries
or a specific -KBArticleID.

## EXAMPLES

### Example 1

```powershell
Get-WindowsUpdateHistory -Last 10
```

### Example 2

```powershell
Get-WindowsUpdateHistory -KBArticleID 'KB5021233'
```

## PARAMETERS

### -KBArticleID

Filter to a specific KB article.

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

### -Last

Number of most recent history entries to return.

```yaml
Type: System.Int32
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
