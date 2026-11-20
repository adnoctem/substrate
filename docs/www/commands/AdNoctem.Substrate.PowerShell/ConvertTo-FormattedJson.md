---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: ConvertTo-FormattedJson
---

# ConvertTo-FormattedJson

## SYNOPSIS

Converts an object to JSON with a fixed indentation on every PowerShell edition.

## SYNTAX

### __AllParameterSets

```
ConvertTo-FormattedJson [-InputObject] <Object> [-Depth <int>] [-Indent <int>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Serializes with ConvertTo-Json -Compress and indents the result by a fixed number of spaces.
Windows PowerShell 5.1 aligns nested values under their keys, which pushes deep reports far to the right.
The apostrophe, angle brackets and ampersand that Windows PowerShell 5.1 escapes as \u00XX are written as plain characters; escaped literal text stays escaped.
Several pipeline values are serialized as one array.

## EXAMPLES

### EXAMPLE 1

```powershell
$report | ConvertTo-FormattedJson -Depth 12 | Set-Content -LiteralPath report.json -Encoding UTF8
```

## PARAMETERS

### -Depth

Maximum nesting depth passed to ConvertTo-Json.

```yaml
Type: System.Int32
DefaultValue: 20
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

### -Indent

Spaces per nesting level. Zero writes one value per line without indentation.

```yaml
Type: System.Int32
DefaultValue: 2
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

Object to serialize.

```yaml
Type: System.Object
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

### System.Object

## OUTPUTS

### System.String

## NOTES

## RELATED LINKS
