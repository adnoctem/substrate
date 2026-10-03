using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSFoundation.Registry;

public sealed class RegistryToolResult
{
    public int? ExitCode { get; }
    public string Output { get; }
    public string StandardError { get; }
    public string Diagnostic => string.Join(" ", (Output + StandardError).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
    public bool Cancelled { get; }
    public bool TimedOut { get; }
    public RegistryToolResult(int? exitCode, string output = "", bool cancelled = false, bool timedOut = false, string standardError = "")
    {
        ExitCode = exitCode;
        Output = output;
        Cancelled = cancelled;
        TimedOut = timedOut;
        StandardError = standardError;
    }
}

public interface IRegistryTool
{
    RegistryToolResult Run(string[] arguments, CancellationToken cancellation);
}

/// <summary>Runs only the Windows registry utility, with independent argv quoting and drained output streams.</summary>
public sealed class RegistryTool : IRegistryTool
{
    private readonly string? workingDirectory;
    public RegistryTool(string? workingDirectory = null) => this.workingDirectory = workingDirectory;
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

    public RegistryToolResult Run(string[] arguments, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "reg.exe"),
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
            try
            {
                while (!process.WaitForExit(50))
                    cancellation.ThrowIfCancellationRequested();
                if (!Task.WaitAll(new Task[] { output, error }, 10000))
                    throw new IOException("Process output streams did not close within 10 seconds.");
                var text = output.GetAwaiter().GetResult();
                var diagnostic = error.GetAwaiter().GetResult();
                return new RegistryToolResult(process.ExitCode, text, standardError: diagnostic);
            }
            catch
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    if (!process.WaitForExit(10000))
                        throw new IOException("Child process did not terminate within 10 seconds.");
                }
                throw;
            }
        }
    }
}

/// <summary>Publishes only successful nonempty output; failed runs preserve the previous destination.</summary>
public sealed class RegistryFileService
{
    private readonly IRegistryTool tool;
    private readonly bool byteOrderMark;
    public RegistryFileService(IRegistryTool tool, bool byteOrderMark = false)
    {
        this.tool = tool;
        this.byteOrderMark = byteOrderMark;
    }

    public void Export(string key, string destination, CancellationToken cancellation) => Publish(new[] { key }, destination, true, cancellation);
    public void Search(string root, string pattern, string destination, CancellationToken cancellation) => Publish(new[] { "query", root, "/f", pattern, "/s" }, destination, false, cancellation);

    private void Publish(string[] arguments, string destination, bool export, CancellationToken cancellation)
    {
        destination = Path.GetFullPath(destination);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".psf-reg-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var result = tool.Run(export ? new[] { "export", arguments[0], temporary, "/y" } : arguments, cancellation);
            if (result.TimedOut || result.Cancelled || result.ExitCode != 0)
                throw new InvalidOperationException("Registry command did not complete successfully; the destination was not replaced.");
            if (!export)
            {
                var content = string.IsNullOrWhiteSpace(result.Output) ? "" : result.Output;
                if (!string.IsNullOrWhiteSpace(result.StandardError))
                    content += "\r\n--- STDERR ---\r\n" + result.StandardError;
                content += "\r\n--- EXITCODE: " + result.ExitCode + " ---\r\n\r\n";
                File.WriteAllText(temporary, content, new UTF8Encoding(byteOrderMark));
            }
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new InvalidOperationException("Registry command did not produce a nonempty output file; the destination was not replaced.");
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
