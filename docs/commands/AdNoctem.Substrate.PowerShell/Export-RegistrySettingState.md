---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Export-RegistrySettingState
---

# Export-RegistrySettingState

## SYNOPSIS

Exports registry setting definitions with Preferred set to current values.

## SYNTAX

### __AllParameterSets

```
Export-RegistrySettingState [-Settings] <Object[]> [[-View] <RegistryView>] [-Detailed]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads the current machine value for each registry setting object and
returns a reusable configuration snapshot.
The returned objects preserve
the original setting metadata, including Path, Name, Default, Type, Group,
Description, and build gates, but replace Preferred with the value found
in the registry.

Missing registry values are exported with Preferred = $null.
This keeps
the output compatible with existing -Config behavior while making absent
values explicit for later diffs.

## EXAMPLES

### Example 1

```powershell
Export-RegistrySettingState -Settings $taskbarSettings | ConvertTo-Json -Depth 3
```

Exports current taskbar registry values in the same schema consumed by
Configure-Taskbar.ps1 -Config.

## PARAMETERS

### -Detailed

Include versioned existence, type, raw value and registry view metadata
suitable for Compare-RegistrySettingState and Restore-RegistrySettingState.
ExpandString values are captured without expanding environment variables.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
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

### -Settings

Registry setting objects or hashtables with at least Path and Name
properties.
Additional properties are preserved.

```yaml
Type: System.Object[]
DefaultValue: ''
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
HelpMessage: ''
```

### -View

Default view for detailed snapshots.
A setting's View overrides this value.

```yaml
Type: Microsoft.Win32.RegistryView
DefaultValue: '[Microsoft.Win32.RegistryView]::Default'
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
