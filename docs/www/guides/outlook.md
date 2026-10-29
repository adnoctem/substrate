# Existing Outlook PST sources

## Automation objects and synthetic tests

Namespace, store and folder inputs represent live Classic Outlook automation objects. The PowerShell adapter unwraps PSObject wrappers
around COM or CLR objects; the shared C# planner reads public CLR/COM members directly. PSCustomObject note properties and ScriptMethod
members are PowerShell extensions and do not provide that automation contract.

For tests that exercise the real planner, load C# doubles with `Add-Type` and supply public properties, fields and methods matching the
Outlook members used by the operation. The repository's
[OutlookFixture.cs](https://github.com/adnoctem/substrate/blob/main/tests/fixtures/v2/OutlookFixture.cs) and
[Outlook adapter probes](https://github.com/adnoctem/substrate/blob/main/tests/fixtures/v2/Outlook-Probe.ps1) show this approach, including
one-based collections and configurable standard-folder identities and failures. These are test fixtures, not a shipped testing API.

For script-only unit tests, mock the exported cmdlet that the consuming script calls in that script's scope. Mocking
`Get-OutlookStandardFolderIdentity` does not intercept discovery inside the compiled `Get-OutlookFolderPlan` command. Keep real-planner
fixture tests alongside script-level mocks so folder exclusions, incomplete identities and preview behavior remain covered.

## Profile inventory and store classification

`Get-OutlookAccount` and `Get-OutlookStore` read the accounts and data files of a connected profile without enumerating a folder or touching
an item. Both come from one correlated read, so a store can state which accounts deliver into it. Correlation uses StoreID: display names
repeat across data files, and two profiles routinely hold several "Personal Folders".

Use them where folder discovery is unavailable. Standard-folder identities are refused for Exchange and OST stores below Outlook 2010, so an
inventory is the only thing that answers "what does this account deliver into" on an Outlook 2007 Exchange profile — which is also where the
answer matters most, because a POP account delivering into an Exchange mailbox loses its mail when that mailbox's OST is removed.

### Delivery on clients below Outlook 2010

`Account.DeliveryStore` was added in Outlook 2010. On Outlook 2007 the property does not exist, the read fails with
`DISP_E_UNKNOWNNAME`, and no account in the profile can state where its mail lands — on exactly the clients where a POP account sharing a
profile with an Exchange mailbox is most likely.

There the inventory resolves the **profile default delivery store** instead, through the StoreID of the Inbox that `GetDefaultFolder`
returns. That property has been readable since Outlook 2000. New mail lands in the default store unless an account carries an override, and a
client that cannot report delivery cannot report an override either, so:

- `DeliveryStoreSource` is `ProfileDefault` and `IsDeliveryStoreInferred` is `$true`.
- The store's `DeliveredFrom` lists the account, and the store's `Evidence` says the delivery was taken from the profile default.
- An account re-pointed somewhere else would still be listed against the default store, so confirm the value in Account Settings before
  removing an account or a data file.

Where the default store cannot be read either, nothing is substituted: the delivery fields stay empty and `DeliveryStoreSource` is
`Unresolved`.

Two classifications carry the operational meaning:

- `IsSynchronized` — changes here can reach a server and other clients. True for an OST, for any Exchange-backed store, and for a store that
  an Exchange, IMAP, HTTP or EAS account delivers into.
- `MayContainHeaderOnlyItems` — an item can still be a stub whose body is on the server, so a transfer must verify `DownloadState` first.
  True for an OST, and for a store that an IMAP, HTTP or EAS account delivers into.

Neither is decided from the file alone, because the file does not carry the answer. **Outlook 2007 backs an IMAP account with a PST**, and
that store reports `olNotExchange`. Classifying by extension or by `ExchangeStoreType` marks it a plain local data file, skips the download
check, and lets a Move-mode transfer delete a server copy whose body was never downloaded. The owning account's `AccountType` is the only
reliable signal, which is why store classification is computed from the accounts rather than published as a property of the store.

`ExchangeStoreType` is reported raw alongside its label, and `olNotExchange` (3) is the only value that proves a store is not served by
Exchange. A store whose type the provider does not report counts as Exchange-backed, and `Evidence` records that it was unreadable rather
than letting a missing value read as local.

## Folder discovery and incomplete plans

`Get-OutlookFolderPlan` tolerates individual standard-folder lookup failures. For example, a provider error for
SuggestedContacts leaves the resolved Inbox and ordinary mail folders usable. The error remains an `Unresolved` identity with its exact
HRESULT; unsupported lookups are `Unavailable`, and a provider-reported missing folder is `Absent`. Discovery never creates missing folders.

Warnings identify the folder kind, StoreID and provider error. Unknown excluded identities are handled conservatively: branches that could
be the protected folder receive `IncompleteIdentity:<kinds>` with `Process` and `Traverse` false. Known identities and default item types
constrain these matches, without guessing from localized names. An unknown mail-folder identity can therefore prevent processing ordinary
branches too. The root remains traversable, but its own items are skipped when classification is incomplete. Review both warnings and plan
rows; successful planning does not imply every branch was included. Inclusion flags, custom exclusions and search-folder exclusions still apply.

The default selection requires Inbox by identity and fails with store/error details if it cannot resolve it. For an archive without Inbox,
use an explicit existing `-FolderName` or `-FolderName ''` for the root. Source access and identity-integrity failures still fail planning;
none of this relaxes destination or transfer error handling. The plan contains scalar metadata only and performs no item transfers.

## PST attachment lifetime

`Open-OutlookPstStore -Namespace $context.Namespace -LiteralPath <existing.pst>` reuses a matching attachment or temporarily attaches the
existing file. `Close-OutlookPstStore -Context $source` releases its root and removes only the attachment that Open created, identified by
StoreID and normalized file path. Existing attachments keep their names and remain attached. Neither helper releases the borrowed MAPI
namespace/application, changes the default store, or deletes, renames, repairs or compacts the file. Destination creation through
`Add-OutlookStoreRoot` remains unchanged and uses Unicode.

The live context has `Path`, `StoreId`, `DisplayName`, `Root`, `Namespace`, `AttachedByCall` and `Closed`, with type name
`PSFoundation.OutlookPstStoreContext`. Report only the scalar fields; do not serialize the context or modify its private `_State`. Release
all child folder/item references before closing. The creating caller must keep its context alive until overlapping users finish; these
contexts are not reference-counted attachment leases.

```powershell
$source = $null
try {
  $source = Open-OutlookPstStore -Namespace $context.Namespace -LiteralPath '.\Combined archive.pst'
  if ($null -ne $source) {
    # Use $source.Root with the existing folder-plan/transfer APIs.
    # Compare normalized source/destination paths and StoreIDs before transferring.
    $source | Select-Object Path, StoreId, DisplayName, AttachedByCall
  }
}
finally {
  if ($null -ne $source) { Close-OutlookPstStore -Context $source }
}
```

Open honors ShouldProcess: a suppressed/declined attachment produces no context. A pre-existing match still returns a context. Close is
required lifetime cleanup, so it performs no second confirmation and inherited WhatIf cannot suppress detachment/release. Close marks the
context closed and releases its root even when detachment fails; it throws an error naming the path, and the attachment may remain. A second
Close does nothing. Consumers should include cleanup failure in their reports without hiding the original processing error.

An explicitly documented wrapper preview may need a temporary attachment to inspect messages. Only that source-opening call may use
`-WhatIf:$false -Confirm:$false`, followed by Close in finally. Destination creation and message transfer retain preview suppression.
Outlook can update PST metadata during inspection, so this is not byte-preserving or offline parsing. User/profile/elevation checks and
source/destination separation remain consumer responsibilities.

Sources must be existing writable local PST files. Relative paths resolve against PowerShell's current location; matching ignores Windows
path case and expands short names. UNC/mapped network sources and reparse-point traversal are rejected; unreadable nonempty store paths
block inspection rather than imply absence. Hard-link aliases are not detected. Do not replace/delete sources or concurrently alter profile
attachments while a context is live: existence rechecks and observed StoreIDs cannot provide atomic file/profile guarantees.

The helpers use [AddStore](https://learn.microsoft.com/en-us/office/vba/api/outlook.namespace.addstore) for existing files without selecting
a creation format, and [RemoveStore](https://learn.microsoft.com/en-us/office/vba/api/outlook.namespace.removestore) only for profile
detachment. No format conversion is requested. Automated tests use synthetic files and mocked COM; native attach/reuse/detach, preview,
duplicate display names and existing ANSI/Unicode PSTs still need disposable-file validation. No splitting or transfer engine is included.
