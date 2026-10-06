---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-Hostname
---

# Get-Hostname

## SYNOPSIS

Returns the computer hostname.

## SYNTAX

### __AllParameterSets

```
Get-Hostname [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses [System.Net.Dns]::GetHostName() and resolves it to a fully-qualified
domain name when joined to a domain.
Returns both the short hostname and,
if different, the FQDN.

## EXAMPLES

### Example 1

```powershell
Get-Hostname
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
