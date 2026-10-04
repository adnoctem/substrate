using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using PSFoundation.IO;
using PSFoundation.PowerShell.Infrastructure;
using PSFoundation.PowerShell.Windows;
using PSFoundation.Security;

namespace PSFoundation.PowerShell.Security;

[Cmdlet(VerbsDiagnostic.Test, "WindowsEventLogChannel")]
public sealed class TestWindowsEventLogChannelCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)] public string LogName { get; set; } = "";
    protected override void ProcessRecord()
    {
        bool exists;
        try
        {
            var manager = new EventLogManager();
            exists = WildcardPattern.ContainsWildcardCharacters(LogName)
                ? manager.GetChannels().Any(new WildcardPattern(LogName, WildcardOptions.IgnoreCase).IsMatch) : manager.ChannelExists(LogName);
        }
        catch (Exception) { exists = false; }
        WriteObject(exists);
    }
}

[Cmdlet(VerbsData.Export, "EventLog")]
public sealed class ExportEventLogCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string LogName { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1)] public string OutputPath { get; set; } = "";
    [Parameter(Position = 2)] public string? MissingLogPath { get; set; }
    protected override void ProcessRecord()
    {
        try
        {
            var manager = new EventLogManager();
            if (!manager.ChannelExists(LogName))
            {
                if (!string.IsNullOrEmpty(MissingLogPath))
                {
                    var path = ResolveFile(MissingLogPath!);
                    var desktop = string.Equals(Convert.ToString(SessionState.PSVariable.GetValue("PSEdition")), "Desktop", StringComparison.OrdinalIgnoreCase);
                    using (var output = new StreamWriter(path.Value, true, new UTF8Encoding(desktop)))
                        output.WriteLine("Log not present: " + LogName);
                }
                WriteVerbose("Event log not found: " + LogName);
                WriteObject(false);
                return;
            }
            manager.Export(new WindowsEventQuery(LogName), ResolveFile(OutputPath), cancellationToken: Cancellation);
            WriteObject(true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { WriteError(LegacyError.Create("Failed to export event log '" + LogName + "': " + error.Message)); WriteObject(false); }
    }
    private FileSystemPath ResolveFile(string path)
    {
        ProviderInfo provider;
        PSDriveInfo drive;
        var resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out provider, out drive);
        if (provider.Name != "FileSystem")
            throw new ArgumentException("Use a filesystem destination.", nameof(path));
        return FileSystemPath.Parse(resolved);
    }
}
