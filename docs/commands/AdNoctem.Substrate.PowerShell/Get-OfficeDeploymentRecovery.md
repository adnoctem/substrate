---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OfficeDeploymentRecovery
---

# Get-OfficeDeploymentRecovery

## SYNOPSIS

Reads a protected Office recovery record without resuming it.

## SYNTAX

### __AllParameterSets

```
Get-OfficeDeploymentRecovery [-RunId] <string> [[-LogRoot] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses the native journal reader to validate schema, identity, scope and fingerprints.
A checkpoint does not authorize replay.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-OfficeDeploymentRecovery -RunId $result.RunId
```

## PARAMETERS

### -LogRoot

Protected local recovery directory.

```yaml
Type: System.String
DefaultValue: (Join-Path $env:ProgramData 'PSFoundation-Office')
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

### -RunId

Run identifier returned by an Office operation.

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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
