---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OutlookStore
---

# Get-OutlookStore

## SYNOPSIS

Reads the data files of the connected Outlook profile with the accounts that depend on each one.

## SYNTAX

### __AllParameterSets

```
Get-OutlookStore [-Namespace] <Object> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns one plain record per store: display name, StoreID, file path, backing file format, raw
OlExchangeStoreType with its label, file size, and the display names of the accounts that deliver new mail there.
Delivery is correlated by StoreID, because two data files can share a display name.

IsSynchronized states that changes here can reach a server and other clients. MayContainHeaderOnlyItems states
that an item can still be a stub, so a transfer must verify each body first. Both are decided from the owning
accounts as well as the file, because an Outlook 2007 IMAP store is a PST that reports olNotExchange: neither the
extension nor the Exchange store type distinguishes it from an ordinary local data file.

olNotExchange (3) is the only value that proves a store is not served by Exchange. A store whose type the provider
does not report counts as Exchange-backed, and Evidence says so.

Only store and account properties are read. No folder is opened, no item is touched, and no COM reference is
retained. Size is a convenience: an absent or unreadable file reports none rather than failing the read, and
Outlook holding the file open does not prevent the length read.

## EXAMPLES

### Example 1

```powershell
Get-OutlookStore -Namespace $context.Namespace
```

### Example 2

```powershell
Get-OutlookStore -Namespace $context.Namespace | Where-Object { $_.MayContainHeaderOnlyItems }
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
