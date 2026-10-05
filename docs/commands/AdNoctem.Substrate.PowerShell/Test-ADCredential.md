---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-ADCredential
---

# Test-ADCredential

## SYNOPSIS

Test-ADCredential - Validates a username/password pair against AD.

## SYNTAX

### __AllParameterSets

```
Test-ADCredential [-Credential] <pscredential> [[-Domain] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Simulates an authentication request against the domain using
System.DirectoryServices.AccountManagement.PrincipalContext.
ValidateCredentials, without opening a session.
Returns $true only when
both the username and password are valid.

The domain is auto-detected from the credential when not specified.
Accepts pipeline input of one or more credentials.

## EXAMPLES

### Example 1

```powershell
Test-ADCredential -Credential (Get-Credential)
```

## PARAMETERS

### -Credential

The credential to validate.

```yaml
Type: System.Management.Automation.PSCredential
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - PSCredential
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

### -Domain

Domain to validate against.
Defaults to the credential's own domain.

```yaml
Type: System.String
DefaultValue: ""
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

### System.Management.Automation.PSCredential

## OUTPUTS

### System.Boolean

## NOTES

## RELATED LINKS
