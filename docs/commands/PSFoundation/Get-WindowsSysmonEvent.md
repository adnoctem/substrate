---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-WindowsSysmonEvent
---

# Get-WindowsSysmonEvent

## SYNOPSIS

Queries Sysmon telemetry if the channel is available.

## SYNTAX

### __AllParameterSets

```
Get-WindowsSysmonEvent [[-StartTime] <datetime>] [[-EndTime] <datetime>] [[-Id] <int[]>]
 [[-ComputerName] <string[]>] [[-MaxEvents] <int>] [[-Configuration] <hashtable>]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Wraps Get-WindowsEventByDefinition for the Sysmon group with
-SkipMissingChannel enabled by default (Sysmon is optional).

## EXAMPLES

### EXAMPLE 1

```powershell
Get-WindowsSysmonEvent -Id 1
```

Returns Sysmon process creation events.

### EXAMPLE 2

```powershell
Get-WindowsSysmonEvent -Id 3, 22
```

Returns Sysmon network connection and DNS query events.

## PARAMETERS

### -ComputerName

Target remote computer(s).

```yaml
Type: System.String[]
DefaultValue: ''
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
HelpMessage: ''
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
  Position: 5
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
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
HelpMessage: ''
```

### -Id

Specific event IDs to return.
Defaults to all Sysmon group IDs.

```yaml
Type: System.Int32[]
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

### -MaxEvents

Maximum events to return.

```yaml
Type: System.Int32
DefaultValue: 0
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
HelpMessage: ''
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
HelpMessage: ''
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
