---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-LGPOSourceAvailability
---

# Test-LGPOSourceAvailability

## SYNOPSIS

Verifies the LGPO download URL is still reachable.

## SYNTAX

### __AllParameterSets

```
Test-LGPOSourceAvailability [[-Source] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Issues a HEAD request to the URL returned by Resolve-LGPOSource.
Does NOT download or verify the content - that's Install-LGPO's job.

Intended for weekly CI runs.
The output object is suitable for
serialising to JSON and archiving as ISO supply-chain evidence
("we monitor external dependencies weekly").

## EXAMPLES

### Example 1

```powershell
Test-LGPOSourceAvailability
```

## PARAMETERS

### -Source

Source identifier forwarded to Resolve-LGPOSource.

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

## RELATED LINKS
