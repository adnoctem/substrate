---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-WindowsUpdate
---

# Get-WindowsUpdate

## SYNOPSIS

Lists available Windows updates.

## SYNTAX

### __AllParameterSets

```
Get-WindowsUpdate [[-Category] <string[]>] [[-KBArticleID] <string>] [[-Title] <string>]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Queries the native Windows Update Agent for available, visible updates and filters by category,
KB article ID, or title wildcard. Scanning may contact the update service configured by Windows policy.
No separate PSWindowsUpdate module is required. Installation and hiding are separate operations.

## EXAMPLES

### Example 1

```powershell
Get-WindowsUpdate -Category Security, Critical
```

### Example 2

```powershell
Get-WindowsUpdate -KBArticleID 'KB5021233'
```

## PARAMETERS

### -Category

One or more update categories to include.
Common values: Security,
Critical, Definition, Update, UpdateRollup, Drivers.

```yaml
Type: System.String[]
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

### -Title

Wildcard title filter forwarded to -Title.

```yaml
Type: System.String
DefaultValue: ''
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
