using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.Office;

/// <summary>Windows version and architecture evidence used to assess deployment prerequisites.</summary>
public sealed class OfficeHostPlatform
{
    public int ProductType { get; }
    public int BuildNumber { get; }
    public int ProcessorArchitecture { get; }
    public bool Supported => ProductType == 1 && BuildNumber >= 19045 && ProcessorArchitecture == 9;
    public OfficeHostPlatform(int productType, int buildNumber, int processorArchitecture)
    { ProductType = productType; BuildNumber = buildNumber; ProcessorArchitecture = processorArchitecture; }
}
/// <summary>Observed deployment activity that may block concurrent Office servicing.</summary>
public sealed class OfficeDeploymentActivity
{
    public IReadOnlyList<CimRecord> Installers { get; }
    public bool Busy => Installers.Count != 0;
    internal OfficeDeploymentActivity(IEnumerable<CimRecord> installers) => Installers = Array.AsReadOnly(installers.ToArray());
}
/// <summary>Explicit host and installer readiness checks. Never prompts, elevates, reboots or terminates an installer.</summary>
public sealed class OfficeDeploymentEnvironment
{
    public OfficeHostPlatform ReadPlatform(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var cim = new CimManager();
        var os = cim.Query("root/cimv2", "SELECT ProductType, BuildNumber FROM Win32_OperatingSystem", timeout, cancellationToken).Single();
        var cpu = cim.Query("root/cimv2", "SELECT Architecture FROM Win32_Processor", timeout, cancellationToken).First();
        return new OfficeHostPlatform(Convert.ToInt32(os.GetValue("ProductType"), CultureInfo.InvariantCulture),
            Convert.ToInt32(os.GetValue("BuildNumber"), CultureInfo.InvariantCulture), Convert.ToInt32(cpu.GetValue("Architecture"), CultureInfo.InvariantCulture));
    }
    public OfficeDeploymentActivity ReadActivity(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var processes = new CimManager().Query("root/cimv2", "SELECT Name, ProcessId, CreationDate FROM Win32_Process", timeout, cancellationToken);
        return new OfficeDeploymentActivity(processes.Where(process => OfficeVersionResolver.IsMatch(process.GetValue("Name") as string, @"^(setup|msiexec|OfficeC2RClient|integratedoffice)\.exe$")));
    }
    public void RequireReady(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!new IdentityManager().IsElevated())
            throw new OfficeException(OfficeFailureReason.ElevationRequired, "Office deployment requires an elevated Windows identity.");
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            throw new OfficeException(OfficeFailureReason.Unsupported, "Use a 64-bit process on 64-bit Windows.");
        if (!ReadPlatform(timeout, cancellationToken).Supported)
            throw new OfficeException(OfficeFailureReason.Unsupported, "This backend requires x64 Windows 10 22H2 (build 19045) or later desktop hosts.");
        var reboot = new RebootManager().GetStatus(timeout, cancellationToken);
        if (reboot.IsPending)
            throw new OfficeException(OfficeFailureReason.RebootRequired, "A pending reboot blocks Office mutation.");
        if (!reboot.IsComplete)
            throw new OfficeException(OfficeFailureReason.PreflightFailed, "Reboot readiness could not be established.");
        if (ReadActivity(timeout, cancellationToken).Busy)
            throw new OfficeException(OfficeFailureReason.DeploymentBusy, "Native deployment activity is active or uncertain.");
    }
    public Task<OfficeHostPlatform> ReadPlatformAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => ReadPlatform(timeout, cancellationToken), cancellationToken);
    public Task<OfficeDeploymentActivity> ReadActivityAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => ReadActivity(timeout, cancellationToken), cancellationToken);
    public Task RequireReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => RequireReady(timeout, cancellationToken), cancellationToken);
}

/// <summary>Owns the shared machine-wide deployment mutex. Acquisition, release and disposal must occur on the same thread.</summary>
/// <remarks>Do not hold this lease across await. Async workflows should run their synchronous orchestration on a dedicated worker.
/// An abandoned mutex permits rediscovery; it never proves the previous deployment completed.</remarks>
public sealed class OfficeDeploymentLock : IDisposable
{
    private readonly Mutex mutex;
    private readonly int ownerThread;
    private bool acquired = true;
    private bool disposed;
    public bool WasAbandoned { get; }
    private OfficeDeploymentLock(Mutex mutex, bool abandoned) { this.mutex = mutex; WasAbandoned = abandoned; ownerThread = Thread.CurrentThread.ManagedThreadId; }
    public static OfficeDeploymentLock Acquire(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Office deployment locking requires Windows.");
        cancellationToken.ThrowIfCancellationRequested();
        var mutex = new Mutex(false, @"Global\PSFoundation.OfficeDeployment");
        try
        {
            var abandoned = false;
            bool acquired;
            try
            { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; abandoned = true; }
            if (!acquired)
                throw new OfficeException(OfficeFailureReason.DeploymentBusy, "Another AdNoctem.Substrate.PowerShell Office operation holds the deployment lock.");
            return new OfficeDeploymentLock(mutex, abandoned);
        }
        catch { mutex.Dispose(); throw; }
    }
    public void ReleaseMutex()
    {
        if (!acquired)
            return;
        if (Thread.CurrentThread.ManagedThreadId != ownerThread)
            throw new InvalidOperationException("Release the deployment lock on its acquiring thread.");
        mutex.ReleaseMutex();
        acquired = false;
    }
    public void Dispose()
    {
        if (disposed)
            return;
        ReleaseMutex();
        mutex.Dispose();
        disposed = true;
    }
}
