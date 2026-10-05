using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PSFoundation.IO;

/// <summary>An absolute lexical filesystem path, independent of PowerShell providers and the process current directory.</summary>
public sealed class FileSystemPath : IEquatable<FileSystemPath>
{
    public string Value { get; }
    private FileSystemPath(string value) { Value = value; }

    /// <summary>Normalizes a fully qualified path or resolves a relative path against an explicit base directory.</summary>
    /// <remarks>Relative paths require an explicit absolute base. Normalization does not resolve links, establish existence, or authorize access.</remarks>
    /// <exception cref="ArgumentException">The path is empty, partially qualified, or relative without an absolute base.</exception>
    public static FileSystemPath Parse(string path, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A filesystem path is required.", nameof(path));
        if (!IsAbsolute(path))
        {
            if (baseDirectory == null || !IsAbsolute(baseDirectory))
                throw new ArgumentException("Relative paths require an absolute base directory.", nameof(baseDirectory));
            // Drive-relative and root-relative Windows paths carry ambient drive state. Never silently resolve it.
            if (Path.IsPathRooted(path) || path.IndexOf(':') >= 0)
                throw new ArgumentException("Partially qualified paths are not supported.", nameof(path));
            path = Path.Combine(baseDirectory, path);
        }
        return new FileSystemPath(Path.GetFullPath(path));
    }

    /// <summary>Resolves a relative path beneath this lexical base.</summary>
    /// <remarks>Parent segments are normalized and can leave the base directory. This method is not a containment check.</remarks>
    public FileSystemPath Combine(string relativePath)
    {
        if (relativePath == null)
            throw new ArgumentNullException(nameof(relativePath));
        if (Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0)
            throw new ArgumentException("A relative path is required.", nameof(relativePath));
        return Parse(relativePath, Value);
    }

    /// <summary>Expands Windows short names for an existing item. Native failures remain Win32 exceptions; symbolic links are not resolved.</summary>
    public FileSystemPath ExpandLongName()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _ = File.GetAttributes(Value);
            return this;
        }
        var capacity = 260;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var output = new StringBuilder(capacity);
            var length = GetLongPathName(Value, output, output.Capacity);
            if (length == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot expand filesystem path '" + Value + "'.");
            if (length < capacity)
                return new FileSystemPath(output.ToString());
            capacity = checked((int)length + 1);
        }
        throw new IOException("The path changed repeatedly while expanding '" + Value + "'. Retry path resolution.");
    }

    private static bool IsAbsolute(string path)
    {
        if (!Path.IsPathRooted(path))
            return false;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;
        return path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/')
            || path.Length >= 3 && (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal));
    }

    // Equality is lexical and ordinal: Windows directories can explicitly enable case-sensitive names.
    /// <summary>Compares normalized paths ordinally, preserving support for case-sensitive Windows directories.</summary>
    public bool Equals(FileSystemPath? other) => other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is FileSystemPath other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value;

    [DllImport("kernel32.dll", EntryPoint = "GetLongPathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string path, StringBuilder output, int length);
}
