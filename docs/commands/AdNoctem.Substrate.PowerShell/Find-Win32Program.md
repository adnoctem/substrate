---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Find-Win32Program
---

# Find-Win32Program

## SYNOPSIS

Finds Win32 program uninstall registrations by wildcard name or exact registry path.

## SYNTAX

### Name

```
Find-Win32Program -Name <string> [-IncludeSystemComponent] [<CommonParameters>]
```

### RegistryPath

```
Find-Win32Program -RegistryPath <string> [-IncludeSystemComponent] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Resolves classic Win32 application registrations from uninstall registry
locations.
Use -Name for broad wildcard matching or -RegistryPath when a
caller has already selected a concrete uninstall key.

## EXAMPLES

### Example 1

```powershell
Find-Win32Program -Name '*Visual Studio Code*'
```

### Example 2

```powershell
Find-Win32Program -RegistryPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SomeApp'
```

## PARAMETERS

### -IncludeSystemComponent

Include hidden SystemComponent entries when resolving by name.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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

### -Name

Wildcard DisplayName pattern to resolve.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: Name
    Position: Named
    IsRequired: true
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -RegistryPath

Exact uninstall registry key path to normalize into a Win32 program object.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: RegistryPath
    Position: Named
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
