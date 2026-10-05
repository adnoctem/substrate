---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Test-Elevation
---

# Test-Elevation

## SYNOPSIS

Test-Elevation - Returns whether the current process has administrator privileges.

## SYNTAX

### __AllParameterSets

```
Test-Elevation [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Checks the Windows principal of the current identity for membership in the
built-in Administrators role.
Returns $true when the process holds an
elevated (administrator) token, $false otherwise.

This is a pure predicate: it makes no changes and never elevates.
Use
Request-AdministratorPrivilege to actually elevate.

## EXAMPLES

### Example 1

```powershell
if (-not (Test-Elevation)) { throw 'Run me elevated.' }
```

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Boolean

## NOTES

## RELATED LINKS
