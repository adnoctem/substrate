---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-SystemDisk
---

# Get-SystemDisk

## SYNOPSIS

Returns disk usage information for fixed volumes backed by physical disks.

## SYNTAX

### __AllParameterSets

```
Get-SystemDisk [-All] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses Win32_DiskDrive associations to include only volumes that resolve
back to a physical disk.
Provider-backed and cloud-mounted drives such as
Google Drive are ignored.
Filters to fixed drives by default and returns
total size, free space, used space, and the filesystem type.

## EXAMPLES

### Example 1

```powershell
Get-SystemDisk
```

### Example 2

```powershell
Get-SystemDisk -All
```

### Example 3

```powershell
Get-SystemDisk | Format-Table -AutoSize
```

## PARAMETERS

### -All

Include non-fixed volumes when they are backed by a physical disk.

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
HelpMessage: ''
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
