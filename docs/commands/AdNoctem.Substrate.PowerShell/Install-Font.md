---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Install-Font
---

# Install-Font

## SYNOPSIS

Installs font files into the system font store.

## SYNTAX

### __AllParameterSets

```
Install-Font [-FontSourceFolder] <string> [-RemoveSource] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Copies .ttf/.ttc/.otf font files from a source folder into
%SystemRoot%\Fonts and registers each one under
HKLM:\Software\Microsoft\Windows NT\CurrentVersion\Fonts with a
correctly-derived display name - Windows does not recognize a font as
installed without the matching registry entry.
The display name comes
from COM Shell.Application metadata extraction with a filename-based
fallback for fonts where the metadata read fails.

Requires administrator elevation for the system font store.
The source
files are left in place unless -RemoveSource is supplied.

Adapted from Install-Font.psm1 in LeDragoX/Win-Debloat-Tools (itself
adapted from a gist by anthonyeden).

## EXAMPLES

### Example 1

```powershell
Install-Font -FontSourceFolder 'C:\downloads\fonts'
Install-Font -FontSourceFolder 'C:\downloads\fonts' -RemoveSource
```

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- cf
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

### -FontSourceFolder

Folder containing the font files (.ttf/.ttc/.otf) to install.

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

### -RemoveSource

Delete each font file from the source folder after a successful install
(matching the reference behavior; off by default - the module never
deletes user files unless asked).

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
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

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- wi
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
