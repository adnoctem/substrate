using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Windows;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace AdNoctem.Substrate.PowerShell.Windows;

[Cmdlet(VerbsCommon.Get, "HostPrerequisiteReport"), OutputType(typeof(PSObject))]
public sealed class GetHostPrerequisiteReportCommand : SystemCommand
{
    [Parameter(Position = 0)]
    public int MinBuild { get; set; }

    [Parameter(Position = 1)]
    public int MaxBuild { get; set; }

    [Parameter(Position = 2)]
    public string[]? Edition { get; set; }

    [Parameter(Position = 3), ValidateSet("x86", "x64", "Arm64")]
    public string? Architecture { get; set; }

    [Parameter]
    public SwitchParameter RequireAdministrator { get; set; }

    [Parameter(Position = 4)]
    public Hashtable RequiredModules { get; set; } = new Hashtable();

    [Parameter(Position = 5)]
    public string[] RequiredCommands { get; set; } = Array.Empty<string>();

    [Parameter(Position = 6)]
    public Hashtable RequiredServices { get; set; } = new Hashtable();

    protected override void ProcessRecord()
    {
        var minimum = MyInvocation.BoundParameters.ContainsKey("MinBuild");
        var maximum = MyInvocation.BoundParameters.ContainsKey("MaxBuild");

        if (minimum && maximum && MinBuild > MaxBuild)
            throw new ArgumentException("MinBuild must not exceed MaxBuild.");

        var requirements = new List<PrerequisiteRequirement>();

        if (minimum)
            requirements.Add(
                new PrerequisiteRequirement(
                    "MinBuild",
                    "Windows",
                    MinBuild,
                    () => Manager.GetBuildNumber(),
                    a => (int)a! >= MinBuild,
                    "Use a Windows build at or above the required minimum."
                )
            );

        if (maximum)
            requirements.Add(
                new PrerequisiteRequirement(
                    "MaxBuild",
                    "Windows",
                    MaxBuild,
                    () => Manager.GetBuildNumber(),
                    a => (int)a! <= MaxBuild,
                    "Use a supported Windows build within the specified range."
                )
            );

        if (Edition?.Length > 0)
            requirements.Add(
                new PrerequisiteRequirement(
                    "Edition",
                    "Windows",
                    Edition,
                    () => Manager.GetEdition(),
                    a => Edition.Contains((string)a!, StringComparer.OrdinalIgnoreCase),
                    "Use one of the supported Windows editions."
                )
            );

        if (!string.IsNullOrEmpty(Architecture))
            requirements.Add(
                new PrerequisiteRequirement(
                    "Architecture",
                    "Windows",
                    Architecture,
                    ReadArchitecture,
                    a =>
                        string.Equals((string)a!, Architecture, StringComparison.OrdinalIgnoreCase),
                    "Use a host with the required OS architecture."
                )
            );

        if (RequireAdministrator)
            requirements.Add(
                new PrerequisiteRequirement(
                    "Elevation",
                    "CurrentProcess",
                    true,
                    () => new IdentityManager().IsElevated(),
                    a => (bool)a!,
                    "Run the operation from an elevated PowerShell session."
                )
            );

        foreach (
            var key in RequiredModules
                .Keys.Cast<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        )
        {
            Exact(key);
            var version = System.Version.Parse(
                LanguagePrimitives.ConvertTo<string>(RequiredModules[key])
            );
            requirements.Add(
                new PrerequisiteRequirement(
                    "Module",
                    key,
                    version,
                    () =>
                        HostModuleDiscovery
                            .GetVersions(key)
                            .OrderByDescending(v => v)
                            .FirstOrDefault(),
                    a => a is System.Version actual && actual >= version,
                    "Install "
                        + key
                        + " at version "
                        + version
                        + " or later in a module search path."
                )
            );
        }

        foreach (var key in RequiredCommands)
        {
            Exact(key);
            requirements.Add(
                new PrerequisiteRequirement(
                    "Command",
                    key,
                    "Available",
                    () =>
                        InvokeCommand.GetCommand(key, CommandTypes.All) == null
                            ? "Missing"
                            : "Available",
                    a => (string?)a == "Available",
                    "Install or expose " + key + " in this session or its PATH."
                )
            );
        }

        foreach (
            var key in RequiredServices
                .Keys.Cast<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        )
        {
            Exact(key);
            var state = LanguagePrimitives.ConvertTo<string>(RequiredServices[key]);

            if (
                !string.IsNullOrEmpty(state)
                && !new[] { "Running", "Stopped", "Paused" }.Contains(
                    state,
                    StringComparer.OrdinalIgnoreCase
                )
            )
                throw new ArgumentException("Unsupported required service state: " + state);

            requirements.Add(
                new PrerequisiteRequirement(
                    "Service",
                    key,
                    RequiredServices[key],
                    () =>
                        new ServiceManager()
                            .GetService(key, TimeSpan.FromSeconds(60), Cancellation)
                            ?.State,
                    a =>
                        a != null
                        && (
                            string.IsNullOrEmpty(state)
                            || string.Equals((string)a, state, StringComparison.OrdinalIgnoreCase)
                        ),
                    "Check that service " + key + " exists and is in the requested state."
                )
            );
        }

        var result = new PrerequisiteEvaluator().Evaluate(requirements, Cancellation);
        WriteObject(
            SystemOutput.Object(
                "Applicable",
                result.Applicable,
                "Checks",
                result
                    .Checks.Select(c =>
                        SystemOutput.Object(
                            "Check",
                            c.Check,
                            "Target",
                            c.Target,
                            "Expected",
                            c.Expected,
                            "Actual",
                            c.Actual,
                            "Satisfied",
                            c.Satisfied,
                            "Reason",
                            c.Reason,
                            "Guidance",
                            c.Guidance
                        )
                    )
                    .ToArray()
            )
        );
    }

