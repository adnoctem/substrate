---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Close-OutlookPstStore
---

# Close-OutlookPstStore

## SYNOPSIS

Releases an existing-PST context and detaches only its owned attachment.

## SYNTAX

### __AllParameterSets

```
Close-OutlookPstStore [-Context] <Object> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Checks the recorded StoreID and path before detaching an attachment created by
Open-OutlookPstStore.
Pre-existing stores and replacement StoreIDs are left alone.
Always releases the owned root, clears Root and sets Closed, even on failure.
Subsequent calls do nothing.
The namespace/application remain borrowed and live.
Never deletes, renames, replaces, repairs or compacts files, or quits Outlook.

This is mandatory finally cleanup, not a second optional profile operation:
it does not prompt or honor inherited WhatIf suppression.
It can only undo the
attachment identified by its context.
Detach/inspection failure is terminating
and identifies the PST path; the context is closed but the attachment may remain.
Callers must release all child COM references first and report cleanup failure
without hiding any original processing error.
Concurrent profile changes and
unobservable reuse of the same store identity cannot be made transactional.

## EXAMPLES

### Example 1

```powershell
if ($null -ne $source) { Close-OutlookPstStore -Context $source }
```

Releases a context in finally without releasing its borrowed namespace.

## PARAMETERS

### -Context

Live context returned by Open-OutlookPstStore.
Guard null in the caller.

```yaml
Type: System.Object
DefaultValue: ''
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
HelpMessage: ''
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
