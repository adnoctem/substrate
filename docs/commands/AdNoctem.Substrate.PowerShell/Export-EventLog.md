---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Export-EventLog
---

# Export-EventLog

## SYNOPSIS

Exports a named Windows event log to an .evtx file via wevtutil.

## SYNTAX

### __AllParameterSets

```
Export-EventLog [-LogName] <string> [-OutputPath] <string> [[-MissingLogPath] <string>]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Wraps wevtutil.exe epl.
If the log name does not exist, a warning
is recorded in -MissingLogPath (when supplied) and no error is thrown.
Designed for bulk log collection during IR triage.

## EXAMPLES

### Example 1

```powershell
Export-EventLog -LogName 'Security' -OutputPath '.\EVTX\Security.evtx'
```

## PARAMETERS

### -LogName

Full event log name, e.g.
'Security', 'Microsoft-Windows-PowerShell/Operational'.

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

### -MissingLogPath

When supplied and the log is not found, the missing log name is appended
to this text file so collectors can report what was unavailable.

```yaml
Type: System.String
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

### -OutputPath

Path for the exported .evtx file.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 1
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

### System.Object

## NOTES

## RELATED LINKS
