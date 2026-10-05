---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Get-EncryptedCredentialFile
---

# Get-EncryptedCredentialFile

## SYNOPSIS

Get-EncryptedCredentialFile - Reads a credential stored by New-EncryptedCredentialFile.

## SYNTAX

### __AllParameterSets

```
Get-EncryptedCredentialFile [-Path] <string> [-KeyPath] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads the key file and the encrypted credential file created by
New-EncryptedCredentialFile and returns the decrypted PSCredential.

File format: the credential file holds the username in plaintext on the
first line and the encrypted password blob on the second; the key file
holds the key as base64.

## EXAMPLES

### Example 1

```powershell
Get-EncryptedCredentialFile -Path '.\svc-cred.bin' -KeyPath '.\svc-cred.key'
```

## PARAMETERS

### -KeyPath

Path of the key file.

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

### -Path

Path of the credential file.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Management.Automation.PSCredential

## NOTES

## RELATED LINKS
