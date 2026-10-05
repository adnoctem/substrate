---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Convert-Quote
---

# Convert-Quote

## SYNOPSIS

Converts single quotes to double quotes or vice versa in a specified file.

## SYNTAX

### __AllParameterSets

```
Convert-Quote [-Path] <string> [[-To] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

This function reads the content of a file and replaces all single quotes with double quotes or all double quotes with single quotes, based on the specified parameter.
It is useful for standardizing quote usage in configuration files, scripts, or any text files.

## EXAMPLES

### EXAMPLE 1

```powershell
Convert-Quote -Path 'C:\config.txt' -To 'Single'
```

This command converts all double quotes in the file 'C:\config.txt' to single quotes.

### EXAMPLE 2

```powershell
Convert-Quote -Path 'C:\config.txt' -To 'Double'
```

This command converts all single quotes in the file 'C:\config.txt' to double quotes.

## PARAMETERS

### -Path

The full path to the file that needs to be processed.
The file must exist and be accessible for reading and writing.

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

### -To

Specifies the type of quote conversion to perform.
Acceptable values are "Single" for converting double quotes to single quotes and "Double" for converting single quotes to double quotes.
The default value is "Double".

```yaml
Type: System.String
DefaultValue: Double
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
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

## OUTPUTS

### System.Void

## NOTES

## RELATED LINKS
