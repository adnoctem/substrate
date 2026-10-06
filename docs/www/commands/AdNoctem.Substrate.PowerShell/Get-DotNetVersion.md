---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-DotNetVersion
---

# Get-DotNetVersion

## SYNOPSIS

Returns the installed .NET runtimes, SDKs, and .NET Framework versions.

## SYNTAX

### __AllParameterSets

```
Get-DotNetVersion [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Enumerates three independent sources of .NET version information:

- dotnet CLI runtimes and SDKs (dotnet --list-runtimes / --list-sdks) — the
  full output of each command, or $null when the dotnet CLI is not installed.
- The classic .NET Framework version of the current runtime
  ([Environment]::Version).
- The full .NET Framework release enumeration from the NDP registry subtree
  (HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP), with each release
  number mapped to a friendly product name.

NOTE: the release-number-to-product mapping is a point-in-time data table
and must be reviewed when new .NET Framework versions ship (see the
mapping in DotNetManager and support.microsoft.com help article
318785 for the authoritative mapping).

## EXAMPLES

### Example 1

```powershell
Get-DotNetVersion | Format-List
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

CLI discovery resolves applications only; an alias or function named dotnet is not executed. Each executable reports its visible architecture. PowerShell retains historical labels/raw output, while the reusable Framework API reports the known minimum version established by release thresholds.

## RELATED LINKS
