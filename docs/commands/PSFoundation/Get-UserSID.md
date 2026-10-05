---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-UserSID
---

# Get-UserSID

## SYNOPSIS

Resolves a Windows account name to its security identifier.

## SYNTAX

### __AllParameterSets

```
Get-UserSID [-UserName] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses native account translation through the C# identity manager.

## EXAMPLES

### EXAMPLE 1

```powershell
Get-UserSID -UserName 'DOMAIN\user'
```

## PARAMETERS

### -UserName

Local or domain account name to resolve.

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

### System.String

## NOTES

## RELATED LINKS
