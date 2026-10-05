---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Search-RegistryKey
---

# Search-RegistryKey

## SYNOPSIS

Searches a registry hive for a pattern via reg.exe query.

## SYNTAX

### __AllParameterSets

```
Search-RegistryKey [-Root] <string> [-Pattern] <string> [-OutputPath] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Calls reg.exe query <Root> /f <Pattern> /s to recursively search for a
string or pattern across a registry hive.
Output is written to -OutputPath.
Uses Invoke-SafeProcess internally and checks the native result.
Publishes
captured output only after success, preserving an existing destination on
failure.
Returns a Boolean and writes an error on failure.
No matches is a
native failure, not a successful empty report.

## EXAMPLES

### Example 1

```powershell
Search-RegistryKey -Root 'HKLM' -Pattern 'InstallUtil' -OutputPath '.\Reg-HKLM-InstallUtil.txt'
```

## PARAMETERS

### -OutputPath

File to write the search results to.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 2
    IsRequired: true
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Pattern

Search pattern forwarded to /f.

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

### -Root

Registry hive root, e.g.
'HKLM', 'HKCU'.

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
