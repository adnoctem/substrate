---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Install-LGPO
---

# Install-LGPO

## SYNOPSIS

Downloads, verifies, and installs LGPO.exe to a controllable path.

## SYNTAX

### __AllParameterSets

```
Install-LGPO [[-Destination] <string>] [[-Source] <string>] [-Force] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Hash-verified install.
Refuses to proceed if the SHA-256 of the
downloaded archive does not match Resolve-LGPOSource's recorded hash.

Idempotent: re-running when LGPO.exe is already present at the
destination returns the existing path unless -Force is specified.

Writing to the default destination (%ProgramData%) requires
administrator elevation.
A non-elevated session can specify an
alternate -Destination within the user's writable scope.

## EXAMPLES

### Example 1

```powershell
Install-LGPO
```

### Example 2

```powershell
Install-LGPO -Force -Verbose
```

### Example 3

```powershell
Install-LGPO -Destination "$env:LOCALAPPDATA\PSFoundation\tools"
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

### -Destination

Directory where LGPO.exe ends up.
Defaults to %ProgramData%\PSFoundation\tools.
Non-elevated callers should supply a user-writable path.

```yaml
Type: System.String
DefaultValue: (Join-Path -Path $env:ProgramData -ChildPath 'PSFoundation\tools')
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

### -Force

Re-download and re-install even if LGPO.exe is already present.

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

### -Source

Source identifier forwarded to Resolve-LGPOSource.

```yaml
Type: System.String
DefaultValue: "'SCT-LGPO-Standalone'"
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

### System.String

## NOTES

## RELATED LINKS
