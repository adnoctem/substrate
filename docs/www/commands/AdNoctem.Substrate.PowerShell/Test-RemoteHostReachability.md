---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ""
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-RemoteHostReachability
---

# Test-RemoteHostReachability

## SYNOPSIS

Tests which remote-management channels a host answers on.

## SYNTAX

### __AllParameterSets

```
Test-RemoteHostReachability [-ComputerName] <string[]> [-Credential <pscredential>] [-IncludeRdp]
 [-CredSSP] [-TimeoutSeconds <int>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Given a computer name, probes DNS resolution, ICMP ping, WSMAN
availability (Test-WSMan), the remote registry service (CIM probe), and
WMI/RPC connectivity (TCP port 135).
The RDP channel (TCP 3389) is only
probed when -IncludeRdp is supplied - not every managed host needs RDP
checked.
An optional CredSSP authentication attempt runs when both
-Credential and -CredSSP are supplied (cross-domain / workgroup
scenarios).

Every network-touching check has an explicit timeout (-TimeoutSeconds,
default 5s) - a filtered or unreachable host can never hang the call.

Returns one structured result object per host, with a convenience
boolean per channel plus a Channels collection carrying per-channel
detail and latency.

## EXAMPLES

### Example 1

```powershell
Test-RemoteHostReachability -ComputerName 'DC01' -IncludeRdp
```

## PARAMETERS

### -ComputerName

One or more host names or IP addresses to test.

```yaml
Type: System.String[]
DefaultValue: ""
SupportsWildcards: false
Aliases:
  - Computer
  - Host
  - Server
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: true
    ValueFromPipeline: true
    ValueFromPipelineByPropertyName: true
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Credential

Optional credential used for the CredSSP authentication attempt.

```yaml
Type: System.Management.Automation.PSCredential
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

### -CredSSP

Attempt a CredSSP WSMAN authentication probe.
Requires -Credential.

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

### -IncludeRdp

Also probe RDP (TCP port 3389).
Off by default.

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

### -TimeoutSeconds

Timeout ceiling for each network-touching check.
Defaults to 5.

```yaml
Type: System.Int32
DefaultValue: 5
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

### System.Management.Automation.PSObject

## NOTES

## RELATED LINKS
