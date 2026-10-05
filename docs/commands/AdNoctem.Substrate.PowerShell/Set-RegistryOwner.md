---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Set-RegistryOwner
---

# Set-RegistryOwner

## SYNOPSIS

Set-RegistryOwner - Takes ownership of a registry key, optionally including all subkeys.

## SYNTAX

### __AllParameterSets

```
Set-RegistryOwner [-Hive] <string> [-Key] <string> [-SID <SecurityIdentifier>] [-Recurse] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Takes ownership of the specified registry key (owned by e.g.
TrustedInstaller or a
vendor installer) and grants the owning account FullControl.

Uses RtlAdjustPrivilege P/Invoke to acquire SeTakeOwnership, SeBackup, and SeRestore,
then manipulates the key ACL via System.Security.AccessControl.RegistrySecurity.
Ownership is granted to the current user by default, or to any account via -SID.

Requires elevation: the process must hold an administrator token, otherwise the
privilege adjustment and ACL writes will fail.
Recurse is on by default: subkeys
are taken over recursively, with access rules propagated.

## EXAMPLES

### Example 1

```powershell
Set-RegistryOwner -Hive 'HKLM' -Key 'SOFTWARE\MyApp'
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

### -Hive

Registry hive, e.g.
HKLM, HKCU, HKCR, HKCC, HKU (or HKEY_* long forms).

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

### -Key

Key path below the hive, e.g.
'SOFTWARE\WOW6432Node\Classes\CLSID\{abc}'.

```yaml
Type: System.String
DefaultValue: ''
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
HelpMessage: ''
```

### -Recurse

Take ownership of the key and all subkeys.
Defaults to $true.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: $true
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

### -SID

SID of the account to grant ownership to.
Defaults to the current user.

```yaml
Type: System.Security.Principal.SecurityIdentifier
DefaultValue: ''
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
