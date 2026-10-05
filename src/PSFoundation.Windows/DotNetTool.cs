using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;
using PSFoundation.IO;

namespace PSFoundation.Windows;

public enum DotNetComponentKind { Runtime, Sdk }
/// <summary>One SDK or runtime installation reported by the selected dotnet executable.</summary>
public sealed class DotNetComponent
{
    public DotNetComponentKind Kind { get; }
    public string? Name { get; }
    public string Version { get; }
    public string BaseDirectory { get; }
    internal DotNetComponent(DotNetComponentKind kind, string? name, string version, string baseDirectory)
    { Kind = kind; Name = name; Version = version; BaseDirectory = baseDirectory; }
}
/// <summary>Parsed dotnet listing rows and lines that could not be interpreted.</summary>
public sealed class DotNetListing
{
    public IReadOnlyList<DotNetComponent> Components { get; }
    public IReadOnlyList<string> Lines { get; }
    public IReadOnlyList<string> UnrecognizedLines { get; }
    private DotNetListing(IEnumerable<DotNetComponent> components, string[] lines, IEnumerable<string> unknown)
    { Components = Array.AsReadOnly(components.ToArray()); Lines = Array.AsReadOnly(lines); UnrecognizedLines = Array.AsReadOnly(unknown.ToArray()); }
    /// <summary>Parses CLI listing output without normalizing semantic-version suffixes or treating unknown lines as installed components.</summary>
    public static DotNetListing Parse(string output, DotNetComponentKind kind)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!Enum.IsDefined(typeof(DotNetComponentKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var lines = output.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        var pattern = kind == DotNetComponentKind.Runtime ? @"^(?<name>\S+)\s+(?<version>\S+)\s+\[(?<path>.+)\]$" : @"^(?<version>\S+)\s+\[(?<path>.+)\]$";
        var matcher = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var components = new List<DotNetComponent>();
        var unknown = new List<string>();
        foreach (var line in lines)
        {
            var match = matcher.Match(line.Trim());
            if (match.Success)
                components.Add(new DotNetComponent(kind, kind == DotNetComponentKind.Runtime ? match.Groups["name"].Value : null,
                match.Groups["version"].Value, match.Groups["path"].Value));
            else
                unknown.Add(line);
        }
        return new DotNetListing(components, lines, unknown);
    }
}
/// <summary>Native dotnet process evidence together with the parsed component listing.</summary>
public sealed class DotNetQueryResult
{
    public ProcessResult Process { get; }
    public DotNetListing Listing { get; }
    public bool IsComplete => Process.ExitCode == 0 && !Process.Cancelled && !Process.TimedOut && !Process.OutputTruncated && Listing.UnrecognizedLines.Count == 0;
    internal DotNetQueryResult(ProcessResult process, DotNetComponentKind kind) { Process = process; Listing = DotNetListing.Parse(process.StandardOutput, kind); }
}

/// <summary>Read-only inventory through a caller-selected, trusted local dotnet executable. Does not download or install runtimes.</summary>
public sealed class DotNetTool
{
    /// <remarks>Lists the architecture visible to this executable, not every architecture installed on the host.</remarks>
    public DotNetQueryResult GetComponents(FileSystemPath executable, DotNetComponentKind kind, TimeSpan timeout, CancellationToken cancellationToken = default)
        => GetComponentsAsync(executable, kind, timeout, cancellationToken).GetAwaiter().GetResult();
    public async Task<DotNetQueryResult> GetComponentsAsync(FileSystemPath executable, DotNetComponentKind kind, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (executable == null)
            throw new ArgumentNullException(nameof(executable));
        if (!Enum.IsDefined(typeof(DotNetComponentKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(executable.Value))
            throw new FileNotFoundException("The selected dotnet executable does not exist.", executable.Value);
        var result = await new ProcessManager().RunAsync(new ProcessRequest(executable.Value, new[] { kind == DotNetComponentKind.Runtime ? "--list-runtimes" : "--list-sdks" },
            timeout: timeout, maximumCapturedCharacters: 1024 * 1024), cancellationToken).ConfigureAwait(false);
        return new DotNetQueryResult(result, kind);
    }
}
