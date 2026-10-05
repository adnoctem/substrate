---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-FileLockProcess
---

# Get-FileLockProcess

## SYNOPSIS

Get-FileLockProcess - Finds the process(es) holding a file open.

## SYNTAX

### __AllParameterSets

```
Get-FileLockProcess [-FilePath] <string[]> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses the Windows Restart Manager API (RmStartSession, RmRegisterResources,
RmGetList - the same mechanism Windows itself uses for "this file is in
use by X" during updates/uninstalls) to determine which running
process(es) hold a lock on the specified file.

Returns one structured result per file with a Lockers collection carrying
the process ID, process name, the Restart Manager application name and
type, session ID, and restartable flag.

## EXAMPLES

### Example 1

```powershell
Get-FileLockProcess -FilePath 'C:\Temp\locked.dat'
```

## PARAMETERS

### -FilePath

Path(s) of the file(s) to check.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

### System.String

### System.String

### System.String

### System.String

### System.String

### System.String

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
