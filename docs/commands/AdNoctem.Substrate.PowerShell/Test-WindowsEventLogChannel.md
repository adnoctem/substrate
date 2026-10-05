---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-WindowsEventLogChannel
---

# Test-WindowsEventLogChannel

## SYNOPSIS

Tests whether an event log channel exists on the local machine.

## SYNTAX

### __AllParameterSets

```
Test-WindowsEventLogChannel [-LogName] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses Get-WinEvent -ListLog to verify channel existence.
Returns $true
if the channel is available, $false otherwise.
No errors are thrown
for missing channels.

## EXAMPLES

### Example 1

```powershell
if (Test-WindowsEventLogChannel -LogName 'Microsoft-Windows-Sysmon/Operational') { ... }
```

## PARAMETERS

### -LogName

Full event log channel name, e.g.
'Security' or
'Microsoft-Windows-Sysmon/Operational'.

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

### System.Object

## NOTES

## RELATED LINKS
