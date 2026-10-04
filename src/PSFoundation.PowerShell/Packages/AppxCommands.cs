using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using PSFoundation.IO;
using PSFoundation.Packages;
using PSFoundation.PowerShell.Windows;
using PSFoundation.Windows;

namespace PSFoundation.PowerShell.Packages;

internal static class AppxOutput
{
    internal static PSObject Object(AppxPackageInfo package) => SystemOutput.Object("Source", package.Source.ToString(), "Name", package.Name, "DisplayName", package.Name,
        "PackageFullName", package.Source == AppxPackageSource.Installed ? package.FullName : null, "PackageName", package.FullName, "PackageFamilyName", package.FamilyName,
        "Publisher", package.Publisher, "Version", Version.TryParse(package.Version, out var version) ? (object)version : package.Version, "Architecture", package.Architecture,
        "IsFramework", package.IsFramework, "IsResourcePackage", package.IsResource, "IsBundle", package.IsBundle, "NonRemovable", package.NonRemovable, "InstallLocation", package.InstallLocation, "User", null);
    internal static AppxPackageInfo Read(PSObject value)
    {
        string Text(string name) => LanguagePrimitives.ConvertTo<string>(value.Properties[name]?.Value) ?? "";
        bool Flag(string name) => LanguagePrimitives.IsTrue(value.Properties[name]?.Value);
        var source = Text("Source") == "Provisioned" ? AppxPackageSource.Provisioned : AppxPackageSource.Installed;
        return new AppxPackageInfo(source, Text("Name"), Text("PackageName"), Text("PackageFamilyName"), Text("Publisher"), Text("Version"), Text("Architecture"), Text("InstallLocation"), Flag("IsFramework"), Flag("IsResourcePackage"), Flag("IsBundle"), Flag("NonRemovable"));
    }
    internal static IReadOnlyList<AppxPackageInfo> Inventory(string pattern, bool installed, bool provisioned, bool allUsers, bool frameworks, bool resources, bool bundles, CancellationToken token)
    {
        var manager = new AppxPackageManager();
        var records = new List<AppxPackageInfo>();
        if (installed || !provisioned)
            records.AddRange(manager.GetInstalled(allUsers, frameworks, resources, bundles, token));
        if (provisioned && new IdentityManager().IsElevated())
            records.AddRange(manager.GetProvisioned(token));
        var match = new WildcardPattern(pattern, WildcardOptions.IgnoreCase);
        return records.Where(item => match.IsMatch(item.Name) || match.IsMatch(item.FullName) || (item.FamilyName != null && match.IsMatch(item.FamilyName))).ToArray();
    }
}
[Cmdlet(VerbsCommon.Get, "UPFAppxPackage"), OutputType(typeof(PSObject))]
public sealed class GetUPFAppxPackageCommand : SystemCommand
{
    [Parameter(Position = 0)] public string Name { get; set; } = "*";
    [Parameter] public SwitchParameter Installed { get; set; }
    [Parameter] public SwitchParameter Provisioned { get; set; }
    [Parameter] public SwitchParameter AllUsers { get; set; }
    [Parameter] public SwitchParameter IncludeFramework { get; set; }
    [Parameter] public SwitchParameter IncludeResource { get; set; }
    [Parameter] public SwitchParameter IncludeBundle { get; set; }
    protected override void ProcessRecord()
    { foreach (var package in AppxOutput.Inventory(Name, Installed, Provisioned, AllUsers, IncludeFramework, IncludeResource, IncludeBundle, Cancellation)) WriteObject(AppxOutput.Object(package)); }
}
[Cmdlet(VerbsCommon.Find, "UPFAppxPackage"), OutputType(typeof(PSObject))]
public sealed class FindUPFAppxPackageCommand : SystemCommand
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)] public string[] Pattern { get; set; } = Array.Empty<string>();
    [Parameter] public SwitchParameter Installed { get; set; }
    [Parameter] public SwitchParameter Provisioned { get; set; }
    [Parameter] public SwitchParameter AllUsers { get; set; }
    [Parameter] public SwitchParameter IncludeProtected { get; set; }
    [Parameter] public SwitchParameter IncludeFramework { get; set; }
    [Parameter] public SwitchParameter IncludeResource { get; set; }
    [Parameter] public SwitchParameter IncludeBundle { get; set; }
    protected override void ProcessRecord()
    {
        foreach (var pattern in Pattern)
        {
            var packages = AppxOutput.Inventory(pattern, Installed, Provisioned, AllUsers, IncludeFramework, IncludeResource, IncludeBundle, Cancellation);
            if (packages.Count == 0)
                WriteObject(SystemOutput.Object("Pattern", pattern, "Matched", false, "Protected", false, "ProtectedReason", null, "Package", null));
            foreach (var package in packages)
            {
                var safety = AppxPackageManager.AssessRemoval(package);
                WriteObject(SystemOutput.Object("Pattern", pattern, "Matched", true, "Protected", safety.Protected, "ProtectedReason", safety.Reason, "Package", AppxOutput.Object(package)));
            }
        }
    }
}
[Cmdlet(VerbsDiagnostic.Test, "UPFAppxPackageRemovalSafety"), OutputType(typeof(PSObject))]
public sealed class TestUPFAppxPackageRemovalSafetyCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)] public PSObject InputObject { get; set; } = null!;
    protected override void ProcessRecord()
    { var package = AppxOutput.Read(InputObject); var safety = AppxPackageManager.AssessRemoval(package); WriteObject(SystemOutput.Object("Target", package.FullName, "Protected", safety.Protected, "Reason", safety.Reason)); }
}
[Cmdlet(VerbsCommon.Get, "AppxPackageCount"), OutputType(typeof(int))]
public sealed class GetAppxPackageCountCommand : SystemCommand
{ protected override void ProcessRecord() => WriteObject(new AppxPackageManager().GetInstalled(cancellationToken: Cancellation).Count); }
[Cmdlet(VerbsCommon.Get, "PackageCount"), OutputType(typeof(PSObject))]
public sealed class GetPackageCountCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        var programs = new Win32ProgramManager().GetInventory(cancellationToken: Cancellation).Programs.Count;
        var appx = new AppxPackageManager().GetInstalled(cancellationToken: Cancellation).Count;
        WriteObject(SystemOutput.Object("Programs", programs, "Appx", appx, "Total", programs + appx));
    }
}
[Cmdlet(VerbsDiagnostic.Repair, "UPFAppxPackage", SupportsShouldProcess = true), OutputType(typeof(PSObject))]
public sealed class RepairUPFAppxPackageCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Name { get; set; } = "";
    [Parameter] public SwitchParameter AllUsers { get; set; }
    [Parameter] public SwitchParameter DryRun { get; set; }
    protected override void ProcessRecord()
    {
        var packages = AppxOutput.Inventory(Name, true, false, AllUsers, true, true, true, Cancellation).Where(item => !string.IsNullOrWhiteSpace(item.InstallLocation)).ToArray();
        if (packages.Length == 0)
        { WriteObject(PackageOutput.Result(Name, "UPFAppxPackage", "Repair", "Skipped", "NoMatch")); return; }
        foreach (var package in packages)
        {
            var manifest = Path.Combine(package.InstallLocation!, "AppxManifest.xml");
            if (!File.Exists(manifest))
            { WriteObject(PackageOutput.Result(package.FullName, "UPFAppxPackage", "Repair", "Skipped", "ManifestNotFound")); continue; }
            if (DryRun || !ShouldProcess(package.FullName, "Re-register UPF AppX/MSIX package"))
            { WriteObject(PackageOutput.Result(package.FullName, "UPFAppxPackage", "Repair", "Skipped", "WhatIf")); continue; }
            try
            { new AppxPackageManager().RegisterManifest(FileSystemPath.Parse(manifest), Cancellation); WriteObject(PackageOutput.Result(package.FullName, "UPFAppxPackage", "Repair", "Completed")); }
            catch (Exception error) when (!(error is OperationCanceledException)) { WriteObject(PackageOutput.Failure(package.FullName, "UPFAppxPackage", "Repair", error)); }
        }
    }
}
[Cmdlet(VerbsCommon.Reset, "UPFAppxPackage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High), OutputType(typeof(PSObject))]
public sealed class ResetUPFAppxPackageCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Name { get; set; } = "";
    [Parameter] public SwitchParameter DryRun { get; set; }
    protected override void ProcessRecord()
    {
        var packages = AppxOutput.Inventory(Name, true, false, false, false, false, false, Cancellation);
        if (packages.Count == 0)
        { WriteObject(PackageOutput.Result(Name, "UPFAppxPackage", "Reset", "Skipped", "NoMatch")); return; }
        foreach (var package in packages)
        {
            if (DryRun || !ShouldProcess(package.FullName, "Reset UPF AppX/MSIX package data"))
            { WriteObject(PackageOutput.Result(package.FullName, "UPFAppxPackage", "Reset", "Skipped", "WhatIf")); continue; }
            try
            { new AppxPackageManager().ResetData(package.FullName, Cancellation); WriteObject(PackageOutput.Result(package.FullName, "UPFAppxPackage", "Reset", "Completed")); }
            catch (Exception error) when (!(error is OperationCanceledException)) { WriteObject(PackageOutput.Failure(package.FullName, "UPFAppxPackage", "Reset", error)); }
        }
    }
}
[Cmdlet(VerbsLifecycle.Uninstall, "UPFAppxPackage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High), OutputType(typeof(PSObject))]
public sealed class UninstallUPFAppxPackageCommand : SystemCommand
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)] public PSObject InputObject { get; set; } = null!;
    [Parameter] public SwitchParameter AllUsers { get; set; }
    [Parameter] public SwitchParameter IncludeProtected { get; set; }
    [Parameter] public SwitchParameter Force { get; set; }
    [Parameter] public SwitchParameter DryRun { get; set; }
    protected override void ProcessRecord()
    {
        var package = AppxOutput.Read(InputObject);
        var safety = AppxPackageManager.AssessRemoval(package);
        var source = package.Source.ToString();
        if (safety.Protected && (!IncludeProtected || !Force))
        { WriteObject(PackageOutput.Result(package.FullName, source, "Uninstall", "Skipped", safety.Reason!)); return; }
        if (DryRun || !ShouldProcess(package.FullName, "Remove " + source.ToLowerInvariant() + " UPF AppX/MSIX package"))
        { WriteObject(PackageOutput.Result(package.FullName, source, "Uninstall", "Skipped", "WhatIf")); return; }
        try
        { new AppxPackageManager().Remove(package.FullName, package.Source, AllUsers, IncludeProtected && Force, Cancellation); WriteObject(PackageOutput.Result(package.FullName, source, "Uninstall", "Removed")); }
        catch (Exception error) when (!(error is OperationCanceledException)) { WriteObject(PackageOutput.Failure(package.FullName, source, "Uninstall", error)); }
    }
}
public static class AppxCompatibility
{
    public static PSObject Install(string path, string[]? dependencies, string? license, bool provisioned, bool skipLicense, bool force)
    {
        var action = provisioned ? "Provision" : "Install";
        try
        {
            var manager = new AppxPackageManager();
            var dependencyPaths = dependencies?.Select(value => FileSystemPath.Parse(value)).ToArray();
            if (provisioned)
                manager.Provision(FileSystemPath.Parse(path), dependencyPaths, string.IsNullOrEmpty(license) ? null : FileSystemPath.Parse(license!), skipLicense);
            else
                manager.Install(FileSystemPath.Parse(path), dependencyPaths, force);
            return PackageOutput.Result(path, "UPFAppxPackage", action, "Completed");
        }
        catch (Exception error) { return PackageOutput.Failure(path, "UPFAppxPackage", action, error); }
    }
}
