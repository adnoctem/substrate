using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;

namespace PSFoundation.Office;

public sealed partial class OfficeMediaManager
{
    /// <summary>Explicitly downloads Office media with a verified ODT, verifies the complete package and publishes by directory rename.</summary>
    /// <remarks>Existing compatible packages are reused; existing incomplete or incompatible content is never overwritten.
    /// Cancellation before publication leaves the destination untouched. Once published, inspection finishes before returning.</remarks>
    public async Task<OfficeMediaAssessment> PrepareAsync(OfficeConfiguration configuration, FileSystemPath destination, FileSystemPath executable,
        CancellationToken cancellationToken = default, string? exactVersionText = null)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (executable == null)
            throw new ArgumentNullException(nameof(executable));
        var guard = new OfficePathGuard();
        guard.ValidatePath(destination.Value, cancellationToken);
        if (Exists(destination.Value))
        {
            var existing = await InspectAsync(destination, configuration, cancellationToken, exactVersionText).ConfigureAwait(false);
            if (!existing.Valid)
                throw new OfficeException(OfficeFailureReason.InvalidMedia, "Existing package is incomplete or incompatible; use a new destination.", existing.Error);
            return existing;
        }
        var parent = Path.GetDirectoryName(destination.Value.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            throw new OfficeException(OfficeFailureReason.UnsafePath, "The destination parent must already exist.");
        var tool = new OdtTool();
        var assessment = await tool.CheckAsync(executable, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!assessment.Valid)
            throw new OfficeException(OfficeFailureReason.UntrustedTool, "ODT verification failed.");
        var work = FileSystemPath.Parse(Path.Combine(parent, "PSFOfficePrepare-" + Guid.NewGuid().ToString("N")));
        var created = false;
        var published = false;
        Exception? failure = null;
        try
        {
            guard.CreateProtectedDirectory(work.Value, cancellationToken);
            created = true;
            guard.RequireProtected(work.Value, cancellationToken: cancellationToken);
            var setup = work.Combine("setup.exe");
            using (var input = new FileStream(executable.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                await new FileManager().WriteAtomicallyAsync(setup, input, cancellationToken: cancellationToken).ConfigureAwait(false);
            var stagedTool = await tool.CheckAsync(setup, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!stagedTool.Valid)
                throw new OfficeException(OfficeFailureReason.UntrustedTool, "Staged ODT verification failed.");
            var document = new OdtConfigurationBuilder().Download(configuration, work.Value);
            var result = await tool.InvokeConfigurationAsync(setup, document, work, OdtMode.Download, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.ExitCode != 0 || result.Cancelled || result.TimedOut)
                throw new OfficeException(OfficeFailureReason.DownloadFailed, "ODT download returned " + result.ExitCode + ".");
            var files = await ReadFilesAsync(work, cancellationToken).ConfigureAwait(false);
            var builds = Directory.EnumerateDirectories(work.Combine("Office/Data").Value).Select(Path.GetFileName).Where(name => OfficeVersionResolver.IsMatch(name, @"^16\.0\.\d+\.\d+$")).ToArray();
            if (builds.Length != 1 || !Version.TryParse(builds[0], out var version) || configuration.Version != null && (configuration.Version != version
                || exactVersionText != null && !string.Equals(exactVersionText, builds[0], StringComparison.Ordinal)))
                throw new OfficeException(OfficeFailureReason.InvalidMedia, "Preparation did not produce exactly the selected build.");
            var pinned = new OfficeConfiguration(configuration.Product, configuration.Architecture, configuration.Channel, configuration.Languages, version, configuration.ExcludedApplications);
            var manifest = new OfficeMediaManifest(pinned, stagedTool.Version!.ToString(), files);
            // Validate before manifest publication as well as through a fresh read afterward.
            new OfficeMediaValidator().Validate(manifest, files, configuration, exactVersionText);
            using (var json = new MemoryStream(new UTF8Encoding(false).GetBytes(manifest.ToJson())))
                await new FileManager().WriteAtomicallyAsync(work.Combine(OfficeMediaManifest.FileName), json, cancellationToken: cancellationToken).ConfigureAwait(false);
            var prepared = await InspectAsync(work, configuration, cancellationToken, exactVersionText).ConfigureAwait(false);
            if (!prepared.Valid)
                throw new OfficeException(prepared.Reason!.Value, "Prepared media verification failed.", prepared.Error);
            guard.ValidatePath(setup.Value, cancellationToken);
            File.Delete(setup.Value);
            guard.ValidatePath(destination.Value, cancellationToken);
            guard.RequireProtected(work.Value, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(work.Value, destination.Value);
            published = true;
            return await InspectAsync(destination, configuration, exactVersionText: exactVersionText).ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            if (created && !published)
            {
                try
                { guard.RemoveWorkDirectory(work.Value, parent); }
                catch (Exception cleanup) { if (failure == null) throw; failure.Data["OfficeCleanupError"] = cleanup.Message; }
            }
        }
    }
    public OfficeMediaAssessment Prepare(OfficeConfiguration configuration, FileSystemPath destination, FileSystemPath executable,
        CancellationToken cancellationToken = default, string? exactVersionText = null)
        => PrepareAsync(configuration, destination, executable, cancellationToken, exactVersionText).GetAwaiter().GetResult();
    private static bool Exists(string path)
    {
        try
        { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
