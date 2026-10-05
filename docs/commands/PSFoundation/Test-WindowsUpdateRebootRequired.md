---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Test-WindowsUpdateRebootRequired
---

# Test-WindowsUpdateRebootRequired

## SYNOPSIS

Returns $true if a reboot is pending after a Windows update install.

## SYNTAX

### __AllParameterSets

```
Test-WindowsUpdateRebootRequired [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads the native Windows Update system information reboot flag. This check does not scan for updates or restart the computer.

## EXAMPLES

### Example 1

```powershell
if (Test-WindowsUpdateRebootRequired) { Restart-Computer }
```

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Boolean

## NOTES

## RELATED LINKS
