using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security;
using System.Threading;
using AdNoctem.Substrate.Packages;
using AdNoctem.Substrate.PowerShell.Windows;
using AdNoctem.Substrate.Registry;
using AdNoctem.Substrate.Registry.Compatibility;
using Microsoft.Win32;

namespace AdNoctem.Substrate.PowerShell.Packages;

internal static class ProgramOutput
{
    // Preserve v1's physical provider paths and labels at the adapter boundary. The public manager uses explicit native views.
    private static readonly Win32ProgramManager Manager = new Win32ProgramManager(
        new[]
        {
            new ProgramRegistryLocation(
                RegistryPath.Parse(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                RegistryView.Default,
                ProgramRegistrationScope.Machine
            ),
            new ProgramRegistryLocation(
                RegistryPath.Parse(
                    @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                ),
                RegistryView.Default,
                ProgramRegistrationScope.Machine
            ),
            new ProgramRegistryLocation(
                RegistryPath.Parse(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                RegistryView.Default,
                ProgramRegistrationScope.CurrentUser
            ),
        }
    );

    private static object? Value(Win32Program program, string name)
    {
        if (!program.Values.TryGetValue(name, out var value))
            return null;

        var data =
            value.Kind == RegistryValueKind.ExpandString ? value.GetString(true) : value.Data;

        if (data is int number && number < 0)
            return unchecked((uint)number);

        if (data is long large && large < 0)
            return unchecked((ulong)large);

        return data;
    }

    internal static bool Hidden(Win32Program program) =>
        LanguagePrimitives.IsTrue(Value(program, "SystemComponent"));

    internal static string Name(Win32Program program) => (string)Value(program, "DisplayName")!;

    private static string Path(Win32Program program) =>
        (program.Location.Path.Hive == RegistryHive.LocalMachine ? "HKLM:\\" : "HKCU:\\")
        + program.Location.Path.SubKey;

    internal static PSObject Shape(Win32Program program, string? exactPath = null) =>
        SystemOutput.Object(
            "Source",
            "Win32Program",
            "Name",
            Value(program, "DisplayName"),
            "DisplayName",
            Value(program, "DisplayName"),
            "DisplayVersion",
            Value(program, "DisplayVersion"),
            "Publisher",
            Value(program, "Publisher"),
            "InstallLocation",
            Value(program, "InstallLocation"),
            "InstallDate",
            Value(program, "InstallDate"),
            "UninstallString",
            Value(program, "UninstallString"),
            "QuietUninstallString",
            Value(program, "QuietUninstallString"),
            "ModifyPath",
            Value(program, "ModifyPath"),
            "EstimatedSize",
            Value(program, "EstimatedSize"),
            "RegistryPath",
            exactPath ?? Path(program),
            "RegistryScope",
            exactPath != null ? null : program.Location.Scope.ToString(),
            "RegistryView",
            exactPath != null ? null
                : program.Location.Scope == ProgramRegistrationScope.CurrentUser ? "Default"
                : program.Location.Path.SubKey.StartsWith(
                    @"SOFTWARE\WOW6432Node\",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? "32-bit"
                : "64-bit",
            "SystemComponent",
            Hidden(program)
        );

    internal static IEnumerable<PSObject> List(
        string? name,
        bool includeSystemComponents,
        CancellationToken cancellationToken
    )
    {
        var pattern = string.IsNullOrEmpty(name)
            ? null
            : new WildcardPattern(name, WildcardOptions.IgnoreCase);
        var programs = Manager
            .GetInventory(true, true, cancellationToken)
            .Programs.Where(program =>
                (includeSystemComponents || !Hidden(program))
                && (pattern == null || pattern.IsMatch(Name(program)))
            )
            .OrderBy(Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(Path, StringComparer.CurrentCultureIgnoreCase);
        var seen = new HashSet<string>(StringComparer.Create(CultureInfo.CurrentCulture, true));

        foreach (var program in programs)
            if (seen.Add(Name(program) + "\0" + Path(program)))
                yield return Shape(program);
    }

    internal static PSObject? Find(string path, CancellationToken cancellationToken)
    {
        try
        {
            var parsed = LegacyRegistryPath.Parse(path);
            var value = Manager.GetProgram(
                new ProgramRegistryLocation(
                    new RegistryPath(parsed.Hive, parsed.SubKey),
                    RegistryView.Default,
                    ProgramRegistrationScope.Other
                ),
                cancellationToken
            );

            return value == null ? null : Shape(value, path);
        }
        catch (Exception error)
            when (error is ArgumentException
                || error is IOException
                || error is InvalidDataException
                || error is UnauthorizedAccessException
                || error is SecurityException
                || error is NotSupportedException
            )
        {
            return null;
        } // v1 deliberately suppresses registry read errors for exact-path lookup.
    }
}

public abstract class ProgramInventoryCommand : PSCmdlet, IDisposable
{
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();
    private protected CancellationToken Cancellation => stopping.Token;

    protected override void StopProcessing()
    {
        try
        {
            stopping.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    protected override void EndProcessing() => Dispose();

    public void Dispose() => stopping.Dispose();
}

[Cmdlet(VerbsCommon.Get, "Win32Program"), OutputType(typeof(PSObject))]
public sealed class GetWin32ProgramCommand : ProgramInventoryCommand
{
    [Parameter(Position = 0)]
    public string Name { get; set; } = "";

    [Parameter]
    public SwitchParameter IncludeSystemComponent { get; set; }

    protected override void ProcessRecord()
    {
        foreach (var program in ProgramOutput.List(Name, IncludeSystemComponent, Cancellation))
            WriteObject(program);
    }
}

[Cmdlet(VerbsCommon.Find, "Win32Program"), OutputType(typeof(PSObject))]
public sealed class FindWin32ProgramCommand : ProgramInventoryCommand
{
    [Parameter(Mandatory = true, ParameterSetName = "Name")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = "RegistryPath")]
    public string RegistryPath { get; set; } = "";

    [Parameter]
    public SwitchParameter IncludeSystemComponent { get; set; }

    protected override void ProcessRecord()
    {
        if (ParameterSetName == "RegistryPath")
        {
            var program = ProgramOutput.Find(RegistryPath, Cancellation);

            if (program != null)
                WriteObject(program);
        }
        else
        {
            foreach (var program in ProgramOutput.List(Name, IncludeSystemComponent, Cancellation))
                WriteObject(program);
        }
    }
}

[Cmdlet(VerbsCommon.Get, "InstalledProgramCount"), OutputType(typeof(int))]
public sealed class GetInstalledProgramCountCommand : ProgramInventoryCommand
{
    protected override void ProcessRecord() =>
        WriteObject(ProgramOutput.List(null, false, Cancellation).Count());
}
