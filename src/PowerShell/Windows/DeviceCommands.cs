using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.PowerShell.Infrastructure;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Windows;

internal static class DeviceOutput
{
    internal static PSObject Printer(PrinterInfo value) => SystemOutput.Object("Name", value.Name, "DeviceId", value.DeviceId,
        "DriverName", value.DriverName, "PortName", value.PortName, "Type", value.Type, "Shared", value.Shared, "Published", value.Published,
        "Network", value.Network, "Default", value.IsDefault, "Source", value.Source == PrinterInventorySource.Cim ? "CIM" : "PrintManagement");
}
[Cmdlet(VerbsCommon.Get, "PrintDevice"), OutputType(typeof(PSObject[]))]
public sealed class GetPrintDeviceCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        try
        {
            var inventory = new DeviceManager().GetPrinters(InventoryTimeout, allowFallback: true, cancellationToken: Cancellation);
            foreach (var error in inventory.Errors)
                WriteVerbose(error.Message);
            foreach (var printer in inventory.Printers)
                WriteObject(DeviceOutput.Printer(printer));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { WriteVerbose("Could not enumerate print devices: " + error.Message); }
    }
}
[Cmdlet(VerbsCommon.Get, "DefaultPrintDevice"), OutputType(typeof(PSObject))]
public sealed class GetDefaultPrintDeviceCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        try
        { foreach (var printer in new DeviceManager().GetDefaultPrinters(InventoryTimeout, Cancellation)) WriteObject(DeviceOutput.Printer(printer)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { WriteVerbose("Could not read default print device: " + error.Message); }
    }
}
[Cmdlet(VerbsCommon.Set, "DefaultPrintDevice", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium), OutputType(typeof(PSObject))]
public sealed class SetDefaultPrintDeviceCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0), ValidateNotNullOrEmpty] public string Name { get; set; } = "";
    [Parameter] public SwitchParameter PassThru { get; set; }
    protected override void ProcessRecord()
    {
        var manager = new DeviceManager();
        var printers = manager.GetPrinters(InventoryTimeout, false, cancellationToken: Cancellation).Printers
            .Where(printer => string.Equals(printer.Name, Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (printers.Length != 1)
        { ThrowTerminatingError(LegacyError.Create(printers.Length == 0 ? "Print device '" + Name + "' was not found." : "Multiple print devices named '" + Name + "' were found. Refusing to choose one.")); return; }
        PSObject Result(string status, string detail) => OperationOutput.Create(Name, "Win32_Printer", "SetDefault", status,
            new Dictionary<string, object?> { ["Detail"] = detail });
        if (printers[0].IsDefault)
        { WriteObject(Result("Skipped", "AlreadyDefault")); return; }
        if (!ShouldProcess(Name, "Set default print device"))
        { WriteObject(Result("Skipped", "WhatIf")); return; }
        try
        {
            var code = manager.SetDefaultPrinter(Name, InventoryTimeout, Cancellation);
            WriteObject(Result(code == 0 ? "Completed" : "Failed", code == 0 ? "Default printer changed." : "SetDefaultPrinter returned " + code + "."));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { WriteObject(Result("Failed", error.Message)); }
    }
}
[Cmdlet(VerbsCommon.Get, "ScanDevice"), OutputType(typeof(PSObject[]))]
public sealed class GetScanDeviceCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        try
        {
            foreach (var scanner in new DeviceManager().GetScanners(Cancellation))
                WriteObject(SystemOutput.Object("Name", scanner.Name, "DeviceId", scanner.DeviceId, "Manufacturer", scanner.Manufacturer,
                    "Type", scanner.Type, "Port", scanner.Port, "Server", scanner.Server, "DriverVersion", scanner.DriverVersion,
                    "WiaVersion", scanner.WiaVersion, "PnpId", scanner.PnpId, "Source", "WIA"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { WriteVerbose("Could not enumerate WIA scan devices: " + error.Message); }
    }
}
