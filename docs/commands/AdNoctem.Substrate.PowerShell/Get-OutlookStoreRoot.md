---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OutlookStoreRoot
---

# Get-OutlookStoreRoot

## SYNOPSIS

Gets the root folder for an Outlook store.

## SYNTAX

### __AllParameterSets

```
Get-OutlookStoreRoot [-Namespace] <Object> [[-Name] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Resolves a named Outlook store by DisplayName, or the default delivery
store when no name is supplied.
Store COM objects are released as they are
inspected; the returned root folder is owned by the caller.

## EXAMPLES

### Example 1

```powershell
Get-OutlookStoreRoot -Namespace $context.Namespace -Name 'user@example.com'
```

## PARAMETERS

### -Name

Optional store display name.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 1
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Namespace

Outlook MAPI namespace returned by Connect-Outlook.

```yaml
Type: System.Object
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: true
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

### System.Object

## NOTES

## RELATED LINKS
