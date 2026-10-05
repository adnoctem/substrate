---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-UPFAppxPackageRemovalSafety
---

# Test-UPFAppxPackageRemovalSafety

## SYNOPSIS

Classifies whether a UPF AppX/MSIX package should be protected from removal by default.

## SYNTAX

### __AllParameterSets

```
Test-UPFAppxPackageRemovalSafety [-InputObject] <psobject> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Checks a normalized package inventory object for non-removable,
framework, resource, and protected-name patterns.
Removal helpers use this
classification to skip Store, App Installer, framework runtimes, and
shell/security-adjacent packages unless the caller deliberately opts into
protected removal.

## EXAMPLES

### Example 1

```powershell
Get-UPFAppxPackage -Name 'Microsoft.WindowsStore' | Test-UPFAppxPackageRemovalSafety
```

## PARAMETERS

### -InputObject

Package object returned by Get-UPFAppxPackage or embedded in a
Find-UPFAppxPackage result.

```yaml
Type: System.Management.Automation.PSObject
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
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

### System.Management.Automation.PSObject

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