    private static void Exact(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || WildcardPattern.ContainsWildcardCharacters(name))
            throw new ArgumentException("Prerequisites require exact, nonempty names.");
    }

    private static object ReadArchitecture()
    {
        var native =
            Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432")
            ?? Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");

        switch (native?.ToUpperInvariant())
        {
            case "AMD64":
                return "x64";
            case "X86":
                return "x86";
            case "ARM64":
                return "Arm64";
            default:
                throw new InvalidOperationException("Unknown OS architecture: " + native);
        }
    }
}

internal static class HostModuleDiscovery
{
    internal static IEnumerable<Version> GetVersions(string name)
    {
        using (
            var pipeline = System.Management.Automation.PowerShell.Create(
                RunspaceMode.CurrentRunspace
            )
        )
            return pipeline
                .AddCommand("Microsoft.PowerShell.Core\\Get-Module")
                .AddParameter("ListAvailable")
                .AddParameter("Name", name)
                .AddParameter("ErrorAction", ActionPreference.Stop)
                .Invoke<PSModuleInfo>()
                .Select(module => module.Version)
                .ToArray();
    }
}

[Cmdlet(VerbsDiagnostic.Test, "PSWindowsUpdateAvailable"), OutputType(typeof(bool))]
public sealed class TestPSWindowsUpdateAvailableCommand : PSCmdlet
{
    protected override void ProcessRecord() =>
        WriteObject(HostModuleDiscovery.GetVersions("PSWindowsUpdate").Any());
}

[Cmdlet(VerbsCommon.Find, "ServiceAccountUsage"), OutputType(typeof(PSObject))]
public sealed class FindServiceAccountUsageCommand : SystemCommand
{
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true
    )]
    public string[] Name { get; set; } = Array.Empty<string>();

    [Parameter(Position = 1)]
    public string[] ComputerName { get; set; } = new[] { "localhost" };

    [Parameter(Position = 2), Credential]
    public PSCredential? Credential { get; set; }

    protected override void ProcessRecord()
    {
        var services = new List<PSObject>();
        var tasks = new List<PSObject>();

        foreach (var computer in ComputerName)
        {
            Cancellation.ThrowIfCancellationRequested();

            try
            {
                using (var options = new WSManSessionOptions())
                {
                    if (Credential != null)
                    {
                        var credential = Credential.GetNetworkCredential();
                        options.AddDestinationCredentials(
                            new CimCredential(
                                PasswordAuthenticationMechanism.Default,
                                credential.Domain,
                                credential.UserName,
                                Credential.Password
                            )
                        );
                    }

                    var local =
                        Credential == null
                        && (
                            computer == "."
                            || string.Equals(
                                computer,
                                "localhost",
                                StringComparison.OrdinalIgnoreCase
                            )
                            || string.Equals(
                                computer,
                                Environment.MachineName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                    using (
                        var session = local
                            ? CimSession.Create(null)
                            : CimSession.Create(computer, options)
                    )
                    {
                        var manager = new AccountUsageManager(session);

                        try
                        {
                            foreach (
                                var item in manager.GetServiceAccounts(
                                    TimeSpan.FromSeconds(60),
                                    Cancellation
                                )
                            )
                                services.Add(
                                    SystemOutput.Object(
                                        "ComputerName",
                                        computer,
                                        "DisplayName",
                                        item.DisplayName,
                                        "StartName",
                                        item.StartName,
                                        "State",
                                        item.State,
                                        "ProcessId",
                                        item.ProcessId
                                    )
                                );
                        }
                        catch (Exception error) when (!(error is OperationCanceledException))
                        {
                            WriteVerbose(
                                "Service query failed on " + computer + ": " + error.Message
                            );
                        }

                        try
                        {
                            foreach (
                                var item in manager.GetTaskAccounts(
                                    TimeSpan.FromSeconds(60),
                                    Cancellation
                                )
                            )
                                tasks.Add(
                                    SystemOutput.Object(
                                        "ComputerName",
                                        computer,
                                        "TaskName",
                                        item.TaskName,
                                        "Status",
                                        item.Status,
                                        "RunAsUser",
                                        item.RunAsUser
                                    )
                                );
                        }
                        catch (Exception error) when (!(error is OperationCanceledException))
                        {
                            WriteVerbose(
                                "Scheduled task query failed on " + computer + ": " + error.Message
                            );
                        }
                    }
                }
            }
            catch (Exception error) when (!(error is OperationCanceledException))
            {
                WriteVerbose("Account discovery failed on " + computer + ": " + error.Message);
            }
        }

        foreach (var account in Name)
        {
            var match = new WildcardPattern("*" + account + "*", WildcardOptions.IgnoreCase);
            WriteObject(
                SystemOutput.Object(
                    "Name",
                    account,
                    "Services",
                    services
                        .Where(s => match.IsMatch((string)s.Properties["StartName"].Value))
                        .ToArray(),
                    "SchTasks",
                    tasks
                        .Where(t => match.IsMatch((string)t.Properties["RunAsUser"].Value))
                        .ToArray()
                )
            );
        }
    }
}
