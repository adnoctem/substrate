using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Interop;

public sealed class OutlookInstallation
{
    public string Path { get; }
    public string? OutlookPath { get; }
    public string? ScanPstPath { get; }
    internal OutlookInstallation(string path, string? outlookPath, string? scanPstPath)
    { Path = path; OutlookPath = outlookPath; ScanPstPath = scanPstPath; }
}

public sealed class OutlookRepairToolInfo
{
    public string Name { get; }
    public string Path { get; }
    public string InstallationPath { get; }
    public Version? FileVersion { get; }
    // Capability inference only; never an execution trust decision.
    public bool SupportsFileArgument => string.Equals(Name, "SCANPST", StringComparison.OrdinalIgnoreCase)
        && FileVersion != null && FileVersion.Major == 16 && FileVersion >= new Version(16, 0, 10325, 20082);
    internal OutlookRepairToolInfo(string path, Version? version)
    { Path = path; Name = System.IO.Path.GetFileNameWithoutExtension(path); InstallationPath = System.IO.Path.GetDirectoryName(path)!; FileVersion = version; }
}

public sealed partial class OutlookManager
{
    public IReadOnlyList<OutlookInstallation> GetInstallations(CancellationToken cancellationToken = default)
    {
        var candidates = new List<string>();
        var views = Environment.Is64BitOperatingSystem ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 };
        foreach (var view in views)
            AddRegistryCandidate(new RegistryManager(view), RegistryPath.Parse(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\OUTLOOK.EXE"));
        AddRegistryCandidate(new RegistryManager(), RegistryPath.Parse(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\OUTLOOK.EXE"));
        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var version in new[] { "Office16", "Office15", "Office14", "Office12", "Office11" })
            {
                candidates.Add(Path.Combine(root!, "Microsoft Office", version));
                candidates.Add(Path.Combine(root!, "Microsoft Office", "root", version));
            }
        var result = new List<OutlookInstallation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(directory))
                continue;
            var resolved = FileSystemPath.Parse(directory).ExpandLongName().Value;
            if (!seen.Add(resolved))
                continue;
            var outlook = Path.Combine(resolved, "OUTLOOK.EXE");
            var repair = Path.Combine(resolved, "SCANPST.EXE");
            if (File.Exists(outlook) || File.Exists(repair))
                result.Add(new OutlookInstallation(resolved, File.Exists(outlook) ? outlook : null, File.Exists(repair) ? repair : null));
        }
        return result.AsReadOnly();

        void AddRegistryCandidate(RegistryManager manager, RegistryPath path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = manager.GetValue(path)?.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                var directory = manager.GetValue(path, "Path")?.GetString();
                if (!string.IsNullOrWhiteSpace(directory))
                    value = Path.Combine(directory, "OUTLOOK.EXE");
            }
            if (!string.IsNullOrWhiteSpace(value))
            {
                var directory = Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(value!));
                if (!string.IsNullOrWhiteSpace(directory))
                    candidates.Add(directory!);
            }
        }
    }

    public Task<IReadOnlyList<OutlookInstallation>> GetInstallationsAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => GetInstallations(cancellationToken), cancellationToken);

    public OutlookRepairToolInfo GetRepairToolInfo(FileSystemPath path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        var resolved = path.ExpandLongName().Value;
        if ((File.GetAttributes(resolved) & FileAttributes.Directory) != 0 || !string.Equals(Path.GetExtension(resolved), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The repair tool must be an existing .exe file.", nameof(path));
        var info = FileVersionInfo.GetVersionInfo(resolved);
        return new OutlookRepairToolInfo(resolved, info.FileMajorPart > 0 ? new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart) : null);
    }

    /// <remarks>Inspects installed ScanPST and explicit search directories. Does not execute candidates or change process PATH.</remarks>
    public IReadOnlyList<OutlookRepairToolInfo> FindRepairTools(IEnumerable<FileSystemPath>? searchDirectories = null, CancellationToken cancellationToken = default)
    {
        var paths = GetInstallations(cancellationToken).Where(item => item.ScanPstPath != null).Select(item => item.ScanPstPath!).ToList();
        foreach (var directory in searchDirectories ?? Array.Empty<FileSystemPath>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = (directory ?? throw new ArgumentException("Search directories cannot contain null.", nameof(searchDirectories))).Combine("SCANPST.EXE").Value;
            if (File.Exists(path))
                paths.Add(path);
        }
        var result = new List<OutlookRepairToolInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = GetRepairToolInfo(FileSystemPath.Parse(path));
            if (seen.Add(info.Path))
                result.Add(info);
        }
        return result.AsReadOnly();
    }
}

public static class MailHeaderParser
{
    public static string? GetTransportMessageId(string headerText)
    {
        if (headerText == null)
            throw new ArgumentNullException(nameof(headerText));
        if (string.IsNullOrWhiteSpace(headerText))
            return null;
        var match = Regex.Match(headerText, @"(?im)^Message-ID:\s*(<[^>]+>)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
