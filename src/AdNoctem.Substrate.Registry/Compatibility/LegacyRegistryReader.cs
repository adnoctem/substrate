using Microsoft.Win32;

namespace AdNoctem.Substrate.Registry.Compatibility;

/// <summary>Opens explicit registry views. A returned handle belongs to the caller.</summary>
internal sealed class LegacyRegistryReader
{
    public RegistryKey? Open(LegacyRegistryPath path, bool writable = false, RegistryView view = RegistryView.Default)
    {
        return new RegistryManager(view).OpenKey(new RegistryPath(path.Hive, path.SubKey), writable);
    }
}
