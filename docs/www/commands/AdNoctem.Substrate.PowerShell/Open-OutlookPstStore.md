---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Open-OutlookPstStore
---

# Open-OutlookPstStore

## SYNOPSIS

Opens an existing local PST and returns an ownership-aware lifetime context.

## SYNTAX

### __AllParameterSets

```
Open-OutlookPstStore [-Namespace] <Object> [-LiteralPath] <string> [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reuses a uniquely matching profile attachment or adds the existing file with
Namespace.AddStore.
Does not create destinations, rename stores or choose a
format.
ANSI compatibility needs native validation with the installed Outlook.
Requires a writable local PST; UNC, network drives and reparse traversal are
rejected.
Relative paths use PowerShell's current filesystem location and
short names are expanded.
Hard-link aliases are not detected.

The returned PSFoundation.OutlookPstStoreContext owns Root and borrows Namespace.
Path, StoreId and DisplayName describe the observed store; AttachedByCall marks
attachment ownership and Closed tracks cleanup.
Private _State is implementation
state.
Do not edit, serialize or reuse it across processes.
Release child COM
references before Close-OutlookPstStore, and keep the creating context alive
until overlapping consumers finish; contexts are not reference-counted leases.

Standalone WhatIf/declined confirmation returns no context when attachment is
needed.
An explicitly documented inspection preview may override WhatIf for
this call only, then must close in finally.
Outlook may update PST metadata.
Existence is rechecked before AddStore, but Outlook has no atomic existing-only
open.
Do not remove/replace the file or change profile attachments concurrently.

## EXAMPLES

### Example 1

```powershell
$source = $null
try {
  $source = Open-OutlookPstStore -Namespace $context.Namespace -LiteralPath '.\Archive.pst'
  if ($null -ne $source) { $source | Select-Object Path, StoreId, DisplayName, AttachedByCall }
}
finally { if ($null -ne $source) { Close-OutlookPstStore -Context $source } }
```

### Example 2

```powershell
Open-OutlookPstStore -Namespace $context.Namespace -LiteralPath 'D:\Archives\Old mail.pst' -WhatIf
```

Previews a required attachment without adding it. A pre-existing match is returned normally.

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - cf
ParameterSets:
  - Name: (All)
    Position: Named
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -LiteralPath

Existing writable local .pst file.
Wildcards are literal; directories are rejected.

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

### -Namespace

Borrowed MAPI namespace, normally returned by Connect-Outlook.
Never released here.

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

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - wi
ParameterSets:
  - Name: (All)
    Position: Named
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
