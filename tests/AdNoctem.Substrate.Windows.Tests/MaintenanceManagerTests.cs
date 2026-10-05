using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;
using Xunit;

namespace AdNoctem.Substrate.Windows.Tests;

public sealed class MaintenanceManagerTests
{
    [Fact]
    public async Task RestartManagerFindsARealLockAndReleasesItsSession()
    {
        var path = Path.Combine(Path.GetTempPath(), "AdNoctem.Substrate.PowerShell-lock-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var current = Process.GetCurrentProcess())
            {
                var locks = await new FileLockManager().GetLockingProcessesAsync(FileSystemPath.Parse(path));
                var own = Assert.Single(locks, item => item.ProcessId == current.Id);
                Assert.Equal(current.StartTime.ToUniversalTime(), own.ProcessStartedAtUtc);
                Assert.Equal(current.ProcessName, own.ProcessName);
            }
            Assert.Empty(new FileLockManager().GetLockingProcesses(FileSystemPath.Parse(path)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadOnlyRebootAndPrinterInventoryRetainDetachedEvidence()
    {
        var reboot = await new RebootManager().GetStatusAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(Enum.GetValues(typeof(RebootIndicator)).Length, reboot.Probes.Count);
        Assert.Equal(reboot.Probes.Any(probe => probe.Status == RebootProbeStatus.Pending), reboot.IsPending);
        Assert.All(reboot.Probes.Where(probe => probe.Status == RebootProbeStatus.Failed), probe => Assert.NotNull(probe.Error));
        var printers = await new DeviceManager().GetPrintersAsync(TimeSpan.FromSeconds(15), allowFallback: true);
        Assert.All(printers.Printers, printer => Assert.False(string.IsNullOrEmpty(printer.Name)));
    }

    [Fact]
    public async Task ServiceAndTaskInventoryResolveExactNamesAndCancelBeforeMutation()
    {
        var timeout = TimeSpan.FromSeconds(15);
        var services = new ServiceManager();
        var inventory = await services.GetServicesAsync(timeout);
        Assert.Contains(inventory, item => string.Equals(item.Name, "RpcSs", StringComparison.OrdinalIgnoreCase));
        Assert.Null(services.GetService("AdNoctem.Substrate.Nonexistent." + Guid.NewGuid().ToString("N"), timeout));
        var tasks = new ScheduledTaskManager();
        var scheduled = await tasks.GetTasksAsync(timeout);
        Assert.All(scheduled, task => Assert.StartsWith("\\", task.FolderPath));
        Assert.Contains(scheduled, task => task.Actions.Count != 0);
        Assert.All(scheduled.SelectMany(task => task.Actions), action => Assert.NotEmpty(action.Data.ClassName));
        var detached = await new CimManager().QueryAsync(@"root\cimv2", "SELECT Name FROM Win32_Service", timeout);
        Assert.Contains(detached, record => string.Equals(record.GetValue("Name") as string, "RpcSs", StringComparison.OrdinalIgnoreCase));
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => services.SetStartupType("NeverRun", ServiceStartupType.Disabled, timeout, cancellation.Token));
            Assert.Throws<OperationCanceledException>(() => services.Control("NeverRun", ServiceControlAction.Stop, timeout, cancellation.Token));
            Assert.Throws<OperationCanceledException>(() => tasks.SetEnabled("\\", "NeverRun", false, timeout, cancellation.Token));
        }
    }
}
