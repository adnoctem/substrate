---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Invoke-ComGarbageCollection
---

# Invoke-ComGarbageCollection

## SYNOPSIS

Runs final COM cleanup garbage collection passes.

## SYNTAX

### __AllParameterSets

```
Invoke-ComGarbageCollection [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Forces garbage collection and waits for pending finalizers.
This is useful
after releasing Office COM references so Outlook can close PST files and
exit cleanly when requested.

## EXAMPLES

### Example 1

```powershell
Invoke-ComGarbageCollection
```

## PARAMETERS

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
