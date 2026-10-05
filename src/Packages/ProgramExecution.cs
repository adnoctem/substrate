using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Diagnostics;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Packages;

/// <summary>A native process request plus caller-selected success and reboot exit-code sets.</summary>
public sealed class ProgramExecutionRequest
{
    public ProcessRequest Process { get; }
    public IReadOnlyList<int> SuccessExitCodes { get; }
    public IReadOnlyList<int> RebootExitCodes { get; }
    public ProgramExecutionRequest(ProcessRequest process, IEnumerable<int>? successExitCodes = null, IEnumerable<int>? rebootExitCodes = null)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        SuccessExitCodes = Array.AsReadOnly((successExitCodes ?? new[] { 0, 1641, 3010 }).Distinct().ToArray());
        RebootExitCodes = Array.AsReadOnly((rebootExitCodes ?? new[] { 1641, 3010 }).Distinct().ToArray());
    }
}

/// <summary>Native process evidence interpreted using the request's success and reboot policy.</summary>
public sealed class ProgramExecutionResult
{
    public ProcessResult Process { get; }
    public bool Succeeded { get; }
    public bool RebootRequired { get; }
    internal ProgramExecutionResult(ProgramExecutionRequest request, ProcessResult process)
    {
        Process = process;
        Succeeded = (request.Process.StopBehavior == ProcessStopBehavior.WaitForExit || !process.Cancelled && !process.TimedOut)
            && request.SuccessExitCodes.Contains(process.ExitCode);
        RebootRequired = Succeeded && request.RebootExitCodes.Contains(process.ExitCode);
    }
}

public enum UninstallCommandStatus { Ready, MissingCommand, InteractiveApprovalRequired }
/// <summary>A selected registered uninstall command or the reason no permitted command could be selected.</summary>
public sealed class UninstallCommandSelection
{
    public string? Command { get; }
    public bool IsQuiet { get; }
    public UninstallCommandStatus Status { get; }
    internal UninstallCommandSelection(string? command, bool quiet, UninstallCommandStatus status) { Command = command; IsQuiet = quiet; Status = status; }
}

public sealed partial class Win32ProgramManager
{
    /// <summary>Prepares a caller-trusted local EXE or MSI without running it. MSI uses the system msiexec path explicitly.</summary>
    public ProgramExecutionRequest CreateInstallRequest(FileSystemPath installer, IEnumerable<string>? arguments = null,
        TimeSpan? timeout = null, ProcessStopBehavior stopBehavior = ProcessStopBehavior.WaitForExit,
        IEnumerable<int>? successExitCodes = null, IEnumerable<int>? rebootExitCodes = null)
    {
        if (installer == null)
            throw new ArgumentNullException(nameof(installer));
        if (!File.Exists(installer.Value))
            throw new FileNotFoundException("Installer does not exist.", installer.Value);
        var extension = Path.GetExtension(installer.Value);
        if (!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".msi", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Direct execution supports executable installers and MSI packages, without shell associations.");
        var msi = string.Equals(extension, ".msi", StringComparison.OrdinalIgnoreCase);
        var args = arguments ?? Array.Empty<string>();
        if (msi)
            args = new[] { "/i", installer.Value }.Concat(args);
        return new ProgramExecutionRequest(new ProcessRequest(msi ? MsiExecutable : installer.Value, args, timeout: timeout,
            stopBehavior: stopBehavior), successExitCodes, rebootExitCodes);
    }

    /// <summary>Chooses a registered uninstall command using explicit quiet and interactive-fallback preferences.</summary>
    public UninstallCommandSelection SelectUninstallCommand(Win32Program program, bool preferQuiet = false, bool allowInteractive = false)
    {
        if (program == null)
            throw new ArgumentNullException(nameof(program));
        return SelectUninstallCommand(program.UninstallCommand, program.QuietUninstallCommand, preferQuiet, allowInteractive);
    }
    /// <summary>A quiet request never silently grants permission to fall back to an interactive command.</summary>
    public UninstallCommandSelection SelectUninstallCommand(string? command, string? quietCommand, bool preferQuiet, bool allowInteractive)
    {
        var quiet = preferQuiet && !string.IsNullOrWhiteSpace(quietCommand);
        var selected = quiet ? quietCommand : command;
        return new UninstallCommandSelection(selected, quiet, string.IsNullOrWhiteSpace(selected) ? UninstallCommandStatus.MissingCommand
            : !quiet && !allowInteractive ? UninstallCommandStatus.InteractiveApprovalRequired : UninstallCommandStatus.Ready);
    }

    /// <summary>Parses a caller-selected, trusted registration command without a shell. Converts MSI install switches to uninstall switches.</summary>
    public ProgramExecutionRequest CreateUninstallRequest(string command, TimeSpan? timeout = null,
        ProcessStopBehavior stopBehavior = ProcessStopBehavior.WaitForExit)
    {
        var parsed = ProcessCommandLine.Parse(command);
        var file = parsed.FileName;
        var args = parsed.Arguments.ToArray();
        var name = Path.GetFileName(file);
        if (string.Equals(name, "msiexec", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "msiexec.exe", StringComparison.OrdinalIgnoreCase))
        {
            file = MsiExecutable;
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "/i", StringComparison.OrdinalIgnoreCase) || string.Equals(args[i], "-i", StringComparison.OrdinalIgnoreCase))
                    args[i] = "/x";
                else if (args[i].StartsWith("/i{", StringComparison.OrdinalIgnoreCase) || args[i].StartsWith("-i{", StringComparison.OrdinalIgnoreCase))
                    args[i] = "/x" + args[i].Substring(2);
                else if (string.Equals(args[i], "/package", StringComparison.OrdinalIgnoreCase))
                    args[i] = "/uninstall";
            }
        }
        return new ProgramExecutionRequest(new ProcessRequest(file, args, timeout: timeout, stopBehavior: stopBehavior));
    }
    /// <summary>Runs a prepared installer or uninstaller and classifies its exit and reboot evidence.</summary>
    public ProgramExecutionResult Execute(ProgramExecutionRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(request, cancellationToken).GetAwaiter().GetResult();
    /// <summary>Runs a prepared native operation asynchronously using its timeout and interruption policy.</summary>
    /// <remarks>WaitForExit does not interrupt a running installer. Other stop policies can leave partial installation state.
    /// Exit codes report the launched process; bootstrapper descendants may require product-specific completion checks.</remarks>
    public async Task<ProgramExecutionResult> ExecuteAsync(ProgramExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        return new ProgramExecutionResult(request, await new ProcessManager().RunAsync(request.Process, cancellationToken).ConfigureAwait(false));
    }
    /// <summary>Launches without waiting. Completion/exit/reboot status is deliberately not claimed; returns the process ID.
    /// The request's capture, timeout and stop settings apply only to Execute and are not used by Start.</summary>
    public int Start(ProgramExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        return new ProcessManager().StartDetached(request.Process.FileName, request.Process.Arguments, request.Process.WorkingDirectory,
            request.Process.Environment, cancellationToken);
    }
    private static string MsiExecutable => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
}
