---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-RegistryKey
---

# Get-RegistryKey

## SYNOPSIS

Retrieves metadata about a registry key.

## SYNTAX

### __AllParameterSets

```
Get-RegistryKey [-Path] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Opens the registry key at the given path and returns an object describing
its subkey names and value names.
Returns $null when the key does not
exist.

## EXAMPLES

### Example 1

```powershell
Get-RegistryKey -Path 'HKLM:\Software\Microsoft'
```

## PARAMETERS

### -Path

Registry path, e.g.
'HKLM:\Software\MyApp'

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
