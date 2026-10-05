---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: New-OperationResult
---

# New-OperationResult

## SYNOPSIS

Creates a structured result object for script and library operations.

## SYNTAX

### __AllParameterSets

```
New-OperationResult [[-Target] <string>] [[-Source] <string>] [[-Scope] <string>]
 [[-Action] <string>] [[-Status] <string>] [[-Detail] <string>] [[-SkippedReason] <string>]
 [[-ErrorMessage] <string>] [[-Property] <hashtable>] [[-Changed] <bool>]
 [[-AlreadyCompliant] <bool>] [[-Before] <Object>] [[-After] <Object>] [[-ExitCode] <int>]
 [[-RebootRequired] <bool>] [[-Duration] <timespan>] [[-RunId] <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Builds a small, consistent PSCustomObject that scripts can return when
they need to report planned, skipped, completed, or failed work.
The
helper keeps the common fields in a predictable order while omitting
optional fields that were not supplied, which lets each script keep a
compact output shape.

Target, Action, and Status are always present.
Source, Scope, Detail,
SkippedReason, and Error are included only when the matching parameter is
supplied.
Additional fields can be appended through -Property for
script-specific metadata without creating another custom result helper.

## EXAMPLES

### Example 1

```powershell
New-OperationResult -Target 'Microsoft.GetHelp' -Source 'UPFAppxPackage' -Action 'Uninstall' -Status 'Skipped' -SkippedReason 'NoMatch'
```

Creates a package-style lifecycle result with Target, Source, Action,
Status, and SkippedReason.

### Example 2

```powershell
New-OperationResult -Target 'HKCU:\Console\%%Startup\DelegationTerminal' -Scope 'CurrentUser' -Action 'SetValue' -Status 'Completed' -Detail 'Terminal delegation default applied.'
```

Creates a terminal configuration result with Target, Scope, Action,
Status, and Detail.

### Example 3

```powershell
New-OperationResult -Target 'OneDrive' -Action 'Uninstall' -Status 'Skipped' -Detail 'No OneDrive uninstaller was found.'
```

Creates a compact script result that does not include Source or Scope
columns because they were not supplied.

## PARAMETERS

### -Action

Operation performed or planned, such as Install, Uninstall, SetValue,
RemoveShortcut, or Disable.

```yaml
Type: System.String
DefaultValue: ''
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

### -After

Optional observed state after the operation, including an explicit null.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 12
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -AlreadyCompliant

Whether the requested state was already satisfied before the operation.

```yaml
Type: System.Boolean
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 10
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Before

Optional captured state before the operation, including an explicit null.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 11
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Changed

Whether the operation is known to have changed state.
Omit when unknown.

```yaml
Type: System.Boolean
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 9
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Detail

Optional human-readable detail that explains the result.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 5
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Duration

Elapsed operation time as a TimeSpan.

```yaml
Type: System.TimeSpan
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 15
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ErrorMessage

Optional failure detail.
This is emitted as the Error property to avoid
assigning to PowerShell's automatic $Error variable.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 7
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ExitCode

Native process exit code.
Does not replace the existing Status field.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 13
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Property

Optional hashtable of additional properties appended after the common
fields.
Existing common fields are not overwritten.

```yaml
Type: System.Collections.Hashtable
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 8
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -RebootRequired

Whether the operation reports that a restart is required.

```yaml
Type: System.Boolean
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 14
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -RunId

Caller-supplied identifier shared by operations in the same run.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 16
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Scope

Optional scope affected by the operation, such as CurrentUser, Machine,
DefaultUser, or AllExistingUsers.

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

### -SkippedReason

Optional machine-readable reason when Status is Skipped.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 6
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Source

Optional subsystem or provider that handled the operation, such as
Registry, FileSystem, WinGet, Win32Program, or UPFAppxPackage.

```yaml
Type: System.String
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

### -Status

Result state, such as Completed, Removed, Skipped, Failed, DryRun, or an
ExitCode:n value.

```yaml
Type: System.String
DefaultValue: ''
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

### -Target

Object targeted by the operation, such as a package name, registry value,
file path, scheduled task, or capability name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
