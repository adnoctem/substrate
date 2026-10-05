---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Install-MSStoreUpdate
---

# Install-MSStoreUpdate

## SYNOPSIS

Installs available Microsoft Store app updates.

## SYNTAX

### Pipeline (Default)

```
Install-MSStoreUpdate -InputObject <psobject[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Name

```
Install-MSStoreUpdate -PackageFamilyName <string> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### All

```
Install-MSStoreUpdate -All [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses the WinRT AppInstallManager API to trigger Store app updates.
Accepts pipeline input from Get-MSStoreUpdate or a specific
-PackageFamilyName.
Progress is reported via Write-Log.

Does not require administrator elevation for user-scope updates.
Non-installed apps that match -PackageFamilyName are skipped silently.

## EXAMPLES

### Example 1

```powershell
Get-MSStoreUpdate | Install-MSStoreUpdate
```

### Example 2

```powershell
Install-MSStoreUpdate -PackageFamilyName '*WindowsStore*'
```

### Example 3

```powershell
Install-MSStoreUpdate -All
```

## PARAMETERS

### -All

Install all available Store updates.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: All
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
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
HelpMessage: ''
```

### -InputObject

Store update object from Get-MSStoreUpdate (pipeline).

```yaml
Type: System.Management.Automation.PSObject[]
DefaultValue: ''
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
HelpMessage: ''
```

### -PackageFamilyName

Specific package family name to update.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Name
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
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
HelpMessage: ''
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
