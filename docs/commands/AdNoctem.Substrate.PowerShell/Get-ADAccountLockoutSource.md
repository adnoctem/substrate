---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-ADAccountLockoutSource
---

# Get-ADAccountLockoutSource

## SYNOPSIS

Get-ADAccountLockoutSource - Reports the source of account lockouts from the PDC.

## SYNTAX

### __AllParameterSets

```
Get-ADAccountLockoutSource [[-DomainName] <string>] [[-UserName] <string>] [[-StartTime] <datetime>]
 [[-Credential] <pscredential>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Dynamically resolves the PDC emulator for the domain (never hardcoded)
and queries its Security event log for Event ID 4740 (account locked
out), reporting which account was locked, when, and the ClientName of
the machine that triggered the lockout.
The ClientName pinpoints the
device with stale cached credentials that is repeatedly failing auth.

## EXAMPLES

### Example 1

```powershell
Get-ADAccountLockoutSource -UserName 'svc-backup' -StartTime (Get-Date).AddDays(-7)
```

## PARAMETERS

### -Credential

Optional alternative credential for the PDC query.

```yaml
Type: System.Management.Automation.PSCredential
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

### -DomainName

Domain to query.
Defaults to the current user's domain.

```yaml
Type: System.String
DefaultValue: $env:USERDOMAIN
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

### -StartTime

Only report lockouts at or after this time.
Defaults to the last 24
hours.

```yaml
Type: System.DateTime
DefaultValue: (Get-Date).AddDays(-1)
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

### -UserName

Account filter (wildcards supported).
Defaults to '*'.

```yaml
Type: System.String
DefaultValue: "'*'"
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
