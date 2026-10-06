using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdNoctem.Substrate.Windows;

/// <summary>Physical-memory byte counts and load percentage observed at one point in time.</summary>
public sealed class MemorySnapshot
{
    public ulong TotalBytes { get; }
    public ulong AvailableBytes { get; }
    public ulong UsedBytes => TotalBytes - AvailableBytes;
    public uint LoadPercent { get; }

    public MemorySnapshot(ulong totalBytes, ulong availableBytes, uint loadPercent)
    {
        if (availableBytes > totalBytes)
            throw new ArgumentOutOfRangeException(nameof(availableBytes));

        if (loadPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(loadPercent));

        TotalBytes = totalBytes;
        AvailableBytes = availableBytes;
        LoadPercent = loadPercent;
    }
}

/// <summary>Logical-volume capacity and free-space byte counts obtained from disk inventory.</summary>
public sealed class DiskSnapshot
{
    public string Name { get; }
    public string? Label { get; }
    public DriveType DriveType { get; }
    public string? FileSystem { get; }
    public ulong TotalBytes { get; }
    public ulong FreeBytes { get; }
    public ulong UsedBytes => TotalBytes - FreeBytes;
    public double PercentFree => TotalBytes == 0 ? 0 : FreeBytes * 100.0 / TotalBytes;

    public DiskSnapshot(
        string name,
        string? label,
        DriveType driveType,
        string? fileSystem,
        ulong totalBytes,
        ulong freeBytes
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A volume name is required.", nameof(name));

        if (freeBytes > totalBytes)
            throw new ArgumentOutOfRangeException(nameof(freeBytes));

        Name = name;
        Label = label;
        DriveType = driveType;
        FileSystem = fileSystem;
        TotalBytes = totalBytes;
        FreeBytes = freeBytes;
    }
}

/// <summary>Fallback is explicit and observable. A successful empty inventory is not a provider failure.</summary>
public sealed class DiskInventory
{
    public IReadOnlyList<DiskSnapshot> Disks { get; }
    public Exception? AssociationError { get; }

    internal DiskInventory(IEnumerable<DiskSnapshot> disks, Exception? associationError)
    {
        Disks = Array.AsReadOnly(disks.ToArray());
        AssociationError = associationError;
    }
}

/// <summary>Local hostname and optional DNS resolution evidence, retaining lookup failure details.</summary>
public sealed class HostnameInfo
{
    public string Hostname { get; }
    public string? FullyQualifiedDomainName { get; }
    public Exception? ResolutionError { get; }

    internal HostnameInfo(string hostname, string? fqdn, Exception? error)
    {
        Hostname = hostname;
        FullyQualifiedDomainName = fqdn;
        ResolutionError = error;
    }
}

/// <summary>A collection of machine observations taken sequentially, without atomic snapshot guarantees.</summary>
public sealed class SystemSnapshot
{
    public WindowsVersionInfo OperatingSystem { get; }
    public MemorySnapshot Memory { get; }
    public DiskInventory DiskInventory { get; }
    public HostnameInfo Hostname { get; }
    public TimeSpan Uptime { get; }

    internal SystemSnapshot(
        WindowsVersionInfo operatingSystem,
        MemorySnapshot memory,
        DiskInventory disks,
        HostnameInfo hostname,
        TimeSpan uptime
    )
    {
        OperatingSystem = operatingSystem;
        Memory = memory;
        DiskInventory = disks;
        Hostname = hostname;
        Uptime = uptime;
    }
}
