using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Windows;

internal static class SystemOutput
{
    internal static PSObject Object(params object?[] pairs)
    {
        var value = new PSObject();
        for (var index = 0; index < pairs.Length; index += 2)
            value.Properties.Add(new PSNoteProperty((string)pairs[index]!, pairs[index + 1]));
        return value;
    }
    internal static string Number(double value, int digits = 2) => Math.Round(value, digits).ToString(digits == 2 ? "0.##" : "0.#", CultureInfo.InvariantCulture);
    internal static string GiB(ulong bytes) => Number(bytes / 1073741824.0);
    internal static string UptimeDisplay(TimeSpan time) => string.Format(CultureInfo.InvariantCulture, "{0}d {1:D2}h {2:D2}m {3:D2}s", time.Days, time.Hours, time.Minutes, time.Seconds);
    // v1 parsed the epoch through PowerShell's local DateTime conversion before marking it UTC.
    // Keep that historical offset at the script boundary; the reusable model stores the actual UTC instant.
    internal static DateTime? LegacyInstallDate(WindowsVersionInfo os) => os.InstalledAt.HasValue
        ? DateTime.SpecifyKind(DateTime.Parse("1970-01-01T00:00:00Z", CultureInfo.InvariantCulture), DateTimeKind.Utc)
            .AddSeconds(os.InstalledAt.Value.ToUnixTimeSeconds()).ToLocalTime() : (DateTime?)null;
    internal static PSObject Version(WindowsVersionInfo os) => Object("ProductName", os.ProductName ?? "", "EditionID", os.EditionId,
        "InstallationType", os.InstallationType, "DisplayVersion", os.DisplayVersion, "CurrentBuild", os.BuildNumber, "UBR", os.UpdateBuildRevision,
        "ReleaseId", os.ReleaseId, "BuildBranch", os.BuildBranch, "InstallDate", LegacyInstallDate(os), "RegisteredOwner", os.RegisteredOwner);
    internal static PSObject Memory(MemorySnapshot memory) => Object("TotalBytes", memory.TotalBytes, "AvailableBytes", memory.AvailableBytes,
        "UsedBytes", memory.UsedBytes, "LoadPercent", memory.LoadPercent, "TotalGiB", GiB(memory.TotalBytes), "AvailableGiB", GiB(memory.AvailableBytes), "UsedGiB", GiB(memory.UsedBytes));
    internal static PSObject Disk(DiskSnapshot disk) => Object("Name", disk.Name, "Label", disk.Label, "Type", disk.DriveType == DriveType.Ram ? "Ram"
        : disk.DriveType == DriveType.CDRom ? "CDRom" : disk.DriveType == DriveType.NoRootDirectory ? "Unknown" : disk.DriveType.ToString(),
        "FileSystem", disk.FileSystem, "TotalGiB", GiB(disk.TotalBytes), "FreeGiB", GiB(disk.FreeBytes), "UsedGiB", GiB(disk.UsedBytes),
        "PercentFree", Number(disk.PercentFree, 1), "TotalBytes", disk.TotalBytes, "FreeBytes", disk.FreeBytes);
}

public abstract class SystemCommand : PSCmdlet, IDisposable
{
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();
    private protected readonly SystemManager Manager = new SystemManager();
    private protected CancellationToken Cancellation => stopping.Token;
    private protected static readonly TimeSpan InventoryTimeout = TimeSpan.FromSeconds(60);
    protected override void StopProcessing() { try { stopping.Cancel(); } catch (ObjectDisposedException) { } }
    protected override void EndProcessing() => Dispose();
    public void Dispose() => stopping.Dispose();
    private protected void DiskWarning(DiskInventory inventory)
    { if (inventory.AssociationError != null) WriteVerbose("Physical disk association inventory failed: " + inventory.AssociationError.Message); }
    private protected void HostWarning(HostnameInfo host)
    { if (host.ResolutionError != null) WriteVerbose("Unable to resolve FQDN for hostname: " + host.Hostname); }
}

