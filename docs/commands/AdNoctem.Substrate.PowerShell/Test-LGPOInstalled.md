---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-LGPOInstalled
---

# Test-LGPOInstalled

## SYNOPSIS

Returns whether the LGPO executable exists at the specified path.

## SYNTAX

### __AllParameterSets

```
Test-LGPOInstalled [[-Path] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses C# for filesystem checks and preserves PowerShell provider and empty-path handling.

## EXAMPLES

### EXAMPLE 1

```powershell
Test-LGPOInstalled
```

## PARAMETERS

### -Path

Literal executable path.
Defaults to the module's ProgramData tools directory.

```yaml
Type: System.String
DefaultValue: (Join-Path -Path $env:ProgramData -ChildPath 'AdNoctem.Substrate.PowerShell\tools\LGPO.exe')
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
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Boolean

## NOTES

## RELATED LINKS
