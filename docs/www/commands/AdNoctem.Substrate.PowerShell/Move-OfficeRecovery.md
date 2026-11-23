---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Move-OfficeRecovery
---

# Move-OfficeRecovery

## SYNOPSIS

Retires a protected Office recovery journal and preserves its evidence.

## SYNTAX

### __AllParameterSets

```
Move-OfficeRecovery [-RunId] <string> [-Reason] <string> [[-LogRoot] <string>] [-WhatIf] [-Confirm]
```

## ALIASES

None.

## DESCRIPTION

Validates the journal's run identity, machine identity, schema, and recorded fingerprints before changing anything.
Writes an atomic `RunId.abandoned.json` audit record with the retirement reason, current inventory fingerprint, and execution metadata.
Moves the journal `.json`, its `.jsonl` log, and related run artifacts into the `Abandoned` subdirectory under the same log root.
Retirement preserves evidence; it does not roll back Office or execute ODT. A retired journal cannot be resumed.

## EXAMPLES

### EXAMPLE 1

```powershell
Move-OfficeRecovery -RunId $result.RunId -Reason 'Superseded by a reviewed replacement plan'
```

### EXAMPLE 2

```powershell
Move-OfficeRecovery -RunId $result.RunId -Reason 'Stale verification evidence' -WhatIf
```

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - cf
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

### -LogRoot

Protected local recovery directory.

```yaml
Type: System.String
DefaultValue: (Join-Path $env:ProgramData 'winkit-Office')
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

### -Reason

Operator reason recorded in the retirement audit.

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

### -RunId

Run identifier returned by an Office operation.

```yaml
Type: System.String
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

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - wi
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

A retirement operation reopens and validates the protected journal before changing files. The audit record remains in the log root while the journal and log move to `Abandoned`. Recovery refuses a retired run with `RetiredRecoveryRecord`; there is no fallback reader.

## RELATED LINKS
