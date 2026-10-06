---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-WindowsLogonEvent
---

# Get-WindowsLogonEvent

## SYNOPSIS

Queries and normalises logon-related security events.

## SYNTAX

### __AllParameterSets

```
Get-WindowsLogonEvent [[-StartTime] <datetime>] [[-EndTime] <datetime>] [[-Id] <int[]>]
 [[-LogonType] <int[]>] [[-ComputerName] <string[]>] [[-MaxEvents] <int>]
 [[-Configuration] <hashtable>] [-IncludeSystem] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Wraps Get-WindowsEventByDefinition for the Logon group.
Supports
filtering by event ID and logon type.
Suppresses noisy system accounts
by default unless -IncludeSystem is supplied.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-WindowsLogonEvent -Id 4625
```

Returns failed logon events from the past 24 hours.

### EXAMPLE 2

```powershell
Get-WindowsLogonEvent -Id 4624 -LogonType 10
```

Returns successful RDP logons.

### EXAMPLE 3

```powershell
Get-WindowsLogonEvent -Id 4624, 4800, 4801 -LogonType 2, 7
```

Returns local console and unlock activity.

## PARAMETERS

### -ComputerName

Target remote computer(s).

```yaml
Type: System.String[]
DefaultValue: ""
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

### -Configuration

Configuration hashtable.
Defaults to cached.

```yaml
Type: System.Collections.Hashtable
DefaultValue: (Import-SecurityEventConfiguration)
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

### -EndTime

Latest event timestamp.
Defaults to now.

```yaml
Type: System.DateTime
DefaultValue: (Get-Date)
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

### -Id

Specific event IDs to return.
Defaults to all Logon group IDs.

```yaml
Type: System.Int32[]
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

### -IncludeSystem

Include system and machine accounts normally suppressed.

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
HelpMessage: ""
```

### -LogonType

Filter by numeric logon type(s), e.g.
10 for RDP.

```yaml
Type: System.Int32[]
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

### -MaxEvents

Maximum events to return.

```yaml
Type: System.Int32
DefaultValue: 0
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

### -StartTime

Earliest event timestamp.
Defaults to 24 hours ago.

```yaml
Type: System.DateTime
DefaultValue: (Get-Date).AddDays(-1)
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
