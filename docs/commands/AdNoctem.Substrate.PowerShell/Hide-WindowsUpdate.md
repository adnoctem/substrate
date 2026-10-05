---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Hide-WindowsUpdate
---

# Hide-WindowsUpdate

## SYNOPSIS

Hides an available update so it does not appear in future scans.

## SYNTAX

### Pipeline

```
Hide-WindowsUpdate -InputObject <psobject[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### KB

```
Hide-WindowsUpdate -KBArticleID <string> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Sets the selected update's hidden state through Windows Update Agent.
Useful for suppressing known-problematic updates
or driver packages.
Accepts pipeline input from Get-WindowsUpdate.

## EXAMPLES

### Example 1

```powershell
Get-WindowsUpdate -Title '*Driver*' | Hide-WindowsUpdate
```

### Example 2

```powershell
Hide-WindowsUpdate -KBArticleID 'KB5021233'
```

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

### -InputObject

Update object from Get-WindowsUpdate (pipeline).

```yaml
Type: System.Management.Automation.PSObject[]
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: Pipeline
    Position: Named
    IsRequired: true
    ValueFromPipeline: true
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -KBArticleID

Hide a specific KB.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: KB
    Position: Named
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

### System.Management.Automation.PSObject

### System.Management.Automation.PSObject

### System.Management.Automation.PSObject

### System.Management.Automation.PSObject

### System.Management.Automation.PSObject

### System.Management.Automation.PSObject

### System.Management.Automation.PSObject

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