[Cmdlet(VerbsCommon.Get, "OSBuildNumber"), OutputType(typeof(int))]
public sealed class GetOSBuildNumberCommand : SystemCommand { protected override void ProcessRecord() => WriteObject(Manager.GetBuildNumber()); }
[Cmdlet(VerbsCommon.Get, "OSDisplayVersion"), OutputType(typeof(string))]
public sealed class GetOSDisplayVersionCommand : SystemCommand { protected override void ProcessRecord() => WriteObject(Manager.GetDisplayVersion()); }
[Cmdlet(VerbsCommon.Get, "OSEdition"), OutputType(typeof(string))]
public sealed class GetOSEditionCommand : SystemCommand { protected override void ProcessRecord() => WriteObject(Manager.GetEdition()); }
[Cmdlet(VerbsCommon.Get, "OSProductName"), OutputType(typeof(string))]
public sealed class GetOSProductNameCommand : SystemCommand { protected override void ProcessRecord() => WriteObject(Manager.GetOperatingSystem().ProductName ?? ""); }
[Cmdlet(VerbsCommon.Get, "OSVersionInfo"), OutputType(typeof(PSObject))]
public sealed class GetOSVersionInfoCommand : SystemCommand
{
    protected override void ProcessRecord() => WriteObject(SystemOutput.Version(Manager.GetOperatingSystem()));
}
[Cmdlet(VerbsCommon.Get, "SystemMemory"), OutputType(typeof(PSObject))]
public sealed class GetSystemMemoryCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        try
        { WriteObject(SystemOutput.Memory(Manager.GetMemory())); }
        catch (System.ComponentModel.Win32Exception) { Infrastructure.LegacyError.Write(this, "GlobalMemoryStatusEx failed."); WriteObject(null); }
    }
}
[Cmdlet(VerbsCommon.Get, "SystemUptime"), OutputType(typeof(PSObject))]
public sealed class GetSystemUptimeCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var span = Manager.GetUptime();
        WriteObject(SystemOutput.Object("TotalMilliseconds", (ulong)span.TotalMilliseconds, "Days", span.Days, "Hours", span.Hours,
            "Minutes", span.Minutes, "Seconds", span.Seconds, "TotalHours", SystemOutput.Number(span.TotalHours, 1),
            "TotalDays", SystemOutput.Number(span.TotalDays, 1), "Display", SystemOutput.UptimeDisplay(span)));
    }
}
[Cmdlet(VerbsCommon.Get, "Hostname"), OutputType(typeof(PSObject))]
public sealed class GetHostnameCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var host = Manager.GetHostname(InventoryTimeout, Cancellation);
        HostWarning(host);
        WriteObject(SystemOutput.Object("Hostname", host.Hostname, "FQDN", host.FullyQualifiedDomainName));
    }
}
[Cmdlet(VerbsCommon.Get, "SystemDisk"), OutputType(typeof(PSObject[]))]
public sealed class GetSystemDiskCommand : SystemCommand
{
    [Parameter] public SwitchParameter All { get; set; }
    protected override void ProcessRecord()
    {
        var result = Manager.GetDisks(InventoryTimeout, All, true, Cancellation);
        DiskWarning(result);
        foreach (var disk in result.Disks)
            WriteObject(SystemOutput.Disk(disk));
    }
}
[Cmdlet(VerbsCommon.Get, "SystemInfo"), OutputType(typeof(PSObject))]
public sealed class GetSystemInfoCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var value = Manager.GetSnapshotAsync(InventoryTimeout, true, Cancellation).GetAwaiter().GetResult();
        DiskWarning(value.DiskInventory);
        HostWarning(value.Hostname);
        var os = value.OperatingSystem;
        WriteObject(SystemOutput.Object("OSProductName", os.ProductName ?? "", "OSEdition", os.EditionId, "OSVersion", os.DisplayVersion,
            "OSBuild", os.BuildNumber, "OSUBRev", os.UpdateBuildRevision, "Hostname", value.Hostname.Hostname, "FQDN", value.Hostname.FullyQualifiedDomainName,
            "TotalMemoryGiB", SystemOutput.GiB(value.Memory.TotalBytes), "MemoryLoadPct", value.Memory.LoadPercent,
            "Disks", string.Join(" | ", value.DiskInventory.Disks.Select(disk => disk.Name + " " + SystemOutput.GiB(disk.FreeBytes) + "GiB/" + SystemOutput.GiB(disk.TotalBytes) + "GiB (" + SystemOutput.Number(disk.PercentFree, 1) + "% free)")),
            "Uptime", SystemOutput.UptimeDisplay(value.Uptime), "InstallDate", SystemOutput.LegacyInstallDate(os)));
    }
}
[Cmdlet(VerbsCommon.Get, "SystemPaths"), OutputType(typeof(PSObject))]
public sealed class GetSystemPathsCommand : PSCmdlet
{
    [Parameter(Position = 0), ValidatePattern("^[A-Za-z0-9._-]+$")] public string Name { get; set; } = "PSFoundation";
    protected override void ProcessRecord()
    {
        var paths = ApplicationPaths.FromEnvironment(Name);
        WriteObject(SystemOutput.Object("Home", paths.Home.Value, "Config", paths.Config.Value, "Cache", paths.Cache.Value, "Data", paths.Data.Value, "Logs", paths.Logs.Value));
    }
}
[Cmdlet(VerbsDiagnostic.Test, "HostApplicability"), OutputType(typeof(bool))]
public sealed class TestHostApplicabilityCommand : SystemCommand
{
    [Parameter(Position = 0)] public int MinBuild { get; set; }
    [Parameter(Position = 1)] public int MaxBuild { get; set; }
    [Parameter(Position = 2)] public string[]? Edition { get; set; }
    [Parameter(Position = 3), ValidateSet("x64", "x86")] public string Bitness { get; set; } = "";
    protected override void ProcessRecord()
    {
        var build = Manager.GetBuildNumber();
        int? minimum = MyInvocation.BoundParameters.ContainsKey(nameof(MinBuild)) ? MinBuild : (int?)null;
        int? maximum = MyInvocation.BoundParameters.ContainsKey(nameof(MaxBuild)) ? MaxBuild : (int?)null;
        if (!HostValidator.IsApplicable(build, null, Environment.Is64BitOperatingSystem, minimum, maximum))
        { WriteObject(false); return; }
        WriteObject(HostValidator.IsApplicable(build, Edition?.Length > 0 ? Manager.GetEdition() : null, Environment.Is64BitOperatingSystem,
            minimum, maximum, Edition, MyInvocation.BoundParameters.ContainsKey(nameof(Bitness)) ? string.Equals(Bitness, "x64", StringComparison.OrdinalIgnoreCase) : (bool?)null));
    }
}
[Cmdlet(VerbsDiagnostic.Test, "Elevation"), OutputType(typeof(bool))]
public sealed class TestElevationCommand : PSCmdlet { protected override void ProcessRecord() => WriteObject(new IdentityManager().IsElevated()); }
[Cmdlet(VerbsData.Convert, "RobocopyExitCode"), OutputType(typeof(string))]
public sealed class ConvertRobocopyExitCodeCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)] public int ExitCode { get; set; }
    protected override void ProcessRecord() => WriteObject(new AdNoctem.Substrate.Diagnostics.RobocopyExitResult(ExitCode).Description);
}

public static class IdentityCompatibility
{
    public static Hashtable GetUserInfo()
    {
        var value = new IdentityManager().GetCurrentIdentity();
        return new Hashtable(StringComparer.OrdinalIgnoreCase) { ["UserName"] = value.Name, ["SID"] = value.SecurityIdentifier, ["IsAdministrator"] = value.IsAdministrator };
    }
    public static string GetSecurityIdentifier(string name) => new IdentityManager().GetSecurityIdentifier(name);
}
