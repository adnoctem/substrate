using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Diagnostics;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Packages.Runtime;

internal sealed class PackageRuntimeClient
{
    private readonly FileSystemPath executable;

    internal PackageRuntimeClient(FileSystemPath? executable = null) =>
        this.executable =
            executable
            ?? FileSystemPath.Parse(
                Path.Combine(
                    Path.GetDirectoryName(typeof(PackageRuntimeClient).Assembly.Location)!,
                    "AdNoctem.Substrate.WinRtHost.exe"
                )
            );

    internal RuntimeResponse Run(RuntimeRequest request, CancellationToken token) =>
        RunAsync(request, token).GetAwaiter().GetResult();

    internal async Task<RuntimeResponse> RunAsync(RuntimeRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (!File.Exists(executable.Value))
            throw new FileNotFoundException(
                "Deploy AdNoctem.Substrate.WinRtHost.exe beside the package library or supply its path explicitly.",
                executable.Value
            );

        var eventName = "Local\\PSFoundation.Package.Cancel." + Guid.NewGuid().ToString("N");

        using var cancelled = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);

        request.CancellationEvent = eventName;
        var argument = Convert.ToBase64String(PackageRuntimeProtocol.Serialize(request));

        if (argument.Length > 30000)
            throw new ArgumentException("Package request exceeds the command-line limit.");

        using var registration = token.Register(() => cancelled.Set());

        var result = await new ProcessManager()
            .RunAsync(
                new ProcessRequest(
                    executable.Value,
                    new[] { argument },
                    stopBehavior: ProcessStopBehavior.WaitForExit,
                    maximumCapturedCharacters: 16 * 1024 * 1024
                ),
                token
            )
            .ConfigureAwait(false);

        if (result.OutputTruncated)
            throw new InvalidDataException("Package runtime response was truncated.");

        RuntimeResponse response;

        try
        {
            response = PackageRuntimeProtocol.Deserialize<RuntimeResponse>(
                Encoding.UTF8.GetBytes(result.StandardOutput.TrimStart('\uFEFF'))
            );
        }
        catch (Exception error)
            when (error is System.Runtime.Serialization.SerializationException
                || error is ArgumentException
            )
        {
            throw new InvalidDataException(
                "The package runtime did not return a valid response (exit "
                    + result.ExitCode
                    + ").",
                error
            );
        }

        if (response.Schema != 1)
            throw new InvalidDataException("Unsupported package runtime protocol.");

        if (response.Cancelled)
            throw new OperationCanceledException(token);

        if (!response.Succeeded || result.ExitCode != 0)
            throw new COMException(
                string.IsNullOrWhiteSpace(response.Error)
                    ? "The native package operation failed."
                    : response.Error,
                response.ErrorCode
            );

        return response;
    }
}
