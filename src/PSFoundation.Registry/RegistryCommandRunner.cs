using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
    public static string QuoteArgument(string value)
    {
        if (value.IndexOf('\0') >= 0)
            throw new ArgumentException("Process arguments cannot contain a NUL character.", nameof(value));
        var text = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\')
            {
                slashes++;
                continue;
            }
            text.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            text.Append(c);
            slashes = 0;
        }
        text.Append('\\', slashes * 2).Append('"');
        return text.ToString();
    }

    public RegistryCommandResult Run(string[] arguments, CancellationToken cancellation) => RunAsync(arguments, cancellation).GetAwaiter().GetResult();

    public async Task<RegistryCommandResult> RunAsync(string[] arguments, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (workingDirectory != null)
                process.StartInfo.WorkingDirectory = workingDirectory;
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            var elapsed = Stopwatch.StartNew();
            try
            {
                while (!process.HasExited)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (timeout != Timeout.InfiniteTimeSpan && elapsed.Elapsed >= timeout)
                        throw new TimeoutException("Registry command exceeded its execution timeout.");
                    await Task.Delay(25, cancellation).ConfigureAwait(false);
                }
                var streams = Task.WhenAll(output, error);
                if (await Task.WhenAny(streams, Task.Delay(10000)).ConfigureAwait(false) != streams)
                    throw new IOException("Process output streams did not close within 10 seconds.");
                await streams.ConfigureAwait(false);
                var text = await output.ConfigureAwait(false);
                var diagnostic = await error.ConfigureAwait(false);
                return new RegistryCommandResult(process.ExitCode, text, standardError: diagnostic);
            }
            catch
            {
                if (!process.HasExited)
                {
                    try
                    { process.Kill(); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                    var cleanup = Stopwatch.StartNew();
                    while (!process.HasExited)
                    {
                        if (cleanup.Elapsed >= TimeSpan.FromSeconds(10))
                            throw new IOException("Child process did not terminate within 10 seconds.");
                        await Task.Delay(25).ConfigureAwait(false);
                    }
                }
                var streams = Task.WhenAll(output, error);
                if (await Task.WhenAny(streams, Task.Delay(10000)).ConfigureAwait(false) == streams)
                    try
                    { await streams.ConfigureAwait(false); }
                    catch (IOException) { }
                throw;
            }
        }
    }
}
