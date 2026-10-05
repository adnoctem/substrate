---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-RegistryValue
---

# Get-RegistryValue

## SYNOPSIS

Reads a named value from a registry key.

## SYNTAX

### __AllParameterSets

```
Get-RegistryValue [-Path] <string> [[-Name] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Opens the registry key at the given path and returns the data stored in
the named value.
Returns $null when either the key or the value does not
exist.

## EXAMPLES

### Example 1

```powershell
Get-RegistryValue -Path 'HKLM:\Software\MyApp' -Name 'Version'
```

## PARAMETERS

### -Name

Name of the value to read; omit to read the default (unnamed) value

```yaml
Type: System.String
DefaultValue: "''"
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
HelpMessage: ""
```

### -Path

Registry path, e.g.
'HKLM:\Software\MyApp'

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
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
