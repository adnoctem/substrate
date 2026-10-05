---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Resolve-OfficeDeploymentToolSource
---

# Resolve-OfficeDeploymentToolSource

## SYNOPSIS

Returns the reviewed Microsoft ODT download location without network I/O.

## SYNTAX

### __AllParameterSets

```
Resolve-OfficeDeploymentToolSource [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Downloads are versioned.
Acquisition validates Authenticode trust before
extraction; URL metadata and hashes alone do not establish publisher trust.

## EXAMPLES

### Example 1

```powershell
Resolve-OfficeDeploymentToolSource
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
