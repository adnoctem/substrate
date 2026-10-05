---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Convert-RobocopyExitCode
---

# Convert-RobocopyExitCode

## SYNOPSIS

Decodes a Robocopy exit code into its success/failure meaning.

## SYNTAX

### __AllParameterSets

```
Convert-RobocopyExitCode [-ExitCode] <int> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Robocopy uses bitmask exit codes where 0-7 are all success variants and
8+ indicates failure.
Decodes a raw exit code into the corresponding
description.

## EXAMPLES

### Example 1

```powershell
Convert-RobocopyExitCode -ExitCode 3
OKCOPY + XTRA
```

## PARAMETERS

### -ExitCode

The raw exit code from robocopy.exe.

```yaml
Type: System.Int32
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
