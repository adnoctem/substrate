using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace PSFoundation.Windows;

public enum PrinterInventorySource { PrintManagement, Cim }
public sealed class PrinterInfo
{
    public string Name { get; }
    public string DeviceId { get; }
    public string? DriverName { get; }
    public string? PortName { get; }
    public uint? Type { get; }
    public bool Shared { get; }
    public bool Published { get; }
    public bool Network { get; }
    public bool IsDefault { get; }
    public PrinterInventorySource Source { get; }
    internal PrinterInfo(CimInstance value, PrinterInventorySource source, bool? isDefault = null)
    {
        object? Read(string name) => value.CimInstanceProperties[name]?.Value;
        string? Text(string name) => Read(name) as string;
        Name = Text("Name") ?? "";
        DeviceId = Text("DeviceID") ?? Name;
        DriverName = Text("DriverName");
        PortName = Text("PortName");
        Type = Read("Type") == null ? null : (uint?)Convert.ToUInt32(Read("Type"), CultureInfo.InvariantCulture);
        Shared = Convert.ToBoolean(Read("Shared"), CultureInfo.InvariantCulture);
        Published = Convert.ToBoolean(Read("Published"), CultureInfo.InvariantCulture);
        Network = Convert.ToBoolean(Read("Network"), CultureInfo.InvariantCulture);
        IsDefault = isDefault ?? Convert.ToBoolean(Read("Default"), CultureInfo.InvariantCulture);
        Source = source;
    }
}
public sealed class PrinterInventory
{
    public IReadOnlyList<PrinterInfo> Printers { get; }
    public IReadOnlyList<Exception> Errors { get; }
    internal PrinterInventory(IEnumerable<PrinterInfo> printers, IEnumerable<Exception> errors)
    { Printers = Array.AsReadOnly(printers.ToArray()); Errors = Array.AsReadOnly(errors.ToArray()); }
}
public sealed class ScannerInfo
{
    public string? Name { get; }
    public string? DeviceId { get; }
    public string? Manufacturer { get; }
    public int? Type { get; }
    public string? Port { get; }
    public string? Server { get; }
    public string? DriverVersion { get; }
    public string? WiaVersion { get; }
    public string? PnpId { get; }
    internal ScannerInfo(IDictionary<string, object?> values)
    {
        object? Read(string name) => values.TryGetValue(name, out var value) ? value : null;
        string? Text(string name) => Read(name) == null ? null : Convert.ToString(Read(name), CultureInfo.InvariantCulture);
        Name = Text("Name");
        DeviceId = Text("Unique Device ID");
        Manufacturer = Text("Manufacturer");
        Type = Read("Type") == null ? null : (int?)Convert.ToInt32(Read("Type"), CultureInfo.InvariantCulture);
        Port = Text("Port");
        Server = Text("Server");
        DriverVersion = Text("Driver Version");
        WiaVersion = Text("WIA Version");
        PnpId = Text("PnP ID String");
    }
}

