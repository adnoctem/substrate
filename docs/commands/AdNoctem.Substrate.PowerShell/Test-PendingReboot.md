---
document type: cmdlet
external help file: AdNoctem.Substrate.PowerShell-help.xml
HelpUri: ''
Locale: en-US
Module Name: AdNoctem.Substrate.PowerShell
PlatyPS schema version: 2024-05-01
title: Test-PendingReboot
---

# Test-PendingReboot

## SYNOPSIS

Returns whether the machine has a pending reboot, and which indicators
triggered.

## SYNTAX

### __AllParameterSets

```
Test-PendingReboot [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Checks ten independent read-only registry/WMI indicators of a pending
reboot:

1. CBS RebootPending key presence
2. CBS servicing state (RebootInProgress / PackagesPending)
3. PendingFileRenameOperations value
4. PendingFileRenameOperations2 value
5. DVDRebootSignal value under RunOnce
6. Netlogon JoinDomain / AvoidSpnSet values (pending domain join)
7. Windows Update RebootRequired / PostRebootReporting values
8. SCCM/MECM client SDK DetermineIfRebootPending (no-op when the client is not installed)
9. Computer rename queued (ActiveComputerName differs from ComputerName)
10. Win32_ComputerSystem.PendingSystemReboot

No elevation required - all checks are read-only.
Note: the ADDS
provisioning scripts in winkit embed a self-contained copy of this
indicator set for remote execution on freshly-provisioned targets that do
not have AdNoctem.Substrate.PowerShell installed; keep the two in sync.

## EXAMPLES

### Example 1

```powershell
Test-PendingReboot
```

A machine without pending indicators returns PendingReboot = False and an empty Indicators collection.

## PARAMETERS

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
