using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using PSFoundation.Registry;

namespace PSFoundation.PowerShell.Registry;

public abstract class RegistryToolCommand : RegistryCommand, IDisposable
{
    protected readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
    protected override void StopProcessing()
    {
        try
        { Cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }
    protected override void EndProcessing() => Dispose();
    public void Dispose() => Cancellation.Dispose();
    protected DefaultUserHiveService HiveService => new DefaultUserHiveService(
        new RegistryTool(SessionState.Path.CurrentFileSystemLocation.Path),
        name => Store.KeyExists(RegistryPath.Parse("HKU\\" + name)),
        file => File.Exists(SessionState.Path.GetUnresolvedProviderPathFromPSPath(file)));

    protected void Log(string text, ConsoleColor color)
    {
        // Write-Host uses the information stream with PSHOST tagging; preserve redirection and color.
        var message = new HostInformationMessage { Message = text, ForegroundColor = color, NoNewLine = false };
        WriteInformation(message, new[] { "PSHOST" });
    }
}
