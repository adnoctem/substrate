using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using PSFoundation.IO;
using PSFoundation.Registry;

namespace PSFoundation.Windows;

public sealed class DriveMapping
{
    public char DriveLetter { get; }
    public FileSystemPath Directory { get; }
    public string Label { get; }
    public string SourceLabel { get; }
    internal DriveMapping(char letter, FileSystemPath directory, string label, string sourceLabel) { DriveLetter = letter; Directory = directory; Label = label; SourceLabel = sourceLabel; }
}
/// <summary>Persistent DOS-device folder mappings. Writes are not transactional, do not elevate, and take effect after the caller's reboot.</summary>
public sealed class DriveMappingManager
{
    private static readonly RegistryPath Devices = RegistryPath.Parse(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices");
    private static readonly RegistryPath Icons = RegistryPath.Parse(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons");
    private readonly RegistryManager registry = new RegistryManager();
    public DriveMapping? Get(char driveLetter)
    {
        var letter = Letter(driveLetter);
        var value = registry.GetValue(Devices, letter + ":")?.Data as string;
        if (value == null)
            return null;
        if (!value.StartsWith(@"\??\", StringComparison.Ordinal) || value.Length < 7 || value[5] != ':')
            throw new IOException("The registration is not a local folder mapping.");
        var path = FileSystemPath.Parse(value.Substring(4));
        return new DriveMapping(letter, path, Label(letter) ?? "", Label(path.Value[0]) ?? "");
    }
    public DriveMapping Prepare(char driveLetter, FileSystemPath directory, string? label = null, string? sourceLabel = null, bool overwrite = false)
    {
        var letter = Letter(driveLetter);
        if (!System.IO.Directory.Exists(directory.Value) || directory.Value.StartsWith(@"\\", StringComparison.Ordinal) || string.Equals(Path.GetPathRoot(directory.Value), directory.Value.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use an existing local folder below a drive root.", nameof(directory));
        var mapping = Get(letter);
        if (mapping != null && !overwrite)
            throw new IOException("Drive letter is already mapped; use overwrite to replace it.");
        if (mapping == null && DriveInfo.GetDrives().Any(drive => char.ToUpperInvariant(drive.Name[0]) == letter))
            throw new IOException("DriveLetter cannot be a physical volume.");
        var source = new DriveInfo(Path.GetPathRoot(directory.Value)!);
        return new DriveMapping(letter, directory, string.IsNullOrEmpty(label) ? Path.GetFileName(directory.Value.TrimEnd('\\')) : label!,
            string.IsNullOrEmpty(sourceLabel) ? (string.IsNullOrEmpty(source.VolumeLabel) ? Label(directory.Value[0]) ?? "" : source.VolumeLabel) : sourceLabel!);
    }
    public DriveMapping Create(char driveLetter, FileSystemPath directory, string? label = null, string? sourceLabel = null, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var plan = Prepare(driveLetter, directory, label, sourceLabel, overwrite);
        cancellationToken.ThrowIfCancellationRequested();
        var source = char.ToUpperInvariant(directory.Value[0]);
        SetLabel(source, "");
        if (overwrite || string.IsNullOrEmpty(Label(source)))
            registry.SetValue(Icons.Combine(source + @"\DefaultLabel"), "", RegistryValue.String(plan.SourceLabel), true);
        registry.SetValue(Icons.Combine(plan.DriveLetter + @"\DefaultLabel"), "", RegistryValue.String(plan.Label), true);
        registry.SetValue(Devices, plan.DriveLetter + ":", RegistryValue.String(@"\??\" + directory.Value.TrimEnd('\\')), true);
        return plan;
    }
    public string Remove(char driveLetter, string? sourceLabel = null, bool forceRestoreLabel = false, CancellationToken cancellationToken = default)
    {
        var mapping = Get(driveLetter) ?? throw new IOException("Drive letter is not a mapped drive.");
        var source = char.ToUpperInvariant(mapping.Directory.Value[0]);
        var label = string.IsNullOrEmpty(sourceLabel) ? mapping.SourceLabel : sourceLabel!;
        if (string.IsNullOrEmpty(label))
            label = source == char.ToUpperInvariant(Environment.GetFolderPath(Environment.SpecialFolder.Windows)[0]) ? "System" : "Data";
        cancellationToken.ThrowIfCancellationRequested();
        registry.DeleteValue(Devices, mapping.DriveLetter + ":");
        registry.DeleteKey(Icons.Combine(mapping.DriveLetter.ToString()), true);
        var others = registry.GetValues(Devices).Any(value => value.Name.Length == 2 && value.Name[1] == ':' && value.Value.Data is string path && path.StartsWith(@"\??\" + source + @":\", StringComparison.OrdinalIgnoreCase));
        if (forceRestoreLabel || !others)
        { registry.DeleteKey(Icons.Combine(source.ToString()), true); SetLabel(source, label); }
        return label;
    }
    private string? Label(char letter) => registry.GetValue(Icons.Combine(char.ToUpperInvariant(letter) + @"\DefaultLabel"))?.Data as string;
    private static char Letter(char value) { value = char.ToUpperInvariant(value); if (value < 'A' || value > 'Z') throw new ArgumentOutOfRangeException(nameof(value)); return value; }
    private static void SetLabel(char letter, string value) { if (!SetVolumeLabel(letter + @":\", value)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "SetVolumeLabelW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetVolumeLabel(string root, string label);
}
