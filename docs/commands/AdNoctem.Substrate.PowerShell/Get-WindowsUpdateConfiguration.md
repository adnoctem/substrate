---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-WindowsUpdateConfiguration
---

# Get-WindowsUpdateConfiguration

## SYNOPSIS

Returns current Windows Update service configuration.

## SYNTAX

### __AllParameterSets

```
Get-WindowsUpdateConfiguration [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads the effective Windows Update configuration through the shared registry API.
Returns an object with ServerSelection,
ServiceID, TargetGroup, and NotificationLevel properties.

## EXAMPLES

### Example 1

```powershell
Get-WindowsUpdateConfiguration
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
