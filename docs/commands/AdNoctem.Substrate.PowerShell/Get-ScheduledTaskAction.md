---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-ScheduledTaskAction
---

# Get-ScheduledTaskAction

## SYNOPSIS

Returns structured scheduled task action data for all registered tasks.

## SYNTAX

### __AllParameterSets

```
Get-ScheduledTaskAction [-SuspiciousOnly] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Enumerates every scheduled task via Get-ScheduledTask and expands each
task's Actions collection into a flat list of [PSCustomObject] records
with TaskName, TaskPath, State, Execute, and Arguments properties.
Use -SuspiciousOnly to filter to known execution-host paths (powershell,
cmd, wscript, cscript, mshta, rundll32, regsvr32, InstallUtil).

## EXAMPLES

### Example 1

```powershell
Get-ScheduledTaskAction | Export-Csv .\ScheduledTasks-Actions.csv -NoTypeInformation
```

### Example 2

```powershell
Get-ScheduledTaskAction -SuspiciousOnly
```

## PARAMETERS

### -SuspiciousOnly

Only return actions with an Execute path matching common scripting and
LOLBin hosts.

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

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
