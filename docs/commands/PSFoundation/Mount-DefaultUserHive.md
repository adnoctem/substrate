---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Mount-DefaultUserHive
---

# Mount-DefaultUserHive

## SYNOPSIS

Loads C:\Users\Default\NTUSER.DAT into the registry under HKU\<MountName>.

## SYNTAX

### __AllParameterSets

```
Mount-DefaultUserHive [[-MountName] <string>] [[-HivePath] <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Mounts the default user profile hive so it can be modified before
sealing an image (Sysprep / Audit Mode).
 Changes written here propagate
to every new user account created on the deployed system.
HKEY_USERS\.DEFAULT is the LocalSystem profile - do NOT write there for
this purpose.
 Use Dismount-DefaultUserHive to unload when done.
Requires elevation.

## EXAMPLES

### Example 1

```powershell
Mount-DefaultUserHive
Set-RegistryValue -Path 'Registry::HKEY_USERS\DefaultUser\Software\...' -Name 'Foo' -Value 1
Dismount-DefaultUserHive
```

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- cf
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

### -HivePath

Path to NTUSER.DAT.
Defaults to C:\Users\Default\NTUSER.DAT.

```yaml
Type: System.String
DefaultValue: (Join-Path $env:SystemDrive 'Users\Default\NTUSER.DAT')
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
HelpMessage: ''
```

### -MountName

Subkey name under HKEY_USERS to mount at.
Defaults to 'DefaultUser'.

```yaml
Type: System.String
DefaultValue: "'DefaultUser'"
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
HelpMessage: ''
```

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- wi
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

### System.String

## NOTES

## RELATED LINKS
