---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-Win32Program
---

# Get-Win32Program

## SYNOPSIS

Returns normalized Win32 program inventory from Windows uninstall registry keys.

## SYNTAX

### __AllParameterSets

```
Get-Win32Program [[-Name] <string>] [-IncludeSystemComponent] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads machine-wide 64-bit, machine-wide 32-bit and current-user uninstall
keys using winkit registry helpers, and returns normalized program
metadata suitable for lifecycle operations.

## EXAMPLES

### Example 1

```powershell
Get-Win32Program
```

Lists visible Win32 programs registered on the machine and current user.

### Example 2

```powershell
Get-Win32Program -Name '*OneDrive*' -IncludeSystemComponent
```

Returns OneDrive-related uninstall entries, including hidden system
component registrations.

## PARAMETERS

### -IncludeSystemComponent

Include entries marked with SystemComponent=1.
These are normally hidden
from user-facing uninstall inventory and are excluded by default.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: Named
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Name

Optional wildcard DisplayName filter.
When omitted, all visible Win32
uninstall registrations are returned.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

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