/// <summary>Native Windows printer and WIA discovery. Native objects are detached and released before results reach the caller.</summary>
public sealed class DeviceManager
{
    public PrinterInventory GetPrinters(TimeSpan timeout, bool preferPrintManagement = true, bool allowFallback = false, CancellationToken cancellationToken = default)
    {
        CheckTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<Exception>();
        var legacy = new List<PrinterInfo>();
        using (var session = CimSession.Create(null))
        using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
        {
            try
            {
                foreach (var printer in session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_Printer", options))
                    using (printer)
                    { cancellationToken.ThrowIfCancellationRequested(); legacy.Add(new PrinterInfo(printer, PrinterInventorySource.Cim)); }
            }
            catch (CimException error) when (preferPrintManagement && allowFallback) { errors.Add(error); }
            if (preferPrintManagement)
            {
                try
                {
                    var modern = new List<PrinterInfo>();
                    foreach (var printer in session.QueryInstances(@"root\StandardCimv2", "WQL", "SELECT * FROM MSFT_Printer", options))
                        using (printer)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var name = printer.CimInstanceProperties["Name"]?.Value as string;
                            modern.Add(new PrinterInfo(printer, PrinterInventorySource.PrintManagement,
                                legacy.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.IsDefault ?? false));
                        }
                    return new PrinterInventory(modern, errors);
                }
                catch (CimException error) when (allowFallback) { errors.Add(error); }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new PrinterInventory(legacy, errors);
    }
    /// <remarks>Offloads synchronous CIM calls; cancellation and timeouts depend on the native provider honoring them.</remarks>
    public Task<PrinterInventory> GetPrintersAsync(TimeSpan timeout, bool preferPrintManagement = true, bool allowFallback = false, CancellationToken cancellationToken = default)
        => Task.Run(() => GetPrinters(timeout, preferPrintManagement, allowFallback, cancellationToken), cancellationToken);
    public IReadOnlyList<PrinterInfo> GetDefaultPrinters(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Array.AsReadOnly(GetPrinters(timeout, false, cancellationToken: cancellationToken).Printers.Where(printer => printer.IsDefault).ToArray());

    /// <summary>Rechecks the named printer, requires one exact match, then changes the current identity's default. Does not elevate.</summary>
    /// <returns>The provider return code; zero means success. Native query/transport failures throw.</returns>
    public uint SetDefaultPrinter(string name, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A printer name is required.", nameof(name));
        CheckTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        using (var session = CimSession.Create(null))
        using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
        {
            CimInstance? selected = null;
            try
            {
                foreach (var printer in session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_Printer", options))
                    using (printer)
                        if (string.Equals(printer.CimInstanceProperties["Name"]?.Value as string, name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (selected != null)
                                throw new InvalidOperationException("Multiple printers match the name.");
                            selected = new CimInstance(printer);
                        }
                if (selected == null)
                    throw new InvalidOperationException("Printer does not exist: " + name);
                cancellationToken.ThrowIfCancellationRequested();
                using (var result = session.InvokeMethod(@"root\cimv2", selected, "SetDefaultPrinter", null, options))
                    return Convert.ToUInt32(result.ReturnValue.Value, CultureInfo.InvariantCulture);
            }
            finally { selected?.Dispose(); }
        }
    }
    public Task<uint> SetDefaultPrinterAsync(string name, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => SetDefaultPrinter(name, timeout, cancellationToken), cancellationToken);

    public IReadOnlyList<ScannerInfo> GetScanners(CancellationToken cancellationToken = default)
        => GetScannersAsync(cancellationToken).GetAwaiter().GetResult();
    /// <remarks>WIA is created and released on a dedicated STA worker. No caller apartment or UI is required.
    /// Cancellation is checked between COM calls; an in-flight driver call cannot be interrupted.</remarks>
    public Task<IReadOnlyList<ScannerInfo>> GetScannersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<IReadOnlyList<ScannerInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            object? manager = null, devices = null;
            IReadOnlyList<ScannerInfo>? inventory = null;
            Exception? failure = null;
            var cancelled = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                manager = Activator.CreateInstance(System.Type.GetTypeFromProgID("WIA.DeviceManager", true)!);
                devices = Get(manager!, "DeviceInfos");
                var scanners = new List<ScannerInfo>();
                var count = Convert.ToInt32(Get(devices!, "Count"), CultureInfo.InvariantCulture);
                for (var i = 1; i <= count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    object? device = null, properties = null;
                    try
                    {
                        device = Get(devices!, "Item", i);
                        properties = Get(device!, "Properties");
                        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        var propertyCount = Convert.ToInt32(Get(properties!, "Count"), CultureInfo.InvariantCulture);
                        for (var p = 1; p <= propertyCount; p++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            object? property = null;
                            try
                            {
                                property = Get(properties!, "Item", p);
                                var name = Convert.ToString(Get(property!, "Name"), CultureInfo.InvariantCulture)!;
                                if (new[] { "Name", "Unique Device ID", "Manufacturer", "Type", "Port", "Server", "Driver Version", "WIA Version", "PnP ID String" }.Contains(name))
                                    values[name] = Get(property!, "Value");
                            }
                            finally { Release(property); }
                        }
                        scanners.Add(new ScannerInfo(values));
                    }
                    finally { try { Release(properties); } finally { Release(device); } }
                }
                cancellationToken.ThrowIfCancellationRequested();
                inventory = scanners.AsReadOnly();
            }
            catch (OperationCanceledException) { cancelled = true; }
            catch (Exception error) { failure = error; }
            finally
            {
                try
                { Release(devices); }
                catch (Exception error) { failure = failure ?? error; }
                try
                { Release(manager); }
                catch (Exception error) { failure = failure ?? error; }
            }
            if (failure != null)
                completion.TrySetException(failure);
            else if (cancelled)
                completion.TrySetCanceled();
            else
                completion.TrySetResult(inventory!);
        })
        { IsBackground = true, Name = "PSFoundation WIA inventory" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        return completion.Task;
    }
    private static object? Get(object value, string name, params object[] args)
        => value.GetType().InvokeMember(name, BindingFlags.GetProperty, null, value, args, CultureInfo.InvariantCulture);
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private static void CheckTimeout(TimeSpan timeout)
    { if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(timeout)); }
}
