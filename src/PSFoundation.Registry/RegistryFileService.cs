using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PSFoundation.Registry;

/// <summary>Local registry file operations. Import is a mutation, not an atomic transaction or a scoped sandbox.</summary>
public sealed class RegistryFileService
{
    private readonly RegistryManager manager;
    private readonly IRegistryCommandRunner runner;
    public RegistryFileService(RegistryManager manager, TimeSpan? timeout = null)
        : this(manager, new RegistryCommandRunner(view: (manager ?? throw new ArgumentNullException(nameof(manager))).View, timeout: timeout)) { }
    internal RegistryFileService(RegistryManager manager, IRegistryCommandRunner runner) { this.manager = manager; this.runner = runner; }

    /// <summary>Exports a local subtree to a registry text file; overwriting requires explicit permission.</summary>
    public void Export(RegistryPath path, string destination, bool overwrite = false, CancellationToken cancellationToken = default) =>
        ExportAsync(path, destination, overwrite, cancellationToken).GetAwaiter().GetResult();
    /// <summary>Runs reg.exe export with a bounded timeout and the manager's resolved registry view.</summary>
    public Task ExportAsync(RegistryPath path, string destination, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        RequireLocal();
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        return PublishAsync("export", path, destination, overwrite, cancellationToken);
    }

    /// <summary>Imports the entire file using the selected view. Cancellation/failure may leave partial registry changes.</summary>
    public void Import(string source, CancellationToken cancellationToken = default) => ImportAsync(source, cancellationToken).GetAwaiter().GetResult();
    /// <summary>Runs reg.exe import after validating the file. Cancellation or failure can leave partial registry changes.</summary>
    public async Task ImportAsync(string source, CancellationToken cancellationToken = default)
    {
        RequireLocal();
        var file = RequireFile(source);
        EnsureSuccess(await runner.RunAsync(new[] { "import", file, manager.View == Microsoft.Win32.RegistryView.Registry32 ? "/reg:32" : "/reg:64" }, cancellationToken).ConfigureAwait(false));
    }

    internal async Task PublishAsync(string operation, RegistryPath path, string destination, bool overwrite, CancellationToken cancellation)
    {
        RequireLocal();
        cancellation.ThrowIfCancellationRequested();
        destination = Path.GetFullPath(destination);
        if (!overwrite && File.Exists(destination))
            throw new IOException("The destination already exists.");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".psf-reg-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            EnsureSuccess(await runner.RunAsync(new[] { operation, path.ToString(), temporary, "/y" }, cancellation).ConfigureAwait(false));
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new IOException("Registry command produced no output file.");
            cancellation.ThrowIfCancellationRequested();
            if (overwrite && File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal void RequireLocal()
    {
        if (manager.MachineName != null)
            throw new NotSupportedException("Registry file and hive operations require an explicit local manager.");
    }
    internal static string RequireFile(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException("Registry input file does not exist.", full);
        return full;
    }
    internal static void EnsureSuccess(RegistryCommandResult result)
    {
        if (result.Cancelled)
            throw new OperationCanceledException();
        if (result.TimedOut)
            throw new TimeoutException("Registry command timed out.");
        if (result.ExitCode != 0)
            throw new RegistryCommandException(result.ExitCode, result.Output, result.StandardError);
    }
}

/// <summary>A failed reg.exe operation with captured standard output, standard error, and any observed exit code.</summary>
public sealed class RegistryCommandException : IOException
{
    public int? ExitCode { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }
    internal RegistryCommandException(int? exitCode, string output, string error) : base("Registry command failed with exit code " + exitCode + ". Registry mutations may have partially completed.")
    { ExitCode = exitCode; StandardOutput = output; StandardError = error; }
}
