---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Write-OperationResultLog
---

# Write-OperationResultLog

## SYNOPSIS

Writes operation result objects to a JSON Lines log file.

## SYNTAX

### __AllParameterSets

```
Write-OperationResultLog [-Results] <IEnumerable> [[-ScriptName] <string>] [[-Name] <string>]
 [[-Path] <string>] [[-RunId] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Serializes each operation result as one compact JSON object per line in
a temp-directory log file.
The helper is intended for scripts that keep
console output concise but still need an auditable record of every
action, skipped action, and failure.

Logs are written to %TEMP%\PSFoundation\logs by default.
Each line
includes a timestamp, script name, and the properties already present on
the result object, such as Target, Source, Scope, Action, Status, Detail,
SkippedReason, or Error.

Consumers that want their own log directory pass -Name.
This module never
writes under another product's directory by default.

## EXAMPLES

### Example 1

```powershell
$path = Write-OperationResultLog -Results $results -ScriptName 'Remove-Bloatware'
Write-Log -Message "Operation log: $path" -Color Gray
```

Writes the accumulated operation results and prints the resulting path.

## PARAMETERS

### -Name

Directory name used under %TEMP% when Path is omitted.
Defaults to
AdNoctem.Substrate.
Consuming products supply their own name, for example
-Name 'winkit'.
A single path segment; separators are rejected.

```yaml
Type: System.String
DefaultValue: "'PSFoundation'"
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

### -Path

Optional explicit output file path.
When omitted, a timestamped .jsonl
file is created under %TEMP%\<Name>\logs.

```yaml
Type: System.String
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

### -Results

Operation result objects to serialize.

```yaml
Type: System.Collections.IEnumerable
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

### -RunId

Identifier assigned to log entries that do not already carry a RunId.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 4
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -ScriptName

Name used in the log entries and default file name.
Defaults to the
calling script name when available.

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

## OUTPUTS

### System.String

## NOTES

## RELATED LINKS
