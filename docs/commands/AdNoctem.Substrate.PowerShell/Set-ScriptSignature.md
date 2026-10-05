---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Set-ScriptSignature
---

# Set-ScriptSignature

## SYNOPSIS

Set-ScriptSignature - Bulk Authenticode-signs PowerShell scripts under a folder.

## SYNTAX

### __AllParameterSets

```
Set-ScriptSignature [-Path] <string> [-Certificate] <X509Certificate2> [[-TimestampServer] <string>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Recursively signs every .ps1/.psm1/.psd1 file under -Path with the
supplied code-signing certificate.
-TimestampServer optionally adds a
trusted timestamp so signatures survive certificate expiry.

The certificate is deliberately a parameter, not generated here: for an
organization with AD, a certificate issued by an internal AD CS CA is
the appropriate source (audit trail, fleet scalability); a purchased
code-signing certificate is the separate consideration for public
PSGallery publishing.
Certificate sourcing is a deliberate decision, not
a script default.

## EXAMPLES

### Example 1

```powershell
$cert = Get-ChildItem -Path 'Cert:\LocalMachine\My' | Where-Object Subject -eq 'CN=PSF Code Signing'
Set-ScriptSignature -Path '.\src' -Certificate $cert -TimestampServer 'http://timestamp.digicert.com'
```

## PARAMETERS

### -Certificate

X509Certificate2 code-signing certificate to sign with.

```yaml
Type: System.Security.Cryptography.X509Certificates.X509Certificate2
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

### -Path

Folder to scan recursively for .ps1/.psm1/.psd1 files.

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

### -TimestampServer

Optional RFC 3161 / Authenticode timestamp server URL, e.g.
http://timestamp.digicert.com.
Timestamps make signatures survive
certificate expiry.

```yaml
Type: System.String
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
