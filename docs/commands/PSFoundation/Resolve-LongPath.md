---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Resolve-LongPath
---

# Resolve-LongPath

## SYNOPSIS

Resolves an existing filesystem path and expands Windows short-name aliases.

## SYNTAX

### __AllParameterSets

```
Resolve-LongPath [-LiteralPath] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns an absolute path with its existing long directory and file names.
Uses GetLongPathNameW on Windows and normal filesystem resolution elsewhere.
Does not create files or resolve symlinks to their final targets.
Failures
are terminating rather than returning a mixture of short and long names.

## EXAMPLES

### Example 1

```powershell
Resolve-LongPath -LiteralPath $env:TEMP
```

## PARAMETERS

### -LiteralPath

Existing file or directory path.
Wildcards are treated literally.

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
