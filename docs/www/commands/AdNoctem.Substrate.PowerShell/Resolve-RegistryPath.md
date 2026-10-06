---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Resolve-RegistryPath
---

# Resolve-RegistryPath

## SYNOPSIS

Resolves a registry path string to a Microsoft.Win32.RegistryKey object.

## SYNTAX

### __AllParameterSets

```
Resolve-RegistryPath [-Path] <string> [[-View] <RegistryView>] [-Writable] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Parses a registry path supplied in PS drive format (HKLM:\...
or
HKCU:\...), short format (HKLM\...), or full .NET provider format
(Registry::HKEY_LOCAL_MACHINE\...), looks up the corresponding hive from
the module's HiveMap, and returns the matching RegistryKey.
Returns $null
when the requested key does not exist.

## EXAMPLES

### Example 1

```powershell
Resolve-RegistryPath -Path 'HKLM:\Software\Microsoft'
```

### Example 2

```powershell
Resolve-RegistryPath -Path 'HKCU:\Control Panel\Desktop' -Writable
```

## PARAMETERS

### -Path

Registry path, e.g.
'HKLM:\Software\MyApp' or 'HKCU:\Control Panel'

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

### -View

Registry view to open.
Default follows the current process architecture.

```yaml
Type: Microsoft.Win32.RegistryView
DefaultValue: "[Microsoft.Win32.RegistryView]::Default"
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

### -Writable

Open the key with write access

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
HelpMessage: ""
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Win32.RegistryKey

## NOTES

## RELATED LINKS
