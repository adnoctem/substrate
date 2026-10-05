---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: ConvertFrom-RegistryPolicy
---

# ConvertFrom-RegistryPolicy

## SYNOPSIS

Reads registry.pol records without applying policy or requiring LGPO.

## SYNTAX

### __AllParameterSets

```
ConvertFrom-RegistryPolicy [-Path] <string> [-Raw] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Validates a PReg version 1 binary file and emits records in file order,
retaining duplicates and special policy directives.
Keys are relative to
a hive; the file's Machine/User location determines that hive.
Type is a numeric registry type.
Data is a string, string array, UInt32,
UInt64, or byte array.
Zero-length data is null.
Unknown types remain
opaque bytes.
EXPAND_SZ variables are not expanded.
Files are limited to
64 MiB and individual payloads to 65535 bytes.
Malformed files terminate
with path and byte-offset information; no partial records are emitted.

## EXAMPLES

### Example 1

```powershell
ConvertFrom-RegistryPolicy -Path '.\Machine\registry.pol'
```

### Example 2

```powershell
ConvertFrom-RegistryPolicy -Path '.\source.pol' -Raw | ConvertTo-RegistryPolicy -Path '.\copy.pol'
```

## PARAMETERS

### -Path

Literal filesystem path to a registry.pol file.

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

### -Raw

Return every payload as bytes, including empty arrays, tagged with the
PSFoundation.RegistryPolicy.RawEntry type name.
The writer recognizes
this type for lossless binary round trips, including opaque data.

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
