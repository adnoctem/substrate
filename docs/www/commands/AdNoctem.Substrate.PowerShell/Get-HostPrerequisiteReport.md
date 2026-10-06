---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-HostPrerequisiteReport
---

# Get-HostPrerequisiteReport

## SYNOPSIS

Explains whether the host satisfies an operation's prerequisites.

## SYNTAX

### __AllParameterSets

```
Get-HostPrerequisiteReport [[-MinBuild] <int>] [[-MaxBuild] <int>] [[-Edition] <string[]>]
 [[-Architecture] <string>] [[-RequiredModules] <hashtable>] [[-RequiredCommands] <string[]>]
 [[-RequiredServices] <hashtable>] [-RequireAdministrator] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns Applicable and a Checks array with expected/actual values, reason
codes and guidance.
Reports every supplied constraint without installing
modules, starting services, or elevating the process.
Discovery errors
become failed checks rather than hiding other prerequisite failures.

## EXAMPLES

### Example 1

```powershell
Get-HostPrerequisiteReport -MinBuild 22000 -RequireAdministrator -RequiredModules @{ Pester = '5.0.0' } -RequiredCommands 'winget.exe' -RequiredServices @{ wuauserv = 'Running' }
```

## PARAMETERS

### -Architecture

Required OS architecture: x86, x64 or Arm64, including under emulation.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 3
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Edition

Accepted Windows edition identifiers.

```yaml
Type: System.String[]
DefaultValue: ""
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
HelpMessage: ""
```

### -MaxBuild

Inclusive maximum Windows build.

```yaml
Type: System.Int32
DefaultValue: ""
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

### -MinBuild

Inclusive minimum Windows build.

```yaml
Type: System.Int32
DefaultValue: ""
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
HelpMessage: ""
```

### -RequireAdministrator

Require an elevated Windows identity.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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
HelpMessage: ""
```

### -RequiredCommands

Exact command names that must resolve in the current session.

```yaml
Type: System.String[]
DefaultValue: "@()"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 5
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -RequiredModules

Hashtable mapping exact module names to minimum versions.

```yaml
Type: System.Collections.Hashtable
DefaultValue: "@{}"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 4
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -RequiredServices

Hashtable mapping exact service names to required states (Running,
Stopped or Paused).
A null or empty state checks only existence.

```yaml
Type: System.Collections.Hashtable
DefaultValue: "@{}"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 6
    IsRequired: false
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
