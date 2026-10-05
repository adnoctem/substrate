using System;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Policies;
using AdNoctem.Substrate.PowerShell.Windows;

namespace AdNoctem.Substrate.PowerShell.Policies;

public abstract class LgpoNetworkCommand : PSCmdlet, IDisposable
{
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();
    private protected CancellationToken Cancellation => stopping.Token;
    protected override void StopProcessing() { try { stopping.Cancel(); } catch (ObjectDisposedException) { } }
    protected override void EndProcessing() => Dispose();
    public void Dispose() => stopping.Dispose();
}

[Cmdlet(VerbsLifecycle.Install, "LGPO", SupportsShouldProcess = true), OutputType(typeof(string))]
public sealed class InstallLgpoCommand : LgpoNetworkCommand
{
    [Parameter(Position = 0)] public string Destination { get; set; } = Path.Combine(Environment.GetEnvironmentVariable("ProgramData") ?? "", "PSFoundation", "tools");
    [Parameter(Position = 1), ValidateSet("SCT-LGPO-Standalone")] public string Source { get; set; } = "SCT-LGPO-Standalone";
    [Parameter] public SwitchParameter Force { get; set; }
    protected override void ProcessRecord()
    {
        var directory = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Destination, out ProviderInfo provider, out _);
        if (provider.Name != "FileSystem")
            throw new ArgumentException("Destination must be a filesystem directory.");
        var displayPath = Path.Combine(Destination, "LGPO.exe");
        var source = LgpoSource.Standalone;
        if (File.Exists(Path.Combine(directory, "LGPO.exe")))
        {
            if (!Force)
            { WriteVerbose("LGPO.exe already present at '" + displayPath + "'; skipping download."); WriteObject(displayPath); return; }
            WriteVerbose("LGPO.exe already present at '" + displayPath + "'; -Force supplied, re-downloading.");
        }
        if (!ShouldProcess(Destination, "Install LGPO from " + source.DownloadUri.AbsoluteUri))
            return;
        using (var client = new HttpClient())
        {
            WriteVerbose("Downloading LGPO from " + source.DownloadUri.AbsoluteUri + "...");
            new LgpoTool(client).Install(FileSystemPath.Parse(directory), TimeSpan.FromMinutes(5), Force, source, Cancellation);
        }
        WriteVerbose("Installed LGPO.exe to '" + displayPath + "'.");
        WriteObject(displayPath);
    }
}

[Cmdlet(VerbsDiagnostic.Test, "LGPOSourceAvailability"), OutputType(typeof(PSObject))]
public sealed class TestLgpoSourceAvailabilityCommand : LgpoNetworkCommand
{
    [Parameter(Position = 0), ValidateSet("SCT-LGPO-Standalone")] public string Source { get; set; } = "SCT-LGPO-Standalone";
    protected override void ProcessRecord()
    {
        using (var client = new HttpClient())
        {
            var result = new LgpoTool(client).CheckSource(TimeSpan.FromSeconds(60), cancellationToken: Cancellation);
            if (!result.Available)
                WriteObject(SystemOutput.Object("Source", Source, "Url", result.Source.AbsoluteUri, "Available", false, "Error", result.Error!.Message, "CheckedAt", result.CheckedAtUtc));
            else
            {
                var length = result.ContentLength?.ToString(CultureInfo.InvariantCulture);
                var desktop = string.Equals(SessionState.PSVariable.GetValue("PSEdition") as string, "Desktop", StringComparison.OrdinalIgnoreCase);
                WriteObject(SystemOutput.Object("Source", Source, "Url", result.Source.AbsoluteUri, "Available", true,
                    "StatusCode", result.StatusCode!.Value, "ContentLength", desktop || length == null ? (object?)length : new[] { length }, "CheckedAt", result.CheckedAtUtc));
            }
        }
    }
}
