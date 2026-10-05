using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Diagnostics;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Packages;

public enum WinGetAction { Install, Update, Uninstall }
public enum WinGetSelection { Id, Name, All }
/// <summary>A constrained WinGet operation with explicit selection, source, version, and agreement options.</summary>
public sealed class WinGetRequest
{
    public WinGetAction Action { get; }
    public WinGetSelection Selection { get; }
    public string? Target { get; }
    public IReadOnlyList<string> Arguments { get; }
    public WinGetRequest(WinGetAction action, WinGetSelection selection, string? target = null, string? version = null, string? source = null, bool includeUnknown = false, bool acceptAgreements = false)
    {
        if (!Enum.IsDefined(typeof(WinGetAction), action))
            throw new ArgumentOutOfRangeException(nameof(action));
        if (!Enum.IsDefined(typeof(WinGetSelection), selection))
            throw new ArgumentOutOfRangeException(nameof(selection));
        if (selection == WinGetSelection.All && action != WinGetAction.Update)
            throw new ArgumentException("All-packages selection is only supported for updates.");
        if (selection != WinGetSelection.All)
            Require(target, nameof(target));
        if (version != null)
            Require(version, nameof(version));
        if (source != null)
            Require(source, nameof(source));
        var args = new List<string> { action == WinGetAction.Update ? "upgrade" : action.ToString().ToLowerInvariant(), "--disable-interactivity", "--silent" };
        if (selection == WinGetSelection.All)
            args.Add("--all");
        else
        { args.Add(selection == WinGetSelection.Id ? "--id" : "--name"); args.Add(target!); args.Add("--exact"); }
        if (version != null)
        { if (action != WinGetAction.Install) throw new ArgumentException("A version is only supported for installation."); args.Add("--version"); args.Add(version); }
        if (source != null)
        { args.Add("--source"); args.Add(source); }
        if (includeUnknown)
        { if (action != WinGetAction.Update) throw new ArgumentException("Unknown versions apply only to updates."); args.Add("--include-unknown"); }
        if (acceptAgreements)
        { args.Add("--accept-source-agreements"); if (action != WinGetAction.Uninstall) args.Add("--accept-package-agreements"); }
        Action = action;
        Selection = selection;
        Target = target;
        Arguments = args.AsReadOnly();
    }
    private static void Require(string? value, string name)
    { if (string.IsNullOrWhiteSpace(value) || value!.StartsWith("-", StringComparison.Ordinal) || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) throw new ArgumentException("An exact non-option value is required.", name); }
}
/// <summary>Integration with an explicitly installed Windows Package Manager executable. Does not acquire or bootstrap WinGet.</summary>
public sealed class WinGetTool
{
    public FileSystemPath Executable { get; }
    public WinGetTool(FileSystemPath executable) => Executable = executable ?? throw new ArgumentNullException(nameof(executable));
    /// <summary>Locates the current user's WinGet executable alias without installing or downloading it.</summary>
    public static FileSystemPath? FindInstalled()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(path) ? FileSystemPath.Parse(path) : null;
    }
    /// <summary>Executes a constrained WinGet request with explicit package selection and agreement policy.</summary>
    /// <remarks>Once launched, wait for the installer to settle even after cancellation. Exit codes and cancellation remain evidence for the caller.</remarks>
    public ProcessResult Run(WinGetRequest request, CancellationToken cancellationToken = default)
        => new ProcessManager().Run(Create(request), cancellationToken);
    /// <summary>Executes WinGet asynchronously and returns native exit, output, and interruption evidence.</summary>
    public Task<ProcessResult> RunAsync(WinGetRequest request, CancellationToken cancellationToken = default)
        => new ProcessManager().RunAsync(Create(request), cancellationToken);
    private ProcessRequest Create(WinGetRequest request)
    { if (request == null) throw new ArgumentNullException(nameof(request)); return new ProcessRequest(Executable.Value, request.Arguments, stopBehavior: ProcessStopBehavior.WaitForExit, maximumCapturedCharacters: 1024 * 1024); }
}
