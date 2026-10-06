---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-CertificateInventory
---

# Get-CertificateInventory

## SYNOPSIS

Get-CertificateInventory - Lists certificates in local or remote stores with expiry filtering.

## SYNTAX

### __AllParameterSets

```
Get-CertificateInventory [[-Name] <string[]>] [[-Subject] <string>] [-StorePath] <string>
 [[-DaysLeft] <int>] [[-Credential] <pscredential>] [-NotExpired] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Queries a certificate store (e.g.
Cert:\LocalMachine\My) on the local
computer or on remote computers, returning one object per certificate
with DaysUntilExpired, Template, and formatted extensions.

-DaysLeft filters to certificates expiring within that many days
(e.g.
-DaysLeft 30 for a standing "what expires in the next 30 days"
report); -NotExpired excludes already-expired certificates.
The store
path is exposed as a dynamic parameter validated against the live Cert:
drive, so an invalid store path is rejected at binding time.

## EXAMPLES

### Example 1

```powershell
Get-CertificateInventory -StorePath Cert:\LocalMachine\My -DaysLeft 30 -NotExpired
Get-CertificateInventory -ComputerName 'SRV01' -StorePath Cert:\LocalMachine\My -DaysLeft 60
```

## PARAMETERS

### -Credential

Optional credential for remote computers.

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

### -DaysLeft

Only return certificates expiring within this many days.
When omitted,
no expiry-window filter is applied.

```yaml
Type: System.Int32
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

Computer name(s) to query.
Defaults to localhost.

```yaml
Type: System.String[]
DefaultValue: "@('localhost')"
SupportsWildcards: false
Aliases:
  - ComputerName
  - Computer
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: false
    ValueFromPipeline: true
    ValueFromPipelineByPropertyName: true
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -NotExpired

Exclude certificates that have already expired.

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

### -StorePath

Required certificate-provider path, for example `Cert:\LocalMachine\My` or `Cert:\CurrentUser\My`.
Valid store names are discovered from the local certificate provider at binding time; the documentation does not capture a machine's store list.

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

### -Subject

Subject filter (wildcards supported).
Defaults to all.

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

### System.String

### System.String

### System.String

### System.String

### System.String

### System.String

### System.String

## OUTPUTS

### System.Object

## NOTES

Returned certificates are detached public certificates owned by the recipient; dispose them when finished. Template text comes from the actual certificate-template extension. Remote reads use Windows certificate-store access rather than WinRM. Remote CurrentUser requires a loaded SID hive; no interactive profile is created.

## RELATED LINKS
