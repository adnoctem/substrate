---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Find-ServiceAccountUsage
---

# Find-ServiceAccountUsage

## SYNOPSIS

Finds where a service account is used across services and scheduled tasks.

## SYNTAX

### __AllParameterSets

```
Find-ServiceAccountUsage [-Name] <string[]> [[-ComputerName] <string[]>]
 [[-Credential] <pscredential>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Given an account name (e.g.
'DOMAIN\svc-backup') and one or more
computers, searches Windows services (Win32_Service.StartName) and
scheduled tasks (schtasks, 'Run As User') for matches.
Use this before
rotating a service account's password - it reports every place a
manually-run credential could be hiding.

-Credential is a plain optional parameter with no default value, so
non-interactive callers are never forced into a Get-Credential prompt.

## EXAMPLES

### Example 1

```powershell
Find-ServiceAccountUsage -Name 'svc-backup' -ComputerName 'SRV01','SRV02'
```

## PARAMETERS

### -ComputerName

Computer(s) to search.
Defaults to the local computer.

```yaml
Type: System.String[]
DefaultValue: "@('localhost')"
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
HelpMessage: ""
```

### -Credential

Optional credential for remote computers.

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

### -Name

Account name(s) to search for (wildcards supported).

```yaml
Type: System.String[]
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: true
    ValueFromPipeline: true
    ValueFromPipelineByPropertyName: true
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

### System.String

### System.String

### System.String

### System.String

### System.String

### System.String

### System.String

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

Service and scheduled-task principals are read through CIM with remote credentials on the session. Task states use native enum names; discovery does not depend on localized schtasks CSV headers.

## RELATED LINKS
