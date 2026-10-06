---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-DefenderThreatDescriptionURL
---

# Get-DefenderThreatDescriptionURL

## SYNOPSIS

Builds the public Microsoft Defender threat description URL for a given
threat family name.

## SYNTAX

### __AllParameterSets

```
Get-DefenderThreatDescriptionURL [-ThreatName] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Takes a threat name (e.g.
'Trojan:Win32/Emotet'), URL-encodes it, and
returns the full HTTP/S link to the official WDSI (Windows Defender
Security Intelligence) threat encyclopedia entry.

## EXAMPLES

### Example 1

```powershell
Get-DefenderThreatDescriptionURL -ThreatName 'Trojan:Win32/Emotet'
https://www.microsoft.com/en-us/wdsi/threats/threat/Trojan%3AWin32%2FEmotet
```

## PARAMETERS

### -ThreatName

The human-readable threat family name, exactly as reported by
Get-MpThreat (Name property) or Get-MpThreatDetection (ThreatName).

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: true
    ValueFromPipeline: true
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

### System.String

## OUTPUTS

### System.String

## NOTES

Descriptions are resolved by joining ThreatID to threat inventory. An unresolved name yields a null URL rather than an invented description.

## RELATED LINKS
