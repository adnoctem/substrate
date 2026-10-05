---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Connect-Outlook
---

# Connect-Outlook

## SYNOPSIS

Connects to an Outlook COM application and MAPI namespace.

## SYNTAX

### __AllParameterSets

```
Connect-Outlook [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reuses a running Outlook instance when available, otherwise starts one,
then logs on to the MAPI namespace without prompting.

## EXAMPLES

### Example 1

```powershell
$context = Connect-Outlook
```

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
