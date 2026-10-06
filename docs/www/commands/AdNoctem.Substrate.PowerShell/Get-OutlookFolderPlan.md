---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OutlookFolderPlan
---

# Get-OutlookFolderPlan

## SYNOPSIS

Builds a read-only, locale-independent Outlook folder processing plan.

## SYNTAX

### __AllParameterSets

```
Get-OutlookFolderPlan [-Namespace] <Object> [-StoreRoot] <Object> [[-FolderName] <string>]
 [[-Include] <string[]>] [[-Exclusions] <string[]>] [[-ProgressId] <int>] [-Recurse]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Resolves one exact store-relative path and optionally its descendants.
Standard folders require inclusion by identity, custom exclusions win,
and search folders are always excluded.
Only mail items are eligible;
included non-mail containers permit traversal to mail subfolders.
Returns plain metadata; callers reopen selected folders by EntryID and
StoreID.
The entire plan must be collected successfully before mutation.

## EXAMPLES

### Example 1

```powershell
Get-OutlookFolderPlan -Namespace $context.Namespace -StoreRoot $root -FolderName Posteingang -Recurse
```

### Example 2

```powershell
Get-OutlookFolderPlan -Namespace $context.Namespace -StoreRoot $root
```

Selects the Inbox by identity, including when localized or renamed.

## PARAMETERS

### -Exclusions

Exact store-relative folder paths to exclude with their descendants.

```yaml
Type: System.String[]
DefaultValue: "@()"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 4
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -FolderName

Exact store-relative path when explicitly supplied.
Empty selects the root.
When omitted, selects the store's Inbox by identity regardless of its name.
If that identity cannot be resolved, supply an explicit path; no fallback
to a name or the store root is attempted.

```yaml
Type: System.String
DefaultValue: "'Inbox'"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 2
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Include

Standard folder kinds permitted within the selected scope.
Default Inbox.

```yaml
Type: System.String[]
DefaultValue: "@('Inbox')"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 3
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

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

### -ProgressId

Progress record identifier used during enumeration.

```yaml
Type: System.Int32
DefaultValue: 0
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 5
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Recurse

Visit descendants of the selected folder.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
SupportsWildcards: false
Aliases: []
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
