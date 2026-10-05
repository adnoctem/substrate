---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-RegistryValue
---

# Test-RegistryValue

## SYNOPSIS

Returns $true if the named registry value exists, $false otherwise.

## SYNTAX

### __AllParameterSets

```
Test-RegistryValue [-Path] <string> [[-Name] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Checks both that the parent key exists and that the named value is
present on that key.
 Never emits errors, making it ideal for
conditional guards before reading or removing values.

## EXAMPLES

### Example 1

```powershell
if (Test-RegistryValue 'HKLM:\Software\MyApp' -Name 'Version') { ... }
```

## PARAMETERS

### -Name

Name of the value to test; omit to test the default (unnamed) value

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
HelpMessage: ''
```

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

### System.Boolean

## NOTES

## RELATED LINKS
