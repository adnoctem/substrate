using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PSFoundation.Registry;

internal sealed class RegistryCommandResult
{
    public int? ExitCode { get; }
    public string Output { get; }
    public string StandardError { get; }
    public string Diagnostic => string.Join(" ", (Output + StandardError).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
    public bool Cancelled { get; }
    public bool TimedOut { get; }
    public RegistryCommandResult(int? exitCode, string output = "", bool cancelled = false, bool timedOut = false, string standardError = "")
    {
        ExitCode = exitCode;
        Output = output;
        Cancelled = cancelled;
        TimedOut = timedOut;
        StandardError = standardError;
    }
}

internal interface IRegistryCommandRunner
{
    RegistryCommandResult Run(string[] arguments, CancellationToken cancellation);
    Task<RegistryCommandResult> RunAsync(string[] arguments, CancellationToken cancellation);
}

/// <summary>Runs only the Windows registry utility, with independent argv quoting and drained output streams.</summary>
internal sealed class RegistryCommandRunner : IRegistryCommandRunner
{
    private readonly string? workingDirectory;
    private readonly string executable;
    private readonly TimeSpan timeout;
    public RegistryCommandRunner(string? workingDirectory = null, RegistryView view = RegistryView.Default, TimeSpan? timeout = null)
    {
        this.workingDirectory = workingDirectory;
        this.timeout = timeout ?? Timeout.InfiniteTimeSpan;
        if (this.timeout != Timeout.InfiniteTimeSpan && this.timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (Environment.Is64BitOperatingSystem && view != RegistryView.Default)
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), view == RegistryView.Registry32 ? "SysWOW64" : Environment.Is64BitProcess ? "System32" : "Sysnative");
        executable = Path.Combine(folder, "reg.exe");
    }
    public static string QuoteArgument(string value) => PSFoundation.Diagnostics.ProcessManager.QuoteArgument(value);

    public RegistryCommandResult Run(string[] arguments, CancellationToken cancellation) => RunAsync(arguments, cancellation).GetAwaiter().GetResult();

    public async Task<RegistryCommandResult> RunAsync(string[] arguments, CancellationToken cancellation)
    {
        var result = await new PSFoundation.Diagnostics.ProcessManager().RunAsync(new PSFoundation.Diagnostics.ProcessRequest(
            executable, arguments, workingDirectory, timeout: timeout, maximumCapturedCharacters: int.MaxValue), cancellation).ConfigureAwait(false);
        if (result.Cancelled)
            throw new OperationCanceledException(cancellation);
        if (result.TimedOut)
            throw new TimeoutException("Registry command exceeded its execution timeout.");
        return new RegistryCommandResult(result.ExitCode, result.StandardOutput, standardError: result.StandardError);
    }
}
