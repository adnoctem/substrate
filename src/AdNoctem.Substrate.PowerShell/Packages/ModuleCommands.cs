using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Packages;
using AdNoctem.Substrate.PowerShell.Windows;

namespace AdNoctem.Substrate.PowerShell.Packages;

/// <summary>Repository operations in the caller's current PowerShell runspace. Requires an installed, configured repository provider.</summary>
/// <remarks>Invoke on the runspace's pipeline thread. No provider bootstrap, trust-policy change, publisher-check bypass or nested host is performed.</remarks>
public sealed class PowerShellModuleRepository : IPowerShellModuleRepository
{
    public Task InstallAsync(PowerShellModuleInstallRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        using (var discovery = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
        {
            var modern = discovery.AddCommand("Microsoft.PowerShell.Core\\Get-Command").AddParameter("Name", "Microsoft.PowerShell.PSResourceGet\\Install-PSResource").AddParameter("ErrorAction", ActionPreference.SilentlyContinue).Invoke().Count > 0;
            using (var pipeline = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                pipeline.AddCommand(modern ? "Microsoft.PowerShell.PSResourceGet\\Install-PSResource" : "PowerShellGet\\Install-Module")
                    .AddParameter("Name", request.Name).AddParameter("Scope", request.Scope.ToString()).AddParameter("ErrorAction", ActionPreference.Stop);
                if (request.Version != null)
                    pipeline.AddParameter(modern ? "Version" : "RequiredVersion", request.Version);
                if (request.MinimumVersion != null)
                    pipeline.AddParameter(modern ? "Version" : "MinimumVersion", modern ? "[" + request.MinimumVersion + ",)" : request.MinimumVersion);
                if (request.Force)
                    pipeline.AddParameter(modern ? "Reinstall" : "Force", true);
                if (!modern)
                    pipeline.AddParameter("AllowClobber", true);
                pipeline.Invoke();
                if (pipeline.HadErrors)
                    throw pipeline.Streams.Error[0].Exception;
            }
        }
        return Task.CompletedTask;
    }
}

public abstract class ModuleCommand : SystemCommand
{
    protected string ScopePath(string scope, string? custom = null)
    {
        if (!string.IsNullOrEmpty(custom))
            return SessionState.Path.GetUnresolvedProviderPathFromPSPath(custom);
        var modern = string.Equals(SessionState.PSVariable.GetValue("PSEdition") as string, "Core", StringComparison.OrdinalIgnoreCase);
        var parent = scope == "AllUsers" ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return System.IO.Path.Combine(parent, modern ? "PowerShell" : "WindowsPowerShell", "Modules");
    }
    protected IReadOnlyList<InstalledPowerShellModule> Inventory(string root, string? expression)
    {
        if (!Directory.Exists(root))
            return Array.Empty<InstalledPowerShellModule>();
        var manifests = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            Cancellation.ThrowIfCancellationRequested();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                continue;
            var name = System.IO.Path.GetFileName(directory);
            void Add(string folder)
            { foreach (var extension in new[] { ".psd1", ".psm1", ".dll" }) { var candidate = System.IO.Path.Combine(folder, name + extension); if (File.Exists(candidate)) { manifests.Add(candidate); break; } } }
            Add(directory);
            foreach (var version in Directory.EnumerateDirectories(directory))
                if ((File.GetAttributes(version) & FileAttributes.ReparsePoint) == 0 && Version.TryParse(System.IO.Path.GetFileName(version), out _))
                    Add(version);
        }
        if (manifests.Count == 0)
            return Array.Empty<InstalledPowerShellModule>();
        using (var pipeline = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
        {
            var modules = pipeline.AddCommand("Microsoft.PowerShell.Core\\Get-Module").AddParameter("ListAvailable").AddParameter("Name", manifests.ToArray()).AddParameter("ErrorAction", ActionPreference.Stop).Invoke<PSModuleInfo>();
            if (pipeline.HadErrors)
                throw pipeline.Streams.Error[0].Exception;
            return new PowerShellModuleManager().Select(modules.Select(m => new InstalledPowerShellModule(m.Name, m.Version, m.ModuleBase, installedDate: Directory.GetCreationTime(m.ModuleBase))), expression);
        }
    }
}
[Cmdlet(VerbsCommon.Get, "PSModule")]
public sealed class GetPSModuleCommand : ModuleCommand
{
    [Parameter(Position = 0)] public string? Path { get; set; }
    [Parameter(Position = 1)] public string? Name { get; set; }
    [Parameter(Position = 2), ValidateSet("CurrentUser", "AllUsers")] public string Scope { get; set; } = "CurrentUser";
    protected override void ProcessRecord()
    {
        var values = Inventory(ScopePath(Scope), Name).Select(m => SystemOutput.Object("Name", m.Name, "Version", m.Version.ToString(), "Repository", m.Repository, "Scope", Scope, "InstalledDate", m.InstalledDate)).ToArray();
        if (string.IsNullOrEmpty(Path))
        { WriteObject(values, true); return; }
        var path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using (var pipeline = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
        {
            var json = pipeline.AddCommand("Microsoft.PowerShell.Utility\\ConvertTo-Json").AddParameter("InputObject", values).AddParameter("Depth", 3).Invoke<string>().Single();
            File.WriteAllText(path, json + Environment.NewLine, new System.Text.UTF8Encoding(!string.Equals(SessionState.PSVariable.GetValue("PSEdition") as string, "Core", StringComparison.OrdinalIgnoreCase)));
        }
        WriteVerbose("Exported " + values.Length + " module(s) to " + path);
    }
}
[Cmdlet(VerbsCommon.Remove, "PSModule", SupportsShouldProcess = true)]
public sealed class RemovePSModuleCommand : ModuleCommand
{
    [Parameter(Position = 0)] public string? Name { get; set; }
    [Parameter(Position = 1), ValidateRange(1, int.MaxValue)] public int LatestToKeep { get; set; } = 1;
    [Parameter] public SwitchParameter All { get; set; }
    [Parameter(Position = 2), ValidateSet("CurrentUser", "AllUsers")] public string Scope { get; set; } = "CurrentUser";
    [Parameter(Position = 3)] public string? Path { get; set; }
    [Parameter] public SwitchParameter Force { get; set; }
    protected override void ProcessRecord()
    {
        var root = ScopePath(Scope, Path);
        var manager = new PowerShellModuleManager();
        var remove = manager.PlanRemoval(Inventory(root, Name), LatestToKeep, All);
        var confirmed = false;
        foreach (var module in remove)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!ShouldProcess(module.Name + " v" + module.Version, "Remove"))
                continue;
            if (All && !Force && !confirmed)
            { if (!ShouldContinue("Remove all " + remove.Count + " selected module versions from " + root + "?", "Remove ALL module versions")) return; confirmed = true; }
            try
            { manager.Remove(module, root, Cancellation); WriteVerbose("Removed " + module.Name + " " + module.Version); }
            catch (Exception error) when (!(error is OperationCanceledException)) { WriteWarning("Failed to remove " + module.Name + ": " + error.Message); }
        }
    }
}
[Cmdlet(VerbsCommon.Add, "PSModule", SupportsShouldProcess = true)]
public sealed class AddPSModuleCommand : ModuleCommand
{
    [Parameter(Position = 0)] public string? Name { get; set; }
    [Parameter(Position = 1)] public string? Version { get; set; }
    [Parameter(Position = 2)] public string? MinimumVersion { get; set; }
    [Parameter(Position = 3), ValidateSet("CurrentUser", "AllUsers")] public string Scope { get; set; } = "CurrentUser";
    [Parameter] public SwitchParameter Force { get; set; }
    [Parameter(Position = 4)] public string? FromFile { get; set; }
    protected override void ProcessRecord()
    {
        if (!string.IsNullOrEmpty(Version) && !string.IsNullOrEmpty(MinimumVersion))
            throw new ArgumentException("Version and MinimumVersion are mutually exclusive.");
        var manager = new PowerShellModuleManager();
        var scope = (PowerShellModuleScope)Enum.Parse(typeof(PowerShellModuleScope), Scope, true);
        var requests = string.IsNullOrEmpty(FromFile) ? new[] { new PowerShellModuleInstallRequest(Name ?? "", Version, MinimumVersion, scope, Force) }
            : manager.ReadRestoreManifest(SessionState.Path.GetUnresolvedProviderPathFromPSPath(FromFile), scope, Force);
        foreach (var request in requests)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!ShouldProcess(request.Name + " " + (request.Version ?? request.MinimumVersion ?? "latest"), "Install"))
                continue;
            try
            { manager.InstallAsync(request, new PowerShellModuleRepository(), Cancellation).GetAwaiter().GetResult(); WriteVerbose("Installed " + request.Name); }
            catch (Exception error) when (!(error is OperationCanceledException)) { WriteWarning("Failed to install " + request.Name + ": " + error.Message); }
        }
    }
}
