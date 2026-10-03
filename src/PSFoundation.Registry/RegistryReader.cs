using Microsoft.Win32;

namespace PSFoundation.Registry;

/// <summary>Opens explicit registry views. A returned handle belongs to the caller.</summary>
public sealed class RegistryReader
{
    public RegistryKey? Open(RegistryPath path, bool writable = false, RegistryView view = RegistryView.Default)
    {
        var root = RegistryKey.OpenBaseKey(path.Hive, view);
        if (path.SubKey.Length == 0)
            return root;
        using (root)
            return root.OpenSubKey(path.SubKey, writable);
    }
}
