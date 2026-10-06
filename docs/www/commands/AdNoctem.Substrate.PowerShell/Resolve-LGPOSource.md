---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Resolve-LGPOSource
---

# Resolve-LGPOSource

## SYNOPSIS

Returns metadata for a known LGPO source location.

## SYNTAX

### __AllParameterSets

```
Resolve-LGPOSource [[-Source] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Pure data lookup.
No I/O.
Centralises the "where do we get LGPO from"
decision so that every other function in this module can reference it
without duplicating URLs or hashes.

When Microsoft moves the file or publishes a new SCT release, update
the table below and have a human review the diff.
This function is the
single point of change for supply-chain trust.

## EXAMPLES

### Example 1

```powershell
Resolve-LGPOSource
```

## PARAMETERS

### -Source

Source identifier.
Currently only 'SCT-LGPO-Standalone' is recognised.

```yaml
Type: System.String
DefaultValue: "'SCT-LGPO-Standalone'"
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
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

Sha256 identifies the reviewed ZIP, not only its contained executable. The catalog also pins the executable separately. Reviewed hashes and verification dates intentionally supersede the historical PSFoundation placeholder; source resolution performs no download.

## RELATED LINKS
