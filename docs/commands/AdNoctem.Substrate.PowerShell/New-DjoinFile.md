---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: New-DjoinFile
---

# New-DjoinFile

## SYNOPSIS

New-DjoinFile - Writes an offline-join blob in the format djoin.exe consumes.

## SYNTAX

### __AllParameterSets

```
New-DjoinFile [-Blob] <string> [[-DestinationFile] <FileInfo>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Writes the blob produced by New-OfflineDomainJoinBlob (or djoin.exe
/requestjoindomain /saveold) to a file in the format Windows Setup and
djoin.exe /loadfile expect: UTF-16LE with a BOM, blob data, and two
terminating null bytes.

## EXAMPLES

### Example 1

```powershell
# AD-side: provision the computer account and generate the join blob.
$result = New-OfflineDomainJoinBlob -ComputerName 'SRV001' -Domain 'contoso.com' -Credential (Get-Credential) -MachineAccountOU 'OU=Servers,DC=contoso,DC=com'
```

## PARAMETERS

### -Blob

The offline-join blob string.

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

### -DestinationFile

Full path of the file to write.
Defaults to C:\temp\djoin.tmp.

```yaml
Type: System.IO.FileInfo
DefaultValue: "'C:\\temp\\djoin.tmp'"
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
