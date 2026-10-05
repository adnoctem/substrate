---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-OfficeDeploymentToolSourceAvailability
---

# Test-OfficeDeploymentToolSourceAvailability

## SYNOPSIS

Checks whether the reviewed Microsoft ODT download is reachable.

## SYNTAX

### __AllParameterSets

```
Test-OfficeDeploymentToolSourceAvailability [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Sends a HEAD request to Resolve-OfficeDeploymentToolSource's URI.
Downloads
no executable and does not establish publisher trust or installation state.
Install-OfficeDeploymentTool verifies the downloaded and extracted files.

## EXAMPLES

### Example 1

```powershell
Test-OfficeDeploymentToolSourceAvailability
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
