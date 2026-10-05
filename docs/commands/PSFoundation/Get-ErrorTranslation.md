---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Get-ErrorTranslation
---

# Get-ErrorTranslation

## SYNOPSIS

Translates known deployment error codes into operator guidance.

## SYNTAX

### Record (Default)

```
Get-ErrorTranslation -ErrorRecord <ErrorRecord> [-Domain <string>] [<CommonParameters>]
```

### Code

```
Get-ErrorTranslation -Code <Object> -Domain <string> [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses separate Appx, Winget, Dism, and Msi tables.
Returns Code, Domain,
Benign, Detail, and Matched, or no result for an unknown/ambiguous error.
Code is an uppercase hexadecimal HRESULT, or a decimal MSI exit code.
Recognized exception HRESULTs take precedence (outer to inner), followed
by codes in ErrorDetails and exception messages.
Multiple distinct known
codes in text are ambiguous and produce no result.
Text matching accepts
hexadecimal HRESULTs and signed/unsigned decimal HRESULTs, never short
decimal substrings that could be counts or identifiers.
Benign means an unconditional success code, not permission to suppress
an error merely because it is recognized.
Conditional cases, including
already-installed versions, remain false; the caller decides whether its
intended state is satisfied.
Reboot success codes retain guidance about
completing the restart.
This function does not log, retry, or change state.

## EXAMPLES

### Example 1

```powershell
Get-ErrorTranslation -ErrorRecord $_ -Domain Appx
```

### Example 2

```powershell
Get-ErrorTranslation -Code 3010 -Domain Msi
```

## PARAMETERS

### -Code

A native exit code or HRESULT, as an integer or hexadecimal string.
A domain is required for this parameter set to disambiguate native codes.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Code
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Domain

Restrict lookup to Appx, Winget, Dism, or Msi.
WinGet's table describes
its own codes; translate wrapped installer failures in their own domain.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Code
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Record
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ErrorRecord

A caught or pipeline ErrorRecord.
Defaults to searching all domains.

```yaml
Type: System.Management.Automation.ErrorRecord
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Record
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
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

### System.Management.Automation.ErrorRecord

## OUTPUTS

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
