---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OutlookAccount
---

# Get-OutlookAccount

## SYNOPSIS

Reads the accounts of the connected Outlook profile and the store each one delivers new mail into.

## SYNTAX

### __AllParameterSets

```
Get-OutlookAccount [-Namespace] <Object> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns one plain record per account: display name, SMTP address, user name, raw OlAccountType with its label,
and the StoreID, display name, and file path of its delivery store.

Delivery store identity is the fact that decides whether removing an account destroys another account's mail.
A POP or IMAP account can deliver into an Exchange mailbox, whose OST cannot be opened without that account.

DeliveryStoreSource states how that identity was established, because the two routes do not carry the same weight:

- Account — read from the account itself. Authoritative, and the only source on Outlook 2010 and later.
- ProfileDefault — the account could not be read, so the store is the profile's default delivery store, resolved
  through the Inbox it owns. Account.DeliveryStore was added in Outlook 2010; below it no account reports delivery,
  and the default is where new mail lands unless that account carries an override. A client that cannot report
  delivery cannot report the override either, so IsDeliveryStoreInferred is true and the value needs confirming in
  Account Settings before anything is removed.
- Unresolved — neither route answered. The delivery fields stay empty.

IsServerSynchronized marks providers whose folders mirror server state, so a local removal can reach the server
and other clients. MayDeferItemBodies marks providers that can leave an item as a header-only stub: Outlook 2007
backs an IMAP account with a PST, so a file extension cannot establish this and the owning account must.

No item is touched, no folder is enumerated, and no COM reference is retained. This stays readable on Outlook 2007
Exchange and OST profiles, where standard folder identity resolution is not.

Only one folder is read, and only to identify the default delivery store: GetDefaultFolder returns the Inbox and
its StoreID is taken. MAPIFolder.StoreID has been readable since Outlook 2000, so this answers where
Account.DeliveryStore does not exist. The folder is not enumerated and no item is reached through it.

Unreadable optional properties are reported in Evidence and as verbose records rather than failing the read.
An account that reports no delivery store leaves the delivery fields empty, and so does a profile whose default
store cannot be read. Nothing is reported without a source that names where it came from.

## EXAMPLES

### Example 1

```powershell
Get-OutlookAccount -Namespace $context.Namespace
```

### Example 2

```powershell
Get-OutlookAccount -Namespace $context.Namespace | Where-Object { $_.AccountTypeName -eq 'IMAP' }
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
