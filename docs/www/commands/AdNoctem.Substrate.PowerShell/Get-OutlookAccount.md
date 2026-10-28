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

IsServerSynchronized marks providers whose folders mirror server state, so a local removal can reach the server
and other clients. MayDeferItemBodies marks providers that can leave an item as a header-only stub: Outlook 2007
backs an IMAP account with a PST, so a file extension cannot establish this and the owning account must.

Only account and store properties are read. No folder is opened, no item is touched, and no COM reference is
retained. This stays readable on Outlook 2007 Exchange and OST profiles, where standard folder discovery is not.

Unreadable optional properties are reported in Evidence and as verbose records rather than failing the read.
A missing delivery store leaves the delivery fields empty; it is never guessed at.

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
