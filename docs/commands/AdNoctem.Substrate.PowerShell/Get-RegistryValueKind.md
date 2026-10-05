---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-RegistryValueKind
---

# Get-RegistryValueKind

## SYNOPSIS

Gets the exact Microsoft.Win32.RegistryValueKind of a registry value.

## SYNTAX

### __AllParameterSets

```
Get-RegistryValueKind [-Path] <string> [[-Name] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Because the PS provider cmdlets (Get-ItemProperty / Get-ItemPropertyValue)
do not expose the raw RegistryValueKind, this function uses a lightweight
The .NET read solely for type inspection.
 It is useful when you need to
distinguish, e.g., String from ExpandString.
Returns $null if the key or value does not exist.

## EXAMPLES

### Example 1

```powershell
Get-RegistryValueKind -Path 'HKLM:\Software\MyApp' -Name 'PathVar'
ExpandString
```

## PARAMETERS

### -Name

Name of the value to inspect; omit to inspect the default (unnamed) value

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

### Microsoft.Win32.RegistryValueKind

## NOTES

## RELATED LINKS
