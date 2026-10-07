# Existing Outlook PST sources

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
