using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace PSFoundation.Windows;

public enum ServiceStartupType { Automatic, Manual, Disabled }
public enum ServiceControlAction { Start, Stop, Pause, Resume }
public sealed class ServiceInfo
{
    public string Name { get; }
    public string DisplayName { get; }
    public string State { get; }
    public string StartMode { get; }
    public string? AccountName { get; }
    public string? ExecutableCommand { get; }
    public uint ProcessId { get; }
    public bool DelayedAutomaticStart { get; }
    internal ServiceInfo(CimInstance instance)
    {
        string Text(string name) => instance.CimInstanceProperties[name]?.Value as string ?? "";
        Name = Text("Name");
        DisplayName = Text("DisplayName");
        State = Text("State");
        StartMode = Text("StartMode");
        AccountName = instance.CimInstanceProperties["StartName"]?.Value as string;
        ExecutableCommand = instance.CimInstanceProperties["PathName"]?.Value as string;
        ProcessId = Convert.ToUInt32(instance.CimInstanceProperties["ProcessId"]?.Value, CultureInfo.InvariantCulture);
        DelayedAutomaticStart = Convert.ToBoolean(instance.CimInstanceProperties["DelayedAutoStart"]?.Value, CultureInfo.InvariantCulture);
    }
}

/// <summary>Local Windows service discovery and explicit control. Does not elevate, prompt, or infer bulk changes.</summary>
public sealed class ServiceManager
{
    public IReadOnlyList<ServiceInfo> GetServices(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var services = new List<ServiceInfo>();
        using (var session = CimSession.Create(null))
        using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
            foreach (var item in session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_Service", options))
                using (item)
                { cancellationToken.ThrowIfCancellationRequested(); services.Add(new ServiceInfo(item)); }
        return services.AsReadOnly();
    }
    /// <remarks>Offloads native calls. Cancellation is forwarded to CIM; an in-flight provider call may complete before observing it.</remarks>
    public Task<IReadOnlyList<ServiceInfo>> GetServicesAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetServices(timeout, cancellationToken), cancellationToken);
    public ServiceInfo? GetService(string name, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return GetServices(timeout, cancellationToken).SingleOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }
    /// <returns>The Win32_Service provider return code. Zero means the request succeeded; other codes are not Win32 last-error values.</returns>
    public uint SetStartupType(string name, ServiceStartupType startupType, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(ServiceStartupType), startupType))
            throw new ArgumentOutOfRangeException(nameof(startupType));
        var parameters = new CimMethodParametersCollection { CimMethodParameter.Create("StartMode", startupType.ToString(), CimType.String, CimFlags.In) };
        return Invoke(name, "ChangeStartMode", parameters, timeout, cancellationToken);
    }
    public Task<uint> SetStartupTypeAsync(string name, ServiceStartupType startupType, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => SetStartupType(name, startupType, timeout, cancellationToken), cancellationToken);
    /// <summary>Requests a transition without waiting for its final state or modifying dependencies. Retains the provider return code.</summary>
    public uint Control(string name, ServiceControlAction action, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(ServiceControlAction), action))
            throw new ArgumentOutOfRangeException(nameof(action));
        return Invoke(name, action + "Service", null, timeout, cancellationToken);
    }
    public Task<uint> ControlAsync(string name, ServiceControlAction action, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => Control(name, action, timeout, cancellationToken), cancellationToken);
    private static uint Invoke(string name, string method, CimMethodParametersCollection? parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ValidateName(name);
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        using (var session = CimSession.Create(null))
        using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
        using (var reference = new CimInstance("Win32_Service", @"root\cimv2"))
        {
            reference.CimInstanceProperties.Add(CimProperty.Create("Name", name, CimType.String, CimFlags.Key));
            using (var instance = session.GetInstance(@"root\cimv2", reference, options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var result = session.InvokeMethod(@"root\cimv2", instance, method, parameters, options))
                    return Convert.ToUInt32(result.ReturnValue.Value, CultureInfo.InvariantCulture);
            }
        }
    }
    internal static void ValidateTimeout(TimeSpan timeout)
    { if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(timeout)); }
    private static void ValidateName(string name)
    { if (string.IsNullOrWhiteSpace(name) || name.IndexOf('\0') >= 0) throw new ArgumentException("An exact service name is required.", nameof(name)); }
}
