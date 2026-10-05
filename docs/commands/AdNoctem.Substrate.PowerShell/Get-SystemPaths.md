---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-SystemPaths
---

# Get-SystemPaths

## SYNOPSIS

Returns standard product directory paths for tools, config, cache, data, and logs.

## SYNTAX

### __AllParameterSets

```
Get-SystemPaths [[-Name] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Provides a single structured lookup for the five canonical per-product
folders.
The -Name parameter controls the subdirectory name used under
each root and defaults to this module's own name, so a consuming product
supplies its own rather than inheriting another one's directories.

## EXAMPLES

### Example 1

```powershell
$paths = Get-SystemPaths
$paths.Data
C:\ProgramData\PSFoundation
```

### Example 2

```powershell
Get-SystemPaths -Name 'myapp' | Format-List
```

## PARAMETERS

### -Name

Subdirectory name under each root.
Defaults to 'PSFoundation'.
A single
path segment; separators are rejected.

```yaml
Type: System.String
DefaultValue: "'PSFoundation'"
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
