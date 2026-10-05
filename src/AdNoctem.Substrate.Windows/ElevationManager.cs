using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Windows;

/// <summary>Explicit Windows RunAs requests. Only calling this API may show a UAC prompt; it never exits the calling application.</summary>
public sealed class ElevationManager
{
    /// <remarks>Cancellation prevents launch. Once started, the elevated child is allowed to finish and its exit code is returned. Do not pass secrets in arguments.</remarks>
    public int Run(FileSystemPath executable, IEnumerable<string> arguments, FileSystemPath workingDirectory, CancellationToken cancellationToken = default)
    {
        if (executable == null)
            throw new ArgumentNullException(nameof(executable));
        if (workingDirectory == null)
            throw new ArgumentNullException(nameof(workingDirectory));
        var values = (arguments ?? throw new ArgumentNullException(nameof(arguments))).ToArray();
        var commandLine = string.Join(" ", values.Select(Quote));
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(new ProcessStartInfo(executable.Value, commandLine)
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = workingDirectory.Value });
        if (process == null)
            throw new Win32Exception("Windows did not return the elevated process.");
        process.WaitForExit();
        return process.ExitCode;
    }
    public Task<int> RunAsync(FileSystemPath executable, IEnumerable<string> arguments, FileSystemPath workingDirectory, CancellationToken cancellationToken = default)
    { var snapshot = (arguments ?? throw new ArgumentNullException(nameof(arguments))).ToArray(); return Task.Run(() => Run(executable, snapshot, workingDirectory, cancellationToken), cancellationToken); }
    private static string Quote(string value)
    {
        if (value == null || value.IndexOf('\0') >= 0)
            throw new ArgumentException("Arguments cannot be null or contain NUL.");
        var output = new System.Text.StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            { slashes++; continue; }
            output.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            output.Append(character);
            slashes = 0;
        }
        return output.Append('\\', slashes * 2).Append('"').ToString();
    }
}
