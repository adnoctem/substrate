using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AdNoctem.Substrate.Registry.Compatibility;

/// <summary>Publishes only successful nonempty output; failed runs preserve the previous destination.</summary>
internal sealed class LegacyRegistryFileService
{
    private readonly IRegistryCommandRunner tool;
    private readonly bool byteOrderMark;

    public LegacyRegistryFileService(IRegistryCommandRunner tool, bool byteOrderMark = false)
    {
        this.tool = tool;
        this.byteOrderMark = byteOrderMark;
    }

    public void Export(string key, string destination, CancellationToken cancellation) =>
        Publish(new[] { key }, destination, true, cancellation);

    public void Search(
        string root,
        string pattern,
        string destination,
        CancellationToken cancellation
    ) => Publish(new[] { "query", root, "/f", pattern, "/s" }, destination, false, cancellation);

    private void Publish(
        string[] arguments,
        string destination,
        bool export,
        CancellationToken cancellation
    )
    {
        destination = Path.GetFullPath(destination);
        var temporary = Path.Combine(
            Path.GetDirectoryName(destination)!,
            ".psf-reg-" + Guid.NewGuid().ToString("N") + ".tmp"
        );

        try
        {
            var result = tool.Run(
                export ? new[] { "export", arguments[0], temporary, "/y" } : arguments,
                cancellation
            );

            if (result.TimedOut || result.Cancelled || result.ExitCode != 0)
                throw new InvalidOperationException(
                    "Registry command did not complete successfully; the destination was not replaced."
                );

            if (!export)
            {
                var content = string.IsNullOrWhiteSpace(result.Output) ? "" : result.Output;

                if (!string.IsNullOrWhiteSpace(result.StandardError))
                    content += "\r\n--- STDERR ---\r\n" + result.StandardError;

                content += "\r\n--- EXITCODE: " + result.ExitCode + " ---\r\n\r\n";
                File.WriteAllText(temporary, content, new UTF8Encoding(byteOrderMark));
            }

            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new InvalidOperationException(
                    "Registry command did not produce a nonempty output file; the destination was not replaced."
                );

            cancellation.ThrowIfCancellationRequested();

            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
