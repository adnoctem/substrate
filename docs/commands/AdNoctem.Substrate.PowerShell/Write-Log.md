---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Write-Log
---

# Write-Log

## SYNOPSIS

Write a message to standard output with color and optional timestamp.

## SYNTAX

### __AllParameterSets

```
Write-Log [-Message] <string> [[-Color] <ConsoleColor>] [-Timestamps] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Emits the supplied message to the console using the chosen foreground
color.
When -Timestamps is set, the output is prefixed with the current
date and time before being written.

## EXAMPLES

### Example 1

```powershell
Write-Log -Message 'Setup completed.' -Color Green -Timestamps
```

## PARAMETERS

### -Color

Console foreground color used for the host message.

```yaml
Type: System.ConsoleColor
DefaultValue: "'White'"
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

### -Message

Text to write to the PowerShell host.

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

### -Timestamps

Prefixes the message with the current local timestamp.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: $false
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
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
