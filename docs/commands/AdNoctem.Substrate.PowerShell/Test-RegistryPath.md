---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-RegistryPath
---

# Test-RegistryPath

## SYNOPSIS

Returns $true if the registry path exists, $false otherwise.

## SYNTAX

### __AllParameterSets

```
Test-RegistryPath [-Path] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

A safe, non-terminating existence check for registry keys.
Unlike
Resolve-RegistryPath, this function never emits errors - it simply
returns a boolean.
Suitable for use in conditionals and idempotency
guards.

## EXAMPLES

### Example 1

```powershell
if (Test-RegistryPath 'HKLM:\Software\MyApp') { ... }
```

## PARAMETERS

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

### System.Boolean

## NOTES

## RELATED LINKS
