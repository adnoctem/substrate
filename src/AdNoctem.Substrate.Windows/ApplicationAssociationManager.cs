using System;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Windows;

/// <summary>An observed Windows file association between an identifier and its program registration.</summary>
public sealed class ApplicationAssociation
{
    public string Extension { get; }
    public string ProgramId { get; }
    public string Command { get; }
    internal ApplicationAssociation(string extension, string programId, string command) { Extension = extension; ProgramId = programId; Command = command; }
}
/// <summary>Reads and explicitly imports or exports Windows default-application associations.</summary>
public sealed class ApplicationAssociationManager
{
    private readonly RegistryManager registry;
    public ApplicationAssociationManager(RegistryManager? registry = null) { this.registry = registry ?? new RegistryManager(); }
    /// <summary>Reads the current user's explicit file association. Returns null for missing registration, and never executes its command.</summary>
    public ApplicationAssociation? Get(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension) || extension[0] != '.' || extension.IndexOfAny(new[] { '\\', '/', '\0' }) >= 0)
            throw new ArgumentException("Use a file extension with a leading dot.", nameof(extension));
        var id = registry.GetValue(RegistryPath.Parse(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension + @"\UserChoice"), "ProgId")?.Data as string;
        if (string.IsNullOrEmpty(id))
            return null;
        var command = registry.GetValue(RegistryPath.Parse(@"HKCR\" + id + @"\shell\open\command"))?.Data as string;
        return command == null ? null : new ApplicationAssociation(extension, id!, command);
    }
}
