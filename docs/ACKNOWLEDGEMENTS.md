# AdNoctem.Substrate.PowerShell source acknowledgements

This records project provenance and retained attributions, distinguishing adapted work from technical references. The C# rewrite does not
erase credits from the predecessor implementation.

## Adapted work

- The predecessor's `Install-Font` credited
  [LeDragoX/Win-Debloat-Tools, Install-Font.psm1](https://github.com/LeDragoX/Win-Debloat-Tools/blob/main/src/lib/Install-Font.psm1). Its
  recorded attribution also credits [anthonyeden](https://github.com/anthonyeden) for the earlier font-installation gist.
  The current [FontManager](../src/Windows/FontManager.cs) carries the workflow into C# with validation, structured outcomes and explicit
  source-removal consent at the PowerShell boundary.
- Domain helpers retain links to [adnoctem/winkit](https://github.com/adnoctem/winkit), the related project from which this shared module
  evolved. Those links record project provenance rather than an additional third-party dependency.

## Evaluated patterns and reference data

- The predecessor's `Request-AdministratorPrivilege` referenced
  [Michael Casey's self-elevating script article](https://michael-casey.com/blog/self-elevating-powershell-and-batch-scripts/). The source
  identified that pattern as evaluated and superseded; the current [elevation adapter](../src/PowerShell/Security/ElevationCompatibility.cs)
  preserves host, parameters and working
  directory and adds a relaunch guard. This is a reference acknowledgement, not a claim that the present implementation was copied.
- WinGet error mappings, now in [ErrorTranslation.cs](../src/Diagnostics/ErrorTranslation.cs), use Microsoft's
  [Windows Package Manager return codes](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md).
  Windows, Office and PowerShell documentation links elsewhere in the module identify the relevant API contracts.

Source comments and upstream license notices remain authoritative for the attributed material. Similar function names alone are not evidence
of shared provenance.
