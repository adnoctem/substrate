using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Windows;

public enum RebootIndicator
{
    CBSRebootPending, CBSServicingState, PendingFileRenameOperations, PendingFileRenameOperations2, DVDRebootSignal,
    NetlogonJoin, WindowsUpdate, SCCMClient, ComputerRename, CIMPendingReboot
}
public enum RebootProbeStatus { Clear, Pending, Unavailable, Failed }
/// <summary>One reboot indicator's state, retaining unavailable or failed reads separately from a clear result.</summary>
public sealed class RebootProbe
{
    public RebootIndicator Indicator { get; }
    public RebootProbeStatus Status { get; }
    public Exception? Error { get; }
    internal RebootProbe(RebootIndicator indicator, RebootProbeStatus status, Exception? error = null) { Indicator = indicator; Status = status; Error = error; }
}
/// <summary>Aggregated reboot evidence; a failed probe must not be interpreted as proof that no restart is needed.</summary>
public sealed class RebootStatus
{
    public IReadOnlyList<RebootProbe> Probes { get; }
    public bool IsPending => Probes.Any(probe => probe.Status == RebootProbeStatus.Pending);
    public bool IsComplete => Probes.All(probe => probe.Status != RebootProbeStatus.Failed);
    internal RebootStatus(IEnumerable<RebootProbe> probes) => Probes = Array.AsReadOnly(probes.ToArray());
}

/// <summary>Collects read-only reboot evidence. Missing optional providers differ from failed checks; no reboot is initiated.</summary>
public sealed class RebootManager
{
    private readonly RegistryManager registry;
    public RebootManager(RegistryManager? registry = null)
    {
        this.registry = registry ?? new RegistryManager();
        if (this.registry.MachineName != null)
            throw new ArgumentException("Use a local registry manager.", nameof(registry));
    }
    public RebootStatus GetStatus(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        var probes = new List<RebootProbe>();
        bool Key(string path) => registry.KeyExists(RegistryPath.Parse(@"HKLM\" + path));
        bool Value(string path, string name) => registry.ValueExists(RegistryPath.Parse(@"HKLM\" + path), name);
        void Check(RebootIndicator indicator, Func<bool> query)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            { probes.Add(new RebootProbe(indicator, query() ? RebootProbeStatus.Pending : RebootProbeStatus.Clear)); }
            catch (Exception error) when (error is IOException || error is SecurityException || error is UnauthorizedAccessException || error is InvalidCastException || error is NotSupportedException)
            { probes.Add(new RebootProbe(indicator, RebootProbeStatus.Failed, error)); }
        }
        const string cbs = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing";
        const string session = @"SYSTEM\CurrentControlSet\Control\Session Manager";
        const string netlogon = @"SYSTEM\CurrentControlSet\Services\Netlogon";
        const string update = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update";
        Check(RebootIndicator.CBSRebootPending, () => Key(cbs + @"\RebootPending"));
        Check(RebootIndicator.CBSServicingState, () => Value(cbs, "RebootInProgress") || Value(cbs, "PackagesPending") || Key(cbs + @"\RebootInProgress") || Key(cbs + @"\PackagesPending"));
        Check(RebootIndicator.PendingFileRenameOperations, () => Value(session, "PendingFileRenameOperations"));
        Check(RebootIndicator.PendingFileRenameOperations2, () => Value(session, "PendingFileRenameOperations2"));
        Check(RebootIndicator.DVDRebootSignal, () => Value(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "DVDRebootSignal"));
        Check(RebootIndicator.NetlogonJoin, () => Value(netlogon, "JoinDomain") || Value(netlogon, "AvoidSpnSet"));
        Check(RebootIndicator.WindowsUpdate, () => Value(update, "RebootRequired") || Value(update, "PostRebootReporting") || Key(update + @"\RebootRequired") || Key(update + @"\PostRebootReporting"));
        using (var cim = CimSession.Create(null))
        using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (var response = cim.InvokeMethod(@"root\ccm\clientsdk", "CCM_ClientUtilities", "DetermineIfRebootPending", null, options))
                {
                    var code = Convert.ToInt32(response.ReturnValue.Value, CultureInfo.InvariantCulture);
                    if (code != 0)
                        throw new Win32Exception(code);
                    var pending = Convert.ToBoolean(response.OutParameters["RebootPending"]?.Value, CultureInfo.InvariantCulture)
                        || Convert.ToBoolean(response.OutParameters["IsHardRebootPending"]?.Value, CultureInfo.InvariantCulture);
                    probes.Add(new RebootProbe(RebootIndicator.SCCMClient, pending ? RebootProbeStatus.Pending : RebootProbeStatus.Clear));
                }
            }
            catch (CimException error)
            {
                cancellationToken.ThrowIfCancellationRequested();
                probes.Add(new RebootProbe(RebootIndicator.SCCMClient, error.NativeErrorCode == NativeErrorCode.InvalidNamespace || error.NativeErrorCode == NativeErrorCode.InvalidClass
                    ? RebootProbeStatus.Unavailable : RebootProbeStatus.Failed, error));
            }
            catch (Win32Exception error) { probes.Add(new RebootProbe(RebootIndicator.SCCMClient, RebootProbeStatus.Failed, error)); }
            Check(RebootIndicator.ComputerRename, () =>
            {
                const string names = @"HKLM\SYSTEM\CurrentControlSet\Control\ComputerName\";
                var active = registry.GetValue(RegistryPath.Parse(names + "ActiveComputerName"), "ComputerName")?.GetString();
                var pending = registry.GetValue(RegistryPath.Parse(names + "ComputerName"), "ComputerName")?.GetString();
                return !string.IsNullOrWhiteSpace(active) && !string.IsNullOrWhiteSpace(pending) && !string.Equals(active, pending, StringComparison.OrdinalIgnoreCase);
            });
            try
            {
                var status = RebootProbeStatus.Unavailable;
                foreach (var computer in cim.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_ComputerSystem", options))
                    using (computer)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var property = computer.CimInstanceProperties["PendingSystemReboot"];
                        if (property != null)
                            status = Convert.ToBoolean(property.Value, CultureInfo.InvariantCulture) ? RebootProbeStatus.Pending : RebootProbeStatus.Clear;
                    }
                probes.Add(new RebootProbe(RebootIndicator.CIMPendingReboot, status));
            }
            catch (CimException error)
            { cancellationToken.ThrowIfCancellationRequested(); probes.Add(new RebootProbe(RebootIndicator.CIMPendingReboot, RebootProbeStatus.Failed, error)); }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new RebootStatus(probes);
    }
    /// <remarks>Offloads native registry/CIM reads. Cancellation is checked between calls and forwarded to the CIM provider.</remarks>
    public Task<RebootStatus> GetStatusAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetStatus(timeout, cancellationToken), cancellationToken);
}
