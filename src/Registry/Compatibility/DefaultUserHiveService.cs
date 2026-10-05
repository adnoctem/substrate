using System;
using System.IO;
using System.Threading;

namespace AdNoctem.Substrate.Registry.Compatibility;

/// <summary>Hive lifecycle policy. Tests supply an isolated tool and mount/file probes.</summary>
internal sealed class DefaultUserHiveService
{
    private readonly IRegistryCommandRunner tool;
    private readonly Func<string, bool> mounted;
    private readonly Func<string, bool> fileExists;
    private readonly Action reclaimHandles;
    public DefaultUserHiveService(IRegistryCommandRunner tool, Func<string, bool> mounted, Func<string, bool>? fileExists = null, Action? reclaimHandles = null)
    {
        this.tool = tool;
        this.mounted = mounted;
        this.fileExists = fileExists ?? File.Exists;
        this.reclaimHandles = reclaimHandles ?? (() => { GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(500); });
    }

    public string? Mount(string name, string file, Func<string, string, bool> authorize, Action<string, ConsoleColor> log, CancellationToken cancellation)
    {
        if (!fileExists(file))
        {
            log($"Default user hive not found at '{file}'.", ConsoleColor.Red);
            return null;
        }
        if (mounted(name))
        {
            log($"A hive is already mounted at HKEY_USERS\\{name}. Dismount it first or choose a different MountName.", ConsoleColor.Red);
            return null;
        }
        if (!authorize("HKEY_USERS\\" + name, $"Load hive from '{file}'"))
            return null;
        var result = tool.Run(new[] { "load", "HKU\\" + name, file }, cancellation);
        if (result.ExitCode != 0)
        {
            log($"reg.exe load failed (exit {result.ExitCode}): {result.Diagnostic}", ConsoleColor.Red);
            return null;
        }
        log("Mounted default user hive: HKEY_USERS\\" + name, ConsoleColor.Green);
        return "Registry::HKEY_USERS\\" + name;
    }

    public void Dismount(string name, Func<string, string, bool> authorize, Action<string, ConsoleColor> log, CancellationToken cancellation)
    {
        if (!mounted(name))
        {
            log($"No hive mounted at HKEY_USERS\\{name}; nothing to unload.", ConsoleColor.Yellow);
            return;
        }
        if (!authorize("HKEY_USERS\\" + name, "Unload hive"))
            return;
        var arguments = new[] { "unload", "HKU\\" + name };
        var result = tool.Run(arguments, cancellation);
        if (result.ExitCode == 0)
        {
            log($"Unloaded HKEY_USERS\\{name}.", ConsoleColor.Green);
            return;
        }
        log($"First unload attempt failed: {result.Diagnostic}. Forcing GC and retrying.", ConsoleColor.Yellow);
        reclaimHandles();
        cancellation.ThrowIfCancellationRequested();
        result = tool.Run(arguments, cancellation);
        if (result.ExitCode != 0)
        {
            log($"reg.exe unload failed after GC retry (exit {result.ExitCode}): {result.Diagnostic}. Investigate which process still has the hive open before continuing.", ConsoleColor.Red);
            return;
        }
        log($"Unloaded HKEY_USERS\\{name} on retry.", ConsoleColor.Green);
    }
}
