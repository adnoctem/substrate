---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-UserInfo
---

# Get-UserInfo

## SYNOPSIS

Retrieves the effective Windows identity, administrator status and SID.

## SYNTAX

### __AllParameterSets

```
Get-UserInfo
```

## ALIASES

None.

## DESCRIPTION

Uses the C# identity manager and preserves the original hashtable and extra-argument handling.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-UserInfo
```

## PARAMETERS

## INPUTS

## OUTPUTS

### System.Collections.Hashtable

## NOTES

The compatibility function retains legacy argument binding, including ignored extra arguments. Domain reads and calculations execute in C#; provider failures remain errors rather than evidence of absent configuration.

## RELATED LINKS
