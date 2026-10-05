---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: ConvertTo-RegistryProviderPath
---

# ConvertTo-RegistryProviderPath

## SYNOPSIS

Converts a registry path to PS drive format suitable for provider cmdlets.

## SYNTAX

### __AllParameterSets

```
ConvertTo-RegistryProviderPath [-Path] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Accepts short hive notation (HKLM\...), PS drive notation (HKLM:\...),
Registry:: prefix (Registry::HKEY_LOCAL_MACHINE\...), or long .NET names
(HKEY_LOCAL_MACHINE\...).
Returns a provider path that can be passed to
registry provider cmdlets.
HKLM and HKCU use their default PowerShell
drives; hives such as HKEY_USERS use the Registry:: provider path because
aliases like HKU: are not present in every shell.

## EXAMPLES

### Example 1

```powershell
ConvertTo-RegistryProviderPath 'HKLM\Software\MyApp'
HKLM:\Software\MyApp
```

### Example 2

```powershell
ConvertTo-RegistryProviderPath 'Registry::HKEY_CURRENT_USER\Control Panel'
HKCU:\Control Panel
```

## PARAMETERS

### -Path

Registry path in any supported format

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
