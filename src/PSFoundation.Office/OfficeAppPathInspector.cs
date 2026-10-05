using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace PSFoundation.Office;

public enum OfficeAppPathState { Uncertain, Missing, Present }
public enum OfficeAppPathReason { InvalidTarget, UnresolvedEnvironment, UnsupportedDrive, FilesystemRedirection, ReparsePoint, UnexpectedPathType, ExecutablePresent, TargetNotFound, AccessDenied, ProbeFailed }
/// <summary>Observed application registration, resolved executable, and reasons an App Paths entry may be stale or unusable.</summary>
public sealed class OfficeAppPathEvidence
{
    public RegistryView RegistryView { get; }
    public string RegistryPath { get; }
    private readonly object? rawTarget;
    public object? RawTarget => rawTarget is Array array ? array.Clone() : rawTarget;
    public string? ResolvedTarget { get; }
    public OfficeAppPathState State { get; }
    public OfficeAppPathReason Reason { get; }
    public Exception? Error { get; }
    internal OfficeAppPathEvidence(RegistryView view, string registryPath, object? rawTarget, string? resolvedTarget, OfficeAppPathState state, OfficeAppPathReason reason, Exception? error = null)
    { RegistryView = view; RegistryPath = registryPath; this.rawTarget = rawTarget is Array array ? array.Clone() : rawTarget; ResolvedTarget = resolvedTarget; State = state; Reason = reason; Error = error; }
}
/// <summary>Probes Office executable registrations without executing them or following directory reparse points and network paths.</summary>
public sealed class OfficeAppPathInspector
{
    public OfficeAppPathEvidence Inspect(OfficeRegistryRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        cancellationToken.ThrowIfCancellationRequested();
        var raw = record.GetValue("");
        string? resolved = null;
        OfficeAppPathEvidence Result(OfficeAppPathReason reason, OfficeAppPathState state = OfficeAppPathState.Uncertain, Exception? error = null)
            => new OfficeAppPathEvidence(record.View, record.Path.SubKey, raw, resolved, state, reason, error);
        try
        {
            if (!(raw is string value) || string.IsNullOrWhiteSpace(value))
                return Result(OfficeAppPathReason.InvalidTarget);
            var path = value.Trim();
            if (path.StartsWith("\"", StringComparison.Ordinal) && path.EndsWith("\"", StringComparison.Ordinal) && path.Length > 1)
                path = path.Substring(1, path.Length - 2);
            var environment = GetEnvironment(record.View);
            foreach (Match token in Regex.Matches(path, "%([^%]+)%", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                if (!environment.TryGetValue(token.Groups[1].Value, out var replacement) || string.IsNullOrWhiteSpace(replacement))
                    return Result(OfficeAppPathReason.UnresolvedEnvironment);
                path = path.Replace(token.Value, replacement);
            }
            if (!OfficeVersionResolver.IsMatch(path, @"^[A-Za-z]:\\")
                || OfficeVersionResolver.IsMatch(path, "[%\"*?<>|/\\x00-\\x1f]") || path.Substring(2).Contains(":")
                || OfficeVersionResolver.IsMatch(path, @"\\\\|\\\.{1,2}(\\|$)|[. ](\\|$)")
                || OfficeVersionResolver.IsMatch(path, @"\\(?:CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\\|\.|$)")
                || path.Substring(3).Split('\\').Any(segment => segment.Length > 255))
                return Result(OfficeAppPathReason.InvalidTarget);
            var expected = record.Path.SubKey.Substring(record.Path.SubKey.LastIndexOf('\\') + 1);
            if (!new[] { "WINWORD.EXE", "EXCEL.EXE", "OUTLOOK.EXE" }.Contains(expected, StringComparer.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase))
                return Result(OfficeAppPathReason.InvalidTarget);
            resolved = Path.GetFullPath(path);
            var root = Path.GetPathRoot(resolved)!;
            var drive = new DriveInfo(root);
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                return Result(OfficeAppPathReason.UnsupportedDrive);
            var windows = Environment.GetEnvironmentVariable("SystemRoot");
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess &&
                (!string.IsNullOrEmpty(windows) && resolved.StartsWith(windows!.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) || OfficeVersionResolver.IsMatch(resolved, @"~\d")))
                return Result(OfficeAppPathReason.FilesystemRedirection);
            var cursor = root;
            if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                return Result(OfficeAppPathReason.ReparsePoint);
            var parts = resolved.Substring(root.Length).Split('\\');
            for (var index = 0; index < parts.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursor = Path.Combine(cursor, parts[index]);
                FileAttributes attributes;
                try
                { attributes = File.GetAttributes(cursor); }
                catch (FileNotFoundException) { return Result(OfficeAppPathReason.TargetNotFound, OfficeAppPathState.Missing); }
                catch (DirectoryNotFoundException) { return Result(OfficeAppPathReason.TargetNotFound, OfficeAppPathState.Missing); }
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return Result(OfficeAppPathReason.ReparsePoint);
                if (((attributes & FileAttributes.Directory) != 0) != (index < parts.Length - 1))
                    return Result(OfficeAppPathReason.UnexpectedPathType);
            }
            return Result(OfficeAppPathReason.ExecutablePresent, OfficeAppPathState.Present);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Result(error is UnauthorizedAccessException || error is SecurityException ? OfficeAppPathReason.AccessDenied : OfficeAppPathReason.ProbeFailed, error: error); }
    }
    private static Dictionary<string, string?> GetEnvironment(RegistryView view)
    {
        string? Value(string name) => Environment.GetEnvironmentVariable(name);
        var program = Value("ProgramFiles");
        var common = Value("CommonProgramFiles");
        if (Environment.Is64BitOperatingSystem)
        { program = Value(view == RegistryView.Registry32 ? "ProgramFiles(x86)" : "ProgramW6432"); common = Value(view == RegistryView.Registry32 ? "CommonProgramFiles(x86)" : "CommonProgramW6432"); }
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProgramFiles"] = program,
            ["CommonProgramFiles"] = common,
            ["ProgramFiles(x86)"] = Value("ProgramFiles(x86)"),
            ["CommonProgramFiles(x86)"] = Value("CommonProgramFiles(x86)"),
            ["ProgramW6432"] = Value("ProgramW6432"),
            ["CommonProgramW6432"] = Value("CommonProgramW6432"),
            ["SystemDrive"] = Value("SystemDrive"),
            ["SystemRoot"] = Value("SystemRoot"),
            ["windir"] = Value("SystemRoot")
        };
    }
}
