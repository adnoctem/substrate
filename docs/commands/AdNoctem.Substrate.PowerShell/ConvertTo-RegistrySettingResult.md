---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: ConvertTo-RegistrySettingResult
---

# ConvertTo-RegistrySettingResult

## SYNOPSIS

Creates operation results for registry setting definitions.

## SYNTAX

### __AllParameterSets

```
ConvertTo-RegistrySettingResult [-Settings] <Object[]> [[-Source] <string>] [-Undo] [-DryRun]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Converts registry setting objects into operation-result records suitable
for Write-OperationResultLog and -PassThru output.
In DryRun mode the
results describe planned SetValue or RemoveValue actions as Skipped with
Detail = DryRun.
In normal mode the helper reads the registry after the
script has run and marks each setting Completed when the target state is
present, Removed when an undo removal target is absent, or Failed when the
current state does not match the expected state.

This helper is intentionally conservative: it does not attempt to infer
whether a Completed value was newly changed or already correct.
Scripts
that need exact lifecycle states can still add explicit results during
their apply loop.

## EXAMPLES

### Example 1

```powershell
$results = ConvertTo-RegistrySettingResult -Settings $taskbarSettings -DryRun
Write-OperationResultLog -Results $results -ScriptName 'Configure-Taskbar'
```

Creates dry-run audit records for a registry-backed configuration script.

## PARAMETERS

### -DryRun

Build planned-operation results without reading final state.

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

Registry setting objects or hashtables with Path, Name, Preferred,
Default, Type, and Description properties.

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

### -Source

Source label for result objects.
Defaults to Registry.

```yaml
Type: System.String
DefaultValue: "'Registry'"
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

### -Undo

Build results for an undo operation, using Default as the target state.

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
