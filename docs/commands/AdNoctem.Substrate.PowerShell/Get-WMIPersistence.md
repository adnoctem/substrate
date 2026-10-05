---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-WMIPersistence
---

# Get-WMIPersistence

## SYNOPSIS

Enumerates WMI subscription-based persistence.

## SYNTAX

### __AllParameterSets

```
Get-WMIPersistence [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Queries the root\subscription namespace for __EventFilter,
CommandLineEventConsumer, and __FilterToConsumerBinding instances.
Returns an object with three properties: EventFilters, CommandLineConsumers,
and Bindings, each an array of the corresponding WMI objects.
Returns $null when no subscriptions exist.

## EXAMPLES

### Example 1

```powershell
$wmi = Get-WMIPersistence
$wmi.EventFilters | Format-List
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
