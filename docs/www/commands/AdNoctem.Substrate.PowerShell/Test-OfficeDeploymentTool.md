---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-OfficeDeploymentTool
---

# Test-OfficeDeploymentTool

## SYNOPSIS

Verifies the signature, identity, and minimum version of ODT setup.exe.

## SYNTAX

### __AllParameterSets

```
Test-OfficeDeploymentTool [-OdtPath] <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Does not execute or download the tool.
Requires a valid Microsoft
Corporation signature and a recognized Office executable identity.
Current ODT uses Bootstrapper.exe as its embedded original filename;
the filename on disk is not that version-resource field.
Invalid assessments retain UntrustedTool and explain the failed check.

## EXAMPLES

### Example 1

```powershell
Test-OfficeDeploymentTool -OdtPath C:\Tools\ODT\setup.exe
```

## PARAMETERS

### -OdtPath

Existing Office Deployment Tool setup.exe.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

ODT trust requires native embedded Microsoft Authenticode evidence, including revocation checks. Catalog-only signatures and missing revocation evidence are not accepted. A reachable download URL is not trust verification.

## RELATED LINKS
