using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AdNoctem.Substrate.Diagnostics;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Packages;

/// <summary>Explicit online AppX provisioning through the Windows system DISM tool. Never reboots or changes installed user registrations.</summary>
public sealed class DismTool
{
    /// <summary>Queries provisioned AppX packages using the system DISM executable.</summary>
    public IReadOnlyList<AppxPackageInfo> GetProvisionedPackages(CancellationToken cancellationToken = default)
        => ParseProvisionedPackages(Run(new[] { "/Get-ProvisionedAppxPackages" }, cancellationToken).StandardOutput);
    /// <summary>Runs DISM provisioning with explicit dependencies and license handling.</summary>
    public void ProvisionPackage(FileSystemPath package, IEnumerable<FileSystemPath>? dependencies = null, FileSystemPath? license = null, bool skipLicense = false, CancellationToken cancellationToken = default)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        var arguments = new List<string> { "/Add-ProvisionedAppxPackage", "/PackagePath:" + package.Value };
        arguments.AddRange((dependencies ?? Array.Empty<FileSystemPath>()).Select(path => "/DependencyPackagePath:" + path.Value));
        if (license != null)
            arguments.Add("/LicensePath:" + license.Value);
        if (skipLicense)
            arguments.Add("/SkipLicense");
        Run(arguments, cancellationToken);
    }
    /// <summary>Removes provisioning for an exact package full name; this does not mean every existing user registration was removed.</summary>
    public void RemoveProvisionedPackage(string fullName, CancellationToken cancellationToken = default)
    { AppxPackageInfo.RequireIdentity(fullName); Run(new[] { "/Remove-ProvisionedAppxPackage", "/PackageName:" + fullName }, cancellationToken); }
    /// <summary>Parses DISM output requested with /English. Unknown heading/progress lines are ignored; incomplete package records fail.</summary>
    public static IReadOnlyList<AppxPackageInfo> ParseProvisionedPackages(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        var result = new List<AppxPackageInfo>();
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Finish()
        {
            if (fields.Count == 0)
                return;
            if (!fields.TryGetValue("DisplayName", out var name) || !fields.TryGetValue("PackageName", out var fullName))
                throw new InvalidDataException("Incomplete DISM package inventory.");
            result.Add(new AppxPackageInfo(AppxPackageSource.Provisioned, name, fullName, version: fields.TryGetValue("Version", out var version) ? version : "", architecture: fields.TryGetValue("Architecture", out var architecture) ? architecture : ""));
            fields.Clear();
        }
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;
            var key = line.Substring(0, colon).Trim();
            var value = line.Substring(colon + 1).Trim();
            if (key == "DisplayName")
            { Finish(); fields[key] = value; }
            else if (fields.Count != 0 && (key == "PackageName" || key == "Version" || key == "Architecture"))
                fields[key] = value;
        }
        Finish();
        if (result.Count == 0 && text.IndexOf("The operation completed successfully.", StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidDataException("DISM did not return a recognizable package inventory.");
        return result.AsReadOnly();
    }
    private static ProcessResult Run(IEnumerable<string> arguments, CancellationToken token)
    {
        var executable = Path.Combine(Environment.SystemDirectory, "dism.exe");
        var result = new ProcessManager().Run(new ProcessRequest(executable, new[] { "/Online", "/English", "/NoRestart" }.Concat(arguments), stopBehavior: ProcessStopBehavior.WaitForExit, maximumCapturedCharacters: 16 * 1024 * 1024), token);
        if (result.OutputTruncated)
            throw new InvalidDataException("DISM output was truncated.");
        if (result.ExitCode != 0 && result.ExitCode != 3010)
            throw new System.ComponentModel.Win32Exception(result.ExitCode, "DISM servicing failed: " + result.StandardOutput.Trim());
        return result;
    }
}
