---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-OfficeActivationStatus
---

# Get-OfficeActivationStatus

## SYNOPSIS

Assesses target activation separately from installation compliance.

## SYNTAX

### __AllParameterSets

```
Get-OfficeActivationStatus [-TargetProductId] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Subscription activation requires the licensed user's session.
Volume license
status is matched to the selected SKU without returning partial product keys.

## EXAMPLES

### Example 1

```powershell
Get-OfficeActivationStatus -TargetProductId Standard2024Volume
```

## PARAMETERS

### -TargetProductId

Exact Office suite product ID.

```yaml
Type: System.String
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
