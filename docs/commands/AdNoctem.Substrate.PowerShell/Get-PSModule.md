---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-PSModule
---

# Get-PSModule

## SYNOPSIS

Exports installed PowerShell modules to the pipeline or a JSON file.

## SYNTAX

### __AllParameterSets

```
Get-PSModule [[-Path] <string>] [[-Name] <string>] [[-Scope] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Lists modules installed at the resolved scope path, filtered by name regex.
JSON exports always contain an array, including for zero or one module.
Discovers module manifests within the selected scope without importing those modules.

## EXAMPLES

### Example 1

```powershell
Get-PSModule
```

Returns all CurrentUser modules to the pipeline.

### Example 2

```powershell
Get-PSModule -Path ./modules.json -Name 'Pester'
```

Exports matching modules to modules.json.

## PARAMETERS

### -Name

Regex filter for module name.
Defaults to '*' (all).

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

### -Path

JSON output file path.
When omitted, modules are returned to the pipeline.

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

### -Scope

Module directory scope: CurrentUser (default) or AllUsers.

```yaml
Type: System.String
DefaultValue: "'CurrentUser'"
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

### System.Object

## NOTES

## RELATED LINKS
