using System;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Windows;

[Cmdlet(VerbsCommon.Get, "DotNetVersion"), OutputType(typeof(PSObject))]
public sealed class GetDotNetVersionCommand : SystemCommand
{
    protected override void ProcessRecord()
    {
        object[]? runtimes = null,
            sdks = null;
        var executable =
            InvokeCommand.GetCommand("dotnet", CommandTypes.Application) as ApplicationInfo;

        if (executable != null)
        {
            var tool = new DotNetTool();
            var path = FileSystemPath.Parse(executable.Path);
            var runtimeQuery = tool.GetComponents(
                path,
                DotNetComponentKind.Runtime,
                InventoryTimeout,
                Cancellation
            );
            Cancellation.ThrowIfCancellationRequested();

            if (!runtimeQuery.IsComplete)
                WriteVerbose("The dotnet runtime query returned incomplete evidence.");

            runtimes = runtimeQuery.Listing.Lines.Cast<object>().ToArray();
            var sdkQuery = tool.GetComponents(
                path,
                DotNetComponentKind.Sdk,
                InventoryTimeout,
                Cancellation
            );
            Cancellation.ThrowIfCancellationRequested();

            if (!sdkQuery.IsComplete)
                WriteVerbose("The dotnet SDK query returned incomplete evidence.");

            sdks = sdkQuery.Listing.Lines.Cast<object>().ToArray();
        }

        var framework = new DotNetManager().GetFrameworkRegistrations(true, Cancellation);

        foreach (var error in framework.Errors)
            WriteVerbose(error.Path + ": " + error.Error.Message);

        var releases = framework
            .Registrations.Where(entry =>
                Regex.IsMatch(entry.Path.Name, @"^(?!S)\p{L}", RegexOptions.IgnoreCase)
            )
            .Select(entry =>
            {
                object? Raw(string name) =>
                    entry.Values.TryGetValue(name, out var value) ? value.Data : null;

                return SystemOutput.Object(
                    "Name",
                    entry.Path.Name,
                    "Version",
                    Raw("Version") == null
                        ? null
                        : LanguagePrimitives.ConvertTo<string>(Raw("Version")),
                    "Release",
                    Raw("Release") == null ? 0 : LanguagePrimitives.ConvertTo<int>(Raw("Release")),
                    "Installed",
                    Raw("Install"),
                    "Product",
                    Product(entry)
                );
            })
            .Cast<object>()
            .ToArray();
        WriteObject(
            SystemOutput.Object(
                "DotnetRuntimes",
                runtimes,
                "DotnetSDKs",
                sdks,
                "FrameworkVersion",
                Environment.Version.ToString(),
                "Releases",
                releases
            )
        );
    }

    // Preserve the old display labels only at the PowerShell boundary. The typed library exposes accurate minimum versions.
    private static string Product(FrameworkRegistration entry)
    {
        var release = entry.Release ?? 0;
        var sp = entry.ServicePack ?? 0;

        if (release >= 533509)
            return "4.8.1 Windows 11 24H2 or Windows Server 2025";

        if (release >= 533320)
            return "4.8.1";

        if (release >= 528449)
            return "4.8 Windows 11 or Windows Server 2022";

        if (release >= 528372)
            return "4.8 Windows 10 May 2020, Oct 2020, May 2021";

        if (release >= 528040)
            return "4.8 Windows 10 May 2019, Nov 2019";

        if (release >= 461808)
            return "4.7.2";

        if (release >= 461308)
            return "4.7.1";

        if (release >= 460798)
            return "4.7 Original Release";

        if (release == 394802)
            return "4.6.2 Windows 10 Anniversary Update";

        if (release == 394806)
            return "4.6.2";

        if (release == 394254)
            return "4.6.1 Windows 10 November Update";

        if (release >= 394271)
            return "4.6.1";

        if (release >= 393295)
            return "4.6 Windows 10";

        if (release >= 379897)
            return "4.6 Original Release";

        if (release >= 379893)
            return "4.5.2";

        if (release >= 378675)
            return "4.5.1 Windows 8.1 or Windows Server 2012";

        if (release >= 378389)
            return "4.5 Original Release";

        switch (entry.Path.Name.ToLowerInvariant())
        {
            case "v3.5":
                return sp == 1 ? "3.5 ServicePack 1" : "3.5 Original Release";
            case "v3.0":
                return sp == 2 ? "3.5 ServicePack 2"
                    : sp == 1 ? "3.5 ServicePack 1"
                    : "3.5";
            case "v2.0*":
                return sp == 2 ? "2.0 ServicePack 2"
                    : sp == 1 ? "2.0 ServicePack 1"
                    : "2.0";
            default:
                return "";
        }
    }
}
