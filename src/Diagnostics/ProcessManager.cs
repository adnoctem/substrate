using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Diagnostics;

/// <summary>Runs Windows executables without a shell. Each call owns and closes its process and redirected stream handles.</summary>
public sealed class ProcessManager
{
    /// <summary>Starts an independent process, closes the launch handle and returns its ID. No completion, output capture or termination is implied.</summary>
    /// <remarks>The caller explicitly accepts that the child outlives this operation. No shell, elevation or credential selection is performed.</remarks>
    public int StartDetached(string fileName, System.Collections.Generic.IEnumerable<string>? arguments = null, string? workingDirectory = null,
        System.Collections.Generic.IReadOnlyDictionary<string, string?>? environment = null, CancellationToken cancellationToken = default)
    {
        var request = new ProcessRequest(fileName, arguments, workingDirectory, environment);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Windows process execution is required.");
        cancellationToken.ThrowIfCancellationRequested();
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo(request.FileName, string.Join(" ", request.Arguments.Select(QuoteArgument)))
            { UseShellExecute = false, CreateNoWindow = true };
            if (request.WorkingDirectory != null)
                process.StartInfo.WorkingDirectory = request.WorkingDirectory;
            foreach (var pair in request.Environment)
                if (pair.Value == null)
                    process.StartInfo.Environment.Remove(pair.Key);
                else
                    process.StartInfo.Environment[pair.Key] = pair.Value;
            if (!process.Start())
                throw new InvalidOperationException("The executable did not start.");
            return process.Id;
        }
    }

    public ProcessResult Run(ProcessRequest request, CancellationToken cancellationToken = default)
        => RunAsync(request, cancellationToken).GetAwaiter().GetResult();

    /// <remarks>Cancellation before launch throws; after launch the configured stop policy returns completed evidence.
    /// Launch and cleanup failures throw. Synchronous process creation cannot be interrupted by cancellation.</remarks>
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Windows process execution is required.");
        cancellationToken.ThrowIfCancellationRequested();
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = request.FileName,
                Arguments = string.Join(" ", request.Arguments.Select(QuoteArgument)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (request.WorkingDirectory != null)
                process.StartInfo.WorkingDirectory = request.WorkingDirectory;
            foreach (var pair in request.Environment)
                if (pair.Value == null)
                    process.StartInfo.Environment.Remove(pair.Key);
                else
                    process.StartInfo.Environment[pair.Key] = pair.Value;
            var elapsed = Stopwatch.StartNew();
            if (!process.Start())
                throw new InvalidOperationException("The executable did not start.");
            var output = new Capture(request.MaximumCapturedCharacters);
            var error = new Capture(request.MaximumCapturedCharacters);
            var streams = Task.WhenAll(output.ReadAsync(process.StandardOutput), error.ReadAsync(process.StandardError));
            var cancelled = false;
            var timedOut = false;
            try
            {
                while (!process.HasExited)
                {
                    cancelled |= cancellationToken.IsCancellationRequested;
                    timedOut |= request.Timeout != Timeout.InfiniteTimeSpan && elapsed.Elapsed >= request.Timeout;
                    if ((cancelled || timedOut) && request.StopBehavior != ProcessStopBehavior.WaitForExit)
                    {
                        await TerminateAsync(process, request.StopBehavior, request.CleanupTimeout).ConfigureAwait(false);
                        break;
                    }
                    await Task.Delay(25).ConfigureAwait(false);
                }
                await DrainAsync(streams, request.CleanupTimeout).ConfigureAwait(false);
                return new ProcessResult(process.Id, process.ExitCode, output.Text, error.Text, output.Truncated, error.Truncated,
                    elapsed.Elapsed, timedOut, cancelled);
            }
            finally
            {
                try
                {
                    if (!process.HasExited && request.StopBehavior != ProcessStopBehavior.WaitForExit)
                        await TerminateAsync(process, request.StopBehavior, request.CleanupTimeout).ConfigureAwait(false);
                }
                finally
                {
                    // A descendant may retain a pipe after its parent exits. Closing our ends bounds ownership;
                    // observe reader failures without retaining process handles or unobserved task exceptions.
                    if (!streams.IsCompleted)
                    {
                        process.StandardOutput.Dispose();
                        process.StandardError.Dispose();
                    }
                    _ = streams.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
        }
    }

    /// <summary>Quotes one argument according to Windows C runtime rules; the result is not a command for cmd.exe or PowerShell.</summary>
    public static string QuoteArgument(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        if (value.IndexOf('\0') >= 0)
            throw new ArgumentException("Arguments cannot contain NUL.", nameof(value));
        var text = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\')
            { slashes++; continue; }
            text.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c);
            slashes = 0;
        }
        return text.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static async Task DrainAsync(Task streams, TimeSpan timeout)
    {
        using (var delayCancellation = new CancellationTokenSource())
        {
            if (await Task.WhenAny(streams, Task.Delay(timeout, delayCancellation.Token)).ConfigureAwait(false) != streams)
                throw new IOException("Process output streams did not close within the cleanup timeout.");
            delayCancellation.Cancel();
            await streams.ConfigureAwait(false);
        }
    }

    private static async Task TerminateAsync(Process process, ProcessStopBehavior behavior, TimeSpan timeout)
    {
        try
        {
            if (process.HasExited)
                return;
            if (behavior == ProcessStopBehavior.TerminateTree)
            {
                var killTree = typeof(Process).GetMethod("Kill", new[] { typeof(bool) });
                if (killTree != null)
                {
                    try
                    { killTree.Invoke(process, new object[] { true }); }
                    catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException && process.HasExited) { }
                }
                else
                {
                    using (var terminator = new Process())
                    {
                        terminator.StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe"),
                            Arguments = "/PID " + process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + " /T /F",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        terminator.Start();
                        var discard = Task.WhenAll(terminator.StandardOutput.ReadToEndAsync(), terminator.StandardError.ReadToEndAsync());
                        try
                        { await WaitAsync(terminator, timeout).ConfigureAwait(false); await DrainAsync(discard, timeout).ConfigureAwait(false); }
                        finally
                        {
                            if (!terminator.HasExited)
                            { terminator.Kill(); await WaitAsync(terminator, timeout).ConfigureAwait(false); }
                            _ = discard.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        }
                        if (terminator.ExitCode != 0 && !process.HasExited)
                            throw new Win32Exception("Cannot terminate the process tree.");
                    }
                }
            }
            else
                process.Kill();
        }
        catch (InvalidOperationException) when (process.HasExited) { }
        catch (Exception error)
        {
            // Failure to terminate the tree must not abandon the directly owned process. Preserve both failures if fallback also fails.
            try
            {
                if (!process.HasExited)
                    process.Kill();
                await WaitAsync(process, timeout).ConfigureAwait(false);
            }
            catch (Exception cleanup) { throw new AggregateException("Process-tree termination and direct-process cleanup failed.", error, cleanup); }
            throw;
        }
        await WaitAsync(process, timeout).ConfigureAwait(false);
    }

    private static async Task WaitAsync(Process process, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (!process.HasExited)
        {
            if (elapsed.Elapsed >= timeout)
                throw new IOException("Child process did not terminate within the cleanup timeout.");
            await Task.Delay(25).ConfigureAwait(false);
        }
    }

    private sealed class Capture
    {
        private readonly int maximum;
        private readonly StringBuilder content = new StringBuilder();
        public bool Truncated { get; private set; }
        public string Text => content.ToString();
        public Capture(int maximum) { this.maximum = maximum; }
        public async Task ReadAsync(StreamReader reader)
        {
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                var keep = Math.Min(count, maximum - content.Length);
                content.Append(buffer, 0, keep);
                Truncated |= keep != count;
            }
        }
    }
}
