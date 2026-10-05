using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Interop;
using PSFoundation.IO;
using PSFoundation.Registry;

namespace PSFoundation.Windows;

/// <summary>Inspects font files and explicitly installs fonts into the Windows system font registration.</summary>
public sealed class FontManager
{
    /// <summary>Lists supported font files directly within the specified directory; does not recurse.</summary>
    public IReadOnlyList<FileSystemPath> FindFiles(FileSystemPath directory) => Array.AsReadOnly(Directory.EnumerateFiles(directory.Value)
        .Where(path => new[] { ".ttf", ".ttc", ".otf" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).Select(path => FileSystemPath.Parse(path)).ToArray());
    /// <summary>Reads a font's display name without permanently registering it; returns null when no usable name is found.</summary>
    public string? GetDisplayName(FileSystemPath file)
    {
        string? name = null;
        var kind = "(OpenType)";
        try
        {
            using (var shell = AutomationObject.Create("Shell.Application"))
            using (var folder = shell.CallObject("NameSpace", Path.GetDirectoryName(file.Value)))
            using (var item = folder.CallObject("ParseName", Path.GetFileName(file.Value)))
            {
                if (Convert.ToString(folder.Call("GetDetailsOf", item.Value, 2)).IndexOf("TrueType", StringComparison.OrdinalIgnoreCase) >= 0)
                    kind = "(TrueType)";
                name = Convert.ToString(folder.Call("GetDetailsOf", item.Value, 21));
            }
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException || error is ArgumentException || error is MissingMethodException) { }
        if (string.IsNullOrWhiteSpace(name))
            name = GetFallbackName(Path.GetFileName(file.Value));
        return string.IsNullOrWhiteSpace(name) ? null : name + " " + kind;
    }
    /// <summary>Derives a registration label from a supported font filename when font metadata is unavailable.</summary>
    public string? GetFallbackName(string fileName)
    {
        var name = Regex.Replace(fileName, @"\.(ttf|ttc|otf)$", "", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        name = Regex.Replace(name, @"[-_\[\]\.]", " ", RegexOptions.None, TimeSpan.FromSeconds(2));
        foreach (var token in new[] { "Mono", "NF", "NL", "VF", "wght", "Thin", "Semi", "Medium", "Extra", "Bold", "Italic", "Light", "Regular" })
            name = name.Replace(token, token == "wght" ? " Variable" : " " + token);
        name = Regex.Replace(name, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(2)).Trim();
        return name.Length == 0 ? null : name;
    }
    /// <summary>Copies and registers one font in the machine store. Does not elevate or remove the source unless explicitly requested.</summary>
    public string Install(FileSystemPath source, bool removeSource = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = GetDisplayName(source) ?? throw new InvalidOperationException("Could not derive a display name for the font.");
        var target = FileSystemPath.Parse(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", Path.GetFileName(source.Value)));
        if (string.Equals(source.Value, target.Value, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Source already belongs to the system font store.", nameof(source));
        using (var input = new FileStream(source.Value, FileMode.Open, FileAccess.Read, FileShare.Read))
            new FileManager().WriteAtomically(target, input, true, cancellationToken);
        new RegistryManager().SetValue(RegistryPath.Parse(@"HKLM\Software\Microsoft\Windows NT\CurrentVersion\Fonts"), name, RegistryValue.String(Path.GetFileName(source.Value)));
        if (removeSource)
        { cancellationToken.ThrowIfCancellationRequested(); File.Delete(source.Value); }
        return name;
    }
    /// <summary>Offloads font installation. Earlier filesystem and registry changes are not rolled back on failure.</summary>
    public Task<string> InstallAsync(FileSystemPath source, bool removeSource = false, CancellationToken cancellationToken = default)
        => Task.Run(() => Install(source, removeSource, cancellationToken), cancellationToken);
}
