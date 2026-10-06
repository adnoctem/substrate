---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: ConvertTo-RegistryPolicy
---

# ConvertTo-RegistryPolicy

## SYNOPSIS

Writes ordered records to a registry.pol file without applying policy.

## SYNTAX

### __AllParameterSets

```
ConvertTo-RegistryPolicy [[-InputObject] <Object[]>] [-Path] <string> [-Force] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Accepts records with Key, ValueName, numeric Type, and Data.
Supports
decoded records and RawEntry records from ConvertFrom-RegistryPolicy.
Preserves order, duplicates, and directive names.
Raw entries retain
payload bytes; other records are encoded by registry type.
Null data
encodes a zero-byte payload.
Unknown types require byte arrays.
Validates and serializes the entire input before creating a temporary
sibling file and atomically replacing the destination.
Empty input writes
a header-only file.
Parent directories must already exist.
Limits match
the reader: 64 MiB per file, 65535 bytes per payload.

## EXAMPLES

### Example 1

```powershell
$entries | ConvertTo-RegistryPolicy -Path '.\registry.pol' -Force -WhatIf
```

### Example 2

```powershell
ConvertTo-RegistryPolicy -InputObject @() -Path '.\empty.pol'
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

### -Force

Allow replacement of an existing destination file.

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

### -InputObject

One record or an array of records, also accepted from the pipeline.

```yaml
Type: System.Object[]
DefaultValue: "@()"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: false
    ValueFromPipeline: true
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Path

Literal filesystem destination.
Machine/User scope is chosen by the caller.

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

### System.Object

### System.Object

### System.Object

### System.Object

### System.Object

### System.Object

### System.Object

## OUTPUTS

### System.Void

## NOTES

## RELATED LINKS
