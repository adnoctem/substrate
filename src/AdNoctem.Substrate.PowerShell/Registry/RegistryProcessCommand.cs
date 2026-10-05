using AdNoctem.Substrate.Registry.Compatibility;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;
using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.PowerShell.Registry;

public abstract class RegistryProcessCommand : RegistryCommand, IDisposable
{
    private protected readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
    protected override void StopProcessing()
    {
        try
        { Cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }
    protected override void EndProcessing() => Dispose();
    public void Dispose() => Cancellation.Dispose();
    private protected DefaultUserHiveService HiveService => new DefaultUserHiveService(
        new RegistryCommandRunner(SessionState.Path.CurrentFileSystemLocation.Path),
        name => Store.KeyExists(RegistryPath.Parse("HKU\\" + name)),
        file => File.Exists(SessionState.Path.GetUnresolvedProviderPathFromPSPath(file)));

    private protected void Log(string text, ConsoleColor color)
    {
        // Write-Host uses the information stream with PSHOST tagging; preserve redirection and color.
        var message = new HostInformationMessage { Message = text, ForegroundColor = color, NoNewLine = false };
        WriteInformation(message, new[] { "PSHOST" });
    }
}
