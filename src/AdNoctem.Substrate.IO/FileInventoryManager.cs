using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.IO;

/// <summary>Detached filesystem evidence, including UTC write time and byte length, rather than an open file handle.</summary>
public sealed class FileSnapshot
{
    public FileSystemPath Path { get; }
    public DateTime LastWriteTimeUtc { get; }
    public DateTime LastAccessTimeUtc { get; }
    public DateTime CreationTimeUtc { get; }
    public FileAttributes Attributes { get; }
    public long Length { get; }
    public bool IsReadOnly => (Attributes & FileAttributes.ReadOnly) != 0;
    internal FileSnapshot(FileInfo file)
    {
        file.Refresh();
        Path = FileSystemPath.Parse(file.FullName);
        LastWriteTimeUtc = file.LastWriteTimeUtc;
        LastAccessTimeUtc = file.LastAccessTimeUtc;
        CreationTimeUtc = file.CreationTimeUtc;
        Attributes = file.Attributes;
        Length = file.Length;
    }
}
/// <summary>A filesystem location that could not be observed and the exception explaining why.</summary>
public sealed class FileInventoryError
{
    public string Path { get; }
    public Exception Error { get; }
    internal FileInventoryError(string path, Exception error) { Path = path; Error = error; }
}
/// <summary>File observations and retained traversal errors; inspect completeness before treating missing entries as absent.</summary>
public sealed class FileInventory
{
    public IReadOnlyList<FileSnapshot> Files { get; }
    public IReadOnlyList<FileInventoryError> Errors { get; }
    public IReadOnlyList<string> SkippedReparsePoints { get; }
    public bool IsComplete => Errors.Count == 0 && SkippedReparsePoints.Count == 0;
    internal FileInventory(List<FileSnapshot> files, List<FileInventoryError> errors, List<string> skipped)
    { Files = files.AsReadOnly(); Errors = errors.AsReadOnly(); SkippedReparsePoints = skipped.AsReadOnly(); }
}
/// <summary>Read-only file inventory. Does not follow directory reparse points or infer that unreadable locations contain no files.</summary>
public sealed class FileInventoryManager
{
    /// <remarks>Write-time bounds are exclusive. Cancellation is checked between filesystem calls; an in-flight filesystem call may finish first.
    /// A scan is not an atomic filesystem snapshot, and paths may change during enumeration.</remarks>
    public FileInventory Find(FileSystemPath root, DateTimeOffset? writtenAfter = null, DateTimeOffset? writtenBefore = null,
        bool recursive = true, bool includeHidden = false, bool continueOnError = false, CancellationToken cancellationToken = default)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        if (writtenAfter.HasValue && writtenBefore.HasValue && writtenAfter > writtenBefore)
            throw new ArgumentException("The write-time window is reversed.");
        cancellationToken.ThrowIfCancellationRequested();
        var files = new List<FileSnapshot>();
        var errors = new List<FileInventoryError>();
        var skipped = new List<string>();
        var directories = new Stack<string>();
        bool Recover(string path, Exception error)
        {
            if (!continueOnError || !(error is IOException || error is UnauthorizedAccessException || error is SecurityException))
                return false;
            errors.Add(new FileInventoryError(path, error));
            return true;
        }
        void Visit(string path, bool explicitRoot = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var attributes = File.GetAttributes(path);
                if (!explicitRoot && !includeHidden && (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    return;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    { skipped.Add(path); return; }
                    if (recursive || explicitRoot)
                        directories.Push(path);
                    return;
                }
                var file = new FileSnapshot(new FileInfo(path));
                if ((!writtenAfter.HasValue || file.LastWriteTimeUtc > writtenAfter.Value.UtcDateTime)
                    && (!writtenBefore.HasValue || file.LastWriteTimeUtc < writtenBefore.Value.UtcDateTime))
                    files.Add(file);
            }
            catch (Exception error) when (Recover(path, error)) { }
        }
        Visit(root.Value, true);
        while (directories.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = directories.Pop();
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                    Visit(path);
            }
            catch (Exception error) when (Recover(directory, error)) { }
        }
        files.Sort((left, right) => left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc));
        return new FileInventory(files, errors, skipped);
    }
    public Task<FileInventory> FindAsync(FileSystemPath root, DateTimeOffset? writtenAfter = null, DateTimeOffset? writtenBefore = null,
        bool recursive = true, bool includeHidden = false, bool continueOnError = false, CancellationToken cancellationToken = default)
        => Task.Run(() => Find(root, writtenAfter, writtenBefore, recursive, includeHidden, continueOnError, cancellationToken), cancellationToken);
}
