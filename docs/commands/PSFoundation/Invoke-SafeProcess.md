---
document type: cmdlet
external help file: PSFoundation-help.xml
HelpUri: ''
Locale: en-US
Module Name: PSFoundation
PlatyPS schema version: 2024-05-01
title: Invoke-SafeProcess
---

# Invoke-SafeProcess

## SYNOPSIS

Runs an external executable and captures stdout/stderr to a file or the pipeline.

## SYNTAX

### __AllParameterSets

```
Invoke-SafeProcess [-FilePath] <string> [[-ArgumentList] <string[]>] [[-OutputPath] <string>]
 [[-TimeoutSeconds] <int>] [[-CancellationToken] <CancellationToken>] [-PassThru] [-AsResult]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses System.Diagnostics.Process to invoke an executable with argument list
and redirects standard output and standard error.
The combined output is
written to -OutputPath.
Use -PassThru to return the output as a string
instead of writing to disk.

Designed for IR/forensics collection where external tools (reg.exe,
wevtutil.exe, systeminfo.exe, etc.) need to be called safely and their
output captured for later review.
Timeout and cancellation attempt to
terminate descendants as well as the child.
Detached processes are not
guaranteed to be terminated.
Programs must follow Windows command-line
quoting conventions on PowerShell 5.1.

## EXAMPLES

### Example 1

```powershell
Invoke-SafeProcess -FilePath 'whoami.exe' -ArgumentList @('/all') -OutputPath '.\whoami.txt'
```

### Example 2

```powershell
Invoke-SafeProcess -FilePath 'systeminfo.exe' -PassThru
```

## PARAMETERS

### -ArgumentList

Array of arguments.
Each element is one argument token.

```yaml
Type: System.String[]
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

### -AsResult

Return ExitCode, StdOut, StdErr, Duration, TimedOut and Cancelled instead
of legacy combined text.
Start and capture failures are terminating errors.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
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
HelpMessage: ''
```

### -CancellationToken

Optional .NET cancellation token.
Cancellation terminates the child.

```yaml
Type: System.Threading.CancellationToken
DefaultValue: '[System.Threading.CancellationToken]::None'
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
HelpMessage: ''
```

### -FilePath

Executable path (resolved from PATH when a bare name is supplied).

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

### -OutputPath

File to write combined stdout + stderr to.
When omitted and -PassThru is
not supplied, output is discarded.

```yaml
Type: System.String
DefaultValue: ''
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
HelpMessage: ''
```

### -PassThru

Return stdout as a string.
When combined with -OutputPath, output is
both written to disk and returned.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
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
HelpMessage: ''
```

### -TimeoutSeconds

Maximum execution time in seconds.
Zero (the default) waits indefinitely.

```yaml
Type: System.Int32
DefaultValue: 0
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
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.String

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
