---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-SystemFileIntegrity
---

# Test-SystemFileIntegrity

## SYNOPSIS

Runs sfc /verifyOnly and returns a structured integrity verdict.

## SYNTAX

### __AllParameterSets

```
Test-SystemFileIntegrity [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Runs a synchronous, read-only system file integrity check
(sfc.exe /verifyOnly) and synthesizes a structured verdict from the
output, the exit code, and the tail of the CBS log (the source of truth).

The function intentionally runs /verifyOnly - a full scan-and-repair
pass (/scannow) has significant runtime and disruption cost and must
only ever run as a deliberate, separately-named action.

The check requires an elevated process for sfc itself to run; the
function does not perform any elevation.

## EXAMPLES

### Example 1

```powershell
Test-SystemFileIntegrity
```

Status  : Completed
Detail  : no integrity violations
ExitCode: 0

## PARAMETERS

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
