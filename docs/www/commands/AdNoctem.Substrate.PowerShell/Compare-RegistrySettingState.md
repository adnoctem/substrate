---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Compare-RegistrySettingState
---

# Compare-RegistrySettingState

## SYNOPSIS

Previews differences between selected registry values and desired state.

## SYNTAX

### __AllParameterSets

```
Compare-RegistrySettingState [-Settings] <Object[]> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns Before, After, Changed and Action for each setting without writes.
Comparison includes existence, value type and ordered, case-sensitive data.
Only selected values are compared; keys and unrelated values are untouched.

## EXAMPLES

### Example 1

```powershell
Compare-RegistrySettingState -Settings $snapshot
```

## PARAMETERS

### -Settings

Detailed snapshots or desired settings with Path, Name, Type and Preferred.
Exists = $false requests removal.
View defaults to the process view.

```yaml
Type: System.Object[]
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: true
    ValueFromPipeline: true
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

### System.Object

### System.Object

### System.Object

### System.Object

### System.Object

### System.Object

### System.Object

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
