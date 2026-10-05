---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-OSDisplayVersion
---

# Get-OSDisplayVersion

## SYNOPSIS

Returns the Windows feature-update display name (e.g. "22H2", "23H2").

## SYNTAX

### __AllParameterSets

```
Get-OSDisplayVersion [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads DisplayVersion from HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion.
Falls back to ReleaseId on older builds that lack DisplayVersion.

## EXAMPLES

### Example 1

```powershell
Get-OSDisplayVersion
23H2
```

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.String

## NOTES

## RELATED LINKS
