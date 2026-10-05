---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Request-AdministratorPrivilege
---

# Request-AdministratorPrivilege

## SYNOPSIS

Relaunches the calling entry script through an explicit Windows UAC request.

## SYNTAX

### __AllParameterSets

```
Request-AdministratorPrivilege [[-ScriptPath] <string>] [[-BoundParameters] <IDictionary>]
 [[-ArgumentList] <Object[]>] [-IsElevatedRelaunch] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns immediately when elevated.
Otherwise forwards inert arguments to
the same PowerShell executable, waits and exits with the child's exit code.
Encoded arguments are not encrypted: never pass secrets in command arguments.

## EXAMPLES

### EXAMPLE 1

```powershell
Request-AdministratorPrivilege -BoundParameters $PSBoundParameters -ArgumentList $args -IsElevatedRelaunch:$Elevated
```

## PARAMETERS

### -ArgumentList

Positional arguments.
Only inert scalar values and arrays are accepted.

```yaml
Type: System.Object[]
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

### -BoundParameters

Named arguments including explicit switch values.
Elevated is reserved.

```yaml
Type: System.Collections.IDictionary
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

### -IsElevatedRelaunch

Loop guard passed from the caller's Elevated switch.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
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

### -ScriptPath

Entry script, defaulting to the caller's script path.

```yaml
Type: System.String
DefaultValue: $MyInvocation.PSCommandPath
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Void

## NOTES

## RELATED LINKS
