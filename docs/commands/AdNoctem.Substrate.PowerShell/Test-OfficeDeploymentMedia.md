---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-OfficeDeploymentMedia
---

# Test-OfficeDeploymentMedia

## SYNOPSIS

Validates an Office package, manifest, payloads, paths, and write protection.

## SYNTAX

### __AllParameterSets

```
Test-OfficeDeploymentMedia [-SourcePath] <string> [[-Configuration] <Object>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

C# validates schema-2 manifests and protected payloads.
The compatibility boundary retains the original JSON object and fingerprint.
Validation performs no writes, downloads or native Office execution.

## EXAMPLES

### EXAMPLE 1

```powershell
Test-OfficeDeploymentMedia -SourcePath C:\Media\Office -Configuration $target
```

## PARAMETERS

### -Configuration

Optional target whose requested languages must be available in the package.

```yaml
Type: System.Object
DefaultValue: ''
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
HelpMessage: ''
```

### -SourcePath

Absolute package directory containing psfoundation-office-media.json.

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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
