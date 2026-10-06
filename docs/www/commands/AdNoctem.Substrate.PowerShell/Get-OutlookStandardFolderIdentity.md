---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OutlookStandardFolderIdentity
---

# Get-OutlookStandardFolderIdentity

## SYNOPSIS

Reads locale-independent standard folder identities for one Outlook store.

## SYNTAX

### __AllParameterSets

```
Get-OutlookStandardFolderIdentity [-Namespace] <Object> [-StoreRoot] <Object> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns plain records keyed by StoreID and EntryID.
Outlook 2010 and later
use Store.GetDefaultFolder.
Outlook 2007 uses read-only MAPI properties,
with the default Inbox as an additional property source.
No localized
folder names are used.
Missing optional properties are reported separately
from unexpected provider errors.
Does not create optional default folders.

## EXAMPLES

### Example 1

```powershell
Get-OutlookStandardFolderIdentity -Namespace $context.Namespace -StoreRoot $root
```

## PARAMETERS

### -Namespace

Connected Outlook MAPI namespace.

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

### -StoreRoot

Root folder of the selected store, owned by the caller.

```yaml
Type: System.Object
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
