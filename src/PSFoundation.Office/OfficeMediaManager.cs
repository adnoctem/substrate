using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;

namespace PSFoundation.Office;

public sealed class OfficeMediaAssessment
{
    public FileSystemPath Path { get; }
    public bool Valid => Error == null;
    public OfficeMediaManifest? Manifest { get; }
    public string? ManifestJson { get; }
    public IReadOnlyList<OfficeMediaFile> Files { get; }
    public string Stage { get; }
    public Exception? Error { get; }
    public OfficeFailureReason? Reason => Error == null ? (OfficeFailureReason?)null : Error is OfficeException office ? office.Reason : OfficeFailureReason.InvalidMedia;
    internal OfficeMediaAssessment(FileSystemPath path, string stage, OfficeMediaManifest? manifest, string? json, IEnumerable<OfficeMediaFile>? files, Exception? error)
    { Path = path; Stage = stage; Manifest = manifest; ManifestJson = json; Files = Array.AsReadOnly((files ?? Array.Empty<OfficeMediaFile>()).ToArray()); Error = error; }
}

/// <summary>Pure validation of a manifest against independently collected file evidence. Does not grant execution authority.</summary>
public sealed class OfficeMediaValidator
{
    public void ValidateTarget(OfficeMediaManifest manifest, OfficeConfiguration? target, string? exactVersionText = null)
    {
        if (manifest == null)
            throw new ArgumentNullException(nameof(manifest));
        if (target == null)
            return;
        var declared = manifest.Configuration;
        if (target.Product != declared.Product || target.Architecture != declared.Architecture || target.Channel != declared.Channel
            || target.Version != null && (target.Version != declared.Version || exactVersionText != null && !string.Equals(exactVersionText, manifest.VersionText, StringComparison.Ordinal)))
            throw new OfficeException(OfficeFailureReason.MediaMismatch, "Package product, architecture, channel, or build differs from target.");
    }
    /// <remarks>ExactVersionText optionally retains an external contract's textual pin. Normal C# callers compare typed versions.</remarks>
    public void Validate(OfficeMediaManifest manifest, IEnumerable<OfficeMediaFile> observedFiles, OfficeConfiguration? target = null, string? exactVersionText = null)
    {
        ValidateTarget(manifest, target, exactVersionText);
        var files = (observedFiles ?? throw new ArgumentNullException(nameof(observedFiles))).ToArray();
        if (files.Length == 0 || files.Any(file => file == null) || files.Length != manifest.Files.Count
            || files.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
            throw new OfficeException(OfficeFailureReason.MediaIntegrityFailed, "Media file set differs from the manifest.");
        var records = manifest.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
            if (!records.TryGetValue(file.Path, out var expected) || expected.Length != file.Length || !string.Equals(expected.Hash, file.Hash, StringComparison.OrdinalIgnoreCase))
                throw new OfficeException(OfficeFailureReason.MediaIntegrityFailed, "Media file size or hash differs from the manifest.");
        var paths = new HashSet<string>(files.Select(file => file.Path), StringComparer.OrdinalIgnoreCase);
        var architecture = (int)manifest.Configuration.Architecture;
        var platform = architecture == 64 ? "x64" : "x86";
        var catalogs = new[] { "Office/Data/v" + architecture + ".cab", "Office/Data/v" + architecture + "_" + manifest.VersionText + ".cab" };
        var missingCatalog = !catalogs.Any(paths.Contains);
        var neutral = "Office/Data/" + manifest.VersionText + "/stream." + platform + ".x-none.dat";
        var missingNeutral = !paths.Contains(neutral);
        var available = manifest.Configuration.Languages;
        var missingRequested = target == null ? Array.Empty<string>() : target.Languages.Where(language => !available.Contains(language, StringComparer.OrdinalIgnoreCase)).ToArray();
        var missingLanguages = available.Concat(missingRequested).Select(language => "Office/Data/" + manifest.VersionText + "/stream." + platform + "." + language + ".dat").Where(path => !paths.Contains(path)).ToArray();
        if (missingCatalog || missingNeutral || missingLanguages.Length != 0 || missingRequested.Length != 0)
        {
            var details = new List<string>();
            if (missingCatalog)
                details.Add("Missing metadata CAB; require one of: " + string.Join(", ", catalogs) + ".");
            if (missingNeutral)
                details.Add("Missing neutral payload: " + neutral + ".");
            if (missingLanguages.Length != 0)
                details.Add("Missing language payloads: " + string.Join(", ", missingLanguages) + ".");
            if (missingRequested.Length != 0)
                details.Add("Requested languages not declared in AvailableLanguages: " + string.Join(", ", missingRequested) + ".");
            throw new OfficeException(missingCatalog || missingNeutral ? OfficeFailureReason.MissingMedia : OfficeFailureReason.MissingLanguageMedia, string.Join(" ", details));
        }
    }
}

/// <summary>Reads protected Office packages without downloads, writes or executable invocation.</summary>
public sealed partial class OfficeMediaManager
{
    public async Task<IReadOnlyList<OfficeMediaFile>> ReadFilesAsync(FileSystemPath root, CancellationToken cancellationToken = default)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        var guard = new OfficePathGuard();
        guard.RequireProtected(root.Value, cancellationToken: cancellationToken);
        var office = root.Combine("Office");
        var data = office.Combine("Data");
        guard.ValidatePath(data.Value, cancellationToken);
        if (!Directory.Exists(data.Value))
            throw new OfficeException(OfficeFailureReason.MissingMedia, "Office\\Data is missing.");
        guard.RequireProtected(office.Value, cancellationToken: cancellationToken);
        foreach (var entry in Directory.EnumerateFileSystemEntries(office.Value))
        {
            guard.ValidatePath(entry, cancellationToken);
            if (!string.Equals(Path.GetFileName(entry), "Data", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(entry))
                throw new OfficeException(OfficeFailureReason.InvalidMedia, "Unexpected content outside Office/Data.");
        }
        var prefix = root.Value.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var queue = new Queue<string>();
        queue.Enqueue(data.Value);
        var files = new List<OfficeMediaFile>();
        var visited = 0;
        while (queue.Count != 0)
        {
            var directory = queue.Dequeue();
            guard.RequireProtected(directory, cancellationToken: cancellationToken);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++visited > 100000)
                    throw new OfficeException(OfficeFailureReason.InvalidMedia, "Media traversal exceeds supported bounds.");
                var full = guard.RequireProtected(entry, cancellationToken: cancellationToken);
                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                    queue.Enqueue(entry);
                else
                {
                    using (var lease = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (lease.Length == 0)
                            throw new OfficeException(OfficeFailureReason.InvalidMedia, "Empty Office payload file.");
                        var hash = await new FileManager().ComputeSha256Async(full, cancellationToken).ConfigureAwait(false);
                        files.Add(new OfficeMediaFile(entry.Substring(prefix.Length).Replace('\\', '/'), lease.Length, hash));
                    }
                }
            }
        }
        return Array.AsReadOnly(files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray());
    }
    public IReadOnlyList<OfficeMediaFile> ReadFiles(FileSystemPath root, CancellationToken cancellationToken = default)
        => ReadFilesAsync(root, cancellationToken).GetAwaiter().GetResult();
    /// <remarks>Inspection is a point-in-time observation. Execution must inspect again while owning its deployment lock and staging.</remarks>
    public async Task<OfficeMediaAssessment> InspectAsync(FileSystemPath root, OfficeConfiguration? target = null, CancellationToken cancellationToken = default, string? exactVersionText = null)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        var stage = "ProtectedPath";
        try
        {
            var guard = new OfficePathGuard();
            guard.RequireProtected(root.Value, "MediaDirectory", cancellationToken);
            var manifestPath = root.Combine(OfficeMediaManifest.FileName);
            guard.ValidatePath(manifestPath.Value, cancellationToken);
            if (!File.Exists(manifestPath.Value))
                throw new OfficeException(OfficeFailureReason.ReprepareMedia, "No schema-2 manifest exists; prepare this package again.");
            guard.RequireProtected(manifestPath.Value, "MediaManifest", cancellationToken);
            stage = "Read";
            string json;
            using (var input = new FileStream(manifestPath.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                if (input.Length > OfficeMediaManifest.MaximumJsonBytes)
                    throw new OfficeException(OfficeFailureReason.InvalidMedia, "Media manifest exceeds the 16 MiB limit.");
                using (var content = new MemoryStream())
                {
                    await input.CopyToAsync(content, 81920, cancellationToken).ConfigureAwait(false);
                    json = new UTF8Encoding(false, true).GetString(content.ToArray()).TrimStart('\uFEFF');
                }
            }
            stage = "Json";
            var manifest = OfficeMediaManifest.Parse(json);
            stage = "Validation";
            var validator = new OfficeMediaValidator();
            validator.ValidateTarget(manifest, target, exactVersionText);
            var files = await ReadFilesAsync(root, cancellationToken).ConfigureAwait(false);
            validator.Validate(manifest, files, target, exactVersionText);
            return new OfficeMediaAssessment(root, stage, manifest, json, files, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            if (stage == "Json" && error is OfficeException)
                stage = "Validation";
            return new OfficeMediaAssessment(root, stage, null, null, null, error);
        }
    }
    public OfficeMediaAssessment Inspect(FileSystemPath root, OfficeConfiguration? target = null, CancellationToken cancellationToken = default, string? exactVersionText = null)
        => InspectAsync(root, target, cancellationToken, exactVersionText).GetAwaiter().GetResult();
}
