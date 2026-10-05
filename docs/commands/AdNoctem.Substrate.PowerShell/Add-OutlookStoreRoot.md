---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Add-OutlookStoreRoot
---

# Add-OutlookStoreRoot

## SYNOPSIS

Adds a Unicode PST store and returns its root folder.

## SYNTAX

### __AllParameterSets

```
Add-OutlookStoreRoot [-Namespace] <Object> [-Path] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Calls Outlook Namespace.AddStoreEx with OlStoreType.olStoreUnicode and
locates the newly attached store by FilePath.
The returned root folder is
owned by the caller.

## EXAMPLES

### Example 1

```powershell
Add-OutlookStoreRoot -Namespace $context.Namespace -Path 'D:\Archive\mail.pst'
```

## PARAMETERS

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

### -Path

Full path to the PST file to attach or create.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 1
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
