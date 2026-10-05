---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-HostApplicability
---

# Test-HostApplicability

## SYNOPSIS

Returns $true if the current host meets all supplied applicability constraints.

## SYNTAX

### __AllParameterSets

```
Test-HostApplicability [[-MinBuild] <int>] [[-MaxBuild] <int>] [[-Edition] <string[]>]
 [[-Bitness] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Gates a setting or operation by build number, OS edition, and processor
bitness.
Any constraint that is not supplied is treated as "no
restriction." All supplied constraints must pass — the check is a
logical AND across them.

Builds on the existing Get-OSBuildNumber and Get-OSEdition helpers in
this file so it shares the same cheap registry-read path.

## EXAMPLES

### Example 1

```powershell
if (-not (Test-HostApplicability -MinBuild 22000)) { Write-Log -Message 'Requires Windows 11+'; exit 0 }
```

### Example 2

```powershell
Test-HostApplicability -Edition @('ServerStandard', 'ServerDatacenter') -Bitness x64
```

## PARAMETERS

### -Bitness

Required processor architecture: x64 or x86.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 3
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Edition

OS edition(s) that qualify.
Accepts an array of strings such as
@('Enterprise', 'Professional').

```yaml
Type: System.String[]
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 2
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -MaxBuild

Maximum Windows build number allowed (inclusive).
Example: 22621 for
Windows 11 22H2 and below.

```yaml
Type: System.Int32
DefaultValue: ""
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
HelpMessage: ""
```

### -MinBuild

Minimum Windows build number required (inclusive).
Example: 22000 for
Windows 11+.

```yaml
Type: System.Int32
DefaultValue: ""
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
HelpMessage: ""
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
