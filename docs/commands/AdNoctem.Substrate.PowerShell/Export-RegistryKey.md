---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Export-RegistryKey
---

# Export-RegistryKey

## SYNOPSIS

Exports a registry key to a .reg text file via reg.exe.

## SYNTAX

### __AllParameterSets

```
Export-RegistryKey [-Key] <string> [-OutputPath] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Calls reg.exe export /y against the supplied key.
Uses Invoke-SafeProcess
internally and checks the native result.
Publishes a nonempty export only
after successful completion, replacing an existing destination atomically.
A failed command leaves the previous destination intact.
Returns a Boolean;
failures also write an error (terminating with ErrorAction Stop).

## EXAMPLES

### Example 1

```powershell
Export-RegistryKey -Key 'HKLM\Software\Microsoft\Windows\CurrentVersion\Run' -OutputPath '.\HKLM-Run.reg'
```

## PARAMETERS

### -Key

Registry key path, e.g.
'HKLM\Software\Microsoft\Windows\CurrentVersion\Run'.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -OutputPath

Path for the exported .reg file.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 1
    IsRequired: true
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

### System.Object

## NOTES

## RELATED LINKS
