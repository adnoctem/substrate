---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-MSStoreUpdate
---

# Get-MSStoreUpdate

## SYNOPSIS

Lists available Microsoft Store app updates.

## SYNTAX

### __AllParameterSets

```
Get-MSStoreUpdate [[-PackageFamilyName] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses the WinRT AppInstallManager API to search for available Store
app updates.
When no -PackageFamilyName is supplied, returns updates
for all installed Store-packaged apps.

Does not require administrator elevation for user-scope updates.

## EXAMPLES

### Example 1

```powershell
Get-MSStoreUpdate
```

### Example 2

```powershell
Get-MSStoreUpdate -PackageFamilyName '*WindowsTerminal*'
```

## PARAMETERS

### -PackageFamilyName

Specific package family name to check, e.g.
Microsoft.WindowsStore_8wekyb3d8bbwe.
Supports wildcard matching.

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
