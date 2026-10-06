---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-ADFSMORoleHolder
---

# Get-ADFSMORoleHolder

## SYNOPSIS

Get-ADFSMORoleHolder - Reports all five FSMO role holders in one call.

## SYNTAX

### __AllParameterSets

```
Get-ADFSMORoleHolder [[-Credential] <pscredential>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Queries the forest and domain for the Schema Master, Domain Naming
Master, Infrastructure Master, RID Master, and PDC Emulator role
holders.
Loads the ActiveDirectory module if it is not already loaded.

## EXAMPLES

### Example 1

```powershell
Get-ADFSMORoleHolder
```

## PARAMETERS

### -Credential

Optional alternative credential for the queries.

```yaml
Type: System.Management.Automation.PSCredential
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - RunAs
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
