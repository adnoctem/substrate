using System;
using System.Collections.Generic;
using AdNoctem.Substrate.Registry;
using Microsoft.Win32;

namespace AdNoctem.Substrate.Office.Tests;

internal static class OfficeInventoryTestData
{
    internal const string Root = @"SOFTWARE\Microsoft\Office\ClickToRun\";
    internal const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
    internal const string Machine = "11111111-2222-3333-4444-555555555555";
    internal const string Product = "Standard2019Volume";
    internal const string Build = "16.0.10417.20095";

    internal static OfficeRegistryRecord Record(
        string path,
        Dictionary<string, RegistryValue>? values = null,
        string[]? children = null
    ) =>
        new OfficeRegistryRecord(
            RegistryView.Registry64,
            RegistryPath.Parse("HKLM\\" + path),
            values ?? new Dictionary<string, RegistryValue>(),
            children
        );

    internal static OfficeRegistryRecord Msi(string code, string name) =>
        Record(
            Uninstall + code,
            new Dictionary<string, RegistryValue>
            {
                ["DisplayName"] = RegistryValue.String(name),
                ["DisplayVersion"] = RegistryValue.String("12.0.0.6785"),
                ["Publisher"] = RegistryValue.String("Microsoft Corporation"),
                ["WindowsInstaller"] = RegistryValue.String("1"),
            }
        );

    internal static List<OfficeRegistryRecord> Configured(RegistryValue? excluded)
    {
        var productPath = Root + @"ProductReleaseIDs\" + Machine + "\\" + Product + ".16";
        var configuration = new Dictionary<string, RegistryValue>
        {
            ["ProductReleaseIds"] = RegistryValue.String(Product),
            ["Platform"] = RegistryValue.String("x64"),
            ["CDNBaseUrl"] = RegistryValue.String(
                "https://officecdn.microsoft.com/pr/f2e724c1-748f-4b47-8fb8-8e0d210e9208"
            ),
            ["ClientCulture"] = RegistryValue.String("de-de"),
        };

        if (excluded != null)
            configuration[Product + ".ExcludedApps"] = excluded;

        return new List<OfficeRegistryRecord>
        {
            Record(Root + "Configuration", configuration),
            Record(
                Root + "ProductReleaseIDs",
                new Dictionary<string, RegistryValue>
                {
                    ["ActiveConfiguration"] = RegistryValue.String(Machine),
                },
                new[] { Machine }
            ),
            Record(productPath, children: new[] { "de-de", "x-none" }),
            Record(
                productPath + "\\de-de",
                new Dictionary<string, RegistryValue> { ["Version"] = RegistryValue.String(Build) }
            ),
            Record(
                productPath + "\\x-none",
                new Dictionary<string, RegistryValue> { ["Version"] = RegistryValue.String(Build) }
            ),
        };
    }
}
