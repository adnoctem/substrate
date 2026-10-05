---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: New-OfflineDomainJoinBlob
---

# New-OfflineDomainJoinBlob

## SYNOPSIS

New-OfflineDomainJoinBlob - Provisions an AD computer account and returns the offline-join blob.

## SYNTAX

### __AllParameterSets

```
New-OfflineDomainJoinBlob [-ComputerName] <string> [-Domain] <string> [[-Credential] <pscredential>]
 [[-MachineAccountOU] <string>] [[-DCName] <string>] [[-ProvisionOptions] <uint>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Fully native offline domain join: provisions the computer account in
Active Directory and generates the join blob via the
NetCreateProvisioningPackage Win32 API (P/Invoke) - no djoin.exe
dependency.
When -Credential is supplied, the API runs under that
account's impersonated context; otherwise the current context is used.

The returned blob is consumed by New-DjoinFile (or djoin.exe /loadfile)
on the target machine - join it during unattended setup or first boot,
before it has live DC connectivity.

## EXAMPLES

### Example 1

```powershell
# AD-side: provision the computer account and generate the join blob.
$result = New-OfflineDomainJoinBlob -ComputerName 'SRV001' -Domain 'contoso.com' -Credential (Get-Credential) -MachineAccountOU 'OU=Servers,DC=contoso,DC=com'
```

## PARAMETERS

### -ComputerName

Computer account name to provision.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - MachineName
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

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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
HelpMessage: ""
```

### -Credential

Optional credential with rights to create computer accounts.
When
omitted, the current context is used.

```yaml
Type: System.Management.Automation.PSCredential
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 2
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -DCName

Optional target domain controller.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 4
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Domain

Domain (DNS or NetBIOS) to join.

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

### -MachineAccountOU

Optional distinguished name of the OU for the machine account, e.g.
'OU=Computers,DC=contoso,DC=com'.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 3
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -ProvisionOptions

NetCreateProvisioningPackage provisioning options.
Defaults to 2
(NETSETUP_PROVISION_REUSE_ACCOUNT, matching the reference behaviour).

```yaml
Type: System.UInt32
DefaultValue: 2
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 5
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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
