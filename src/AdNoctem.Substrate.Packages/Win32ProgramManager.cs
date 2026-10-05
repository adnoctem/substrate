using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Packages;

/// <summary>Reads local uninstall registrations through the shared registry API. Does not invoke Windows Installer or repair products.</summary>
public sealed partial class Win32ProgramManager
{
    private const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private static readonly string[] FieldNames = { "DisplayName", "DisplayVersion", "Publisher", "InstallLocation", "InstallDate",
        "UninstallString", "QuietUninstallString", "ModifyPath", "EstimatedSize", "SystemComponent" };
    public IReadOnlyList<ProgramRegistryLocation> Locations { get; }

    /// <param name="locations">Explicit local roots, including mounted hives if desired. Null selects machine views and the current user.
    /// The current user is the calling identity; no other user's profile is loaded. Default registry views resolve at construction.</param>
    public Win32ProgramManager(IEnumerable<ProgramRegistryLocation>? locations = null)
    {
        var roots = (locations ?? DefaultLocations()).ToArray();
        if (roots.Any(root => root == null))
            throw new ArgumentException("Locations cannot contain null.", nameof(locations));
        Locations = Array.AsReadOnly(roots);
    }

    private static IEnumerable<ProgramRegistryLocation> DefaultLocations()
    {
        if (Environment.Is64BitOperatingSystem)
            yield return new ProgramRegistryLocation(new RegistryPath(RegistryHive.LocalMachine, UninstallRoot), RegistryView.Registry64, ProgramRegistrationScope.Machine);
        yield return new ProgramRegistryLocation(new RegistryPath(RegistryHive.LocalMachine, UninstallRoot), RegistryView.Registry32, ProgramRegistrationScope.Machine);
        yield return new ProgramRegistryLocation(new RegistryPath(RegistryHive.CurrentUser, UninstallRoot), RegistryView.Default, ProgramRegistrationScope.CurrentUser);
    }

    /// <summary>Missing roots/registrations are absent. Access, malformed data and other read failures throw unless partial inventory is requested.</summary>
    public Win32ProgramInventory GetInventory(bool includeSystemComponents = false, bool continueOnError = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var programs = new List<Win32Program>();
        var errors = new List<ProgramInventoryError>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(root.View + ":" + root.Path))
                continue;
            var registry = new RegistryManager(root.View);
            IReadOnlyList<RegistryPath> children;
            try
            {
                if (!registry.KeyExists(root.Path))
                    continue;
                children = registry.GetSubKeys(root.Path);
            }
            catch (Exception error) when (continueOnError && IsReadFailure(error))
            { errors.Add(new ProgramInventoryError(root, error)); continue; }
            foreach (var path in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var location = new ProgramRegistryLocation(path, root.View, root.Scope);
                try
                {
                    var program = GetProgram(location, cancellationToken);
                    if (program != null && (includeSystemComponents || !program.IsSystemComponent))
                        programs.Add(program);
                }
                catch (Exception error) when (continueOnError && IsReadFailure(error))
                { errors.Add(new ProgramInventoryError(location, error)); }
            }
        }
        return new Win32ProgramInventory(programs, errors);
    }

    /// <summary>Reads one exact registration, including hidden components. Null means missing key, missing name or a blank name.</summary>
    public Win32Program? GetProgram(ProgramRegistryLocation location, CancellationToken cancellationToken = default)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));
        cancellationToken.ThrowIfCancellationRequested();
        var registry = new RegistryManager(location.View);
        var nameValue = registry.GetValue(location.Path, "DisplayName");
        if (nameValue == null)
            return null;
        if (nameValue.Kind != RegistryValueKind.String && nameValue.Kind != RegistryValueKind.ExpandString)
            throw new InvalidDataException("Uninstall DisplayName must be a registry string: " + location.Path);
        var name = nameValue.GetString();
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var values = new Dictionary<string, RegistryValue>(StringComparer.OrdinalIgnoreCase) { { "DisplayName", nameValue } };
        foreach (var field in FieldNames.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = registry.GetValue(location.Path, field);
            if (value != null)
                values.Add(field, value);
        }
        return new Win32Program(location, name, values);
    }

    /// <summary>Reads detached uninstall registrations on a worker thread.</summary>
    /// <remarks>Offloads synchronous registry reads. Cancellation is checked between native calls; an in-flight call cannot be interrupted.</remarks>
    public Task<Win32ProgramInventory> GetInventoryAsync(bool includeSystemComponents = false, bool continueOnError = false, CancellationToken cancellationToken = default)
        => Task.Run(() => GetInventory(includeSystemComponents, continueOnError, cancellationToken), cancellationToken);
    /// <summary>Offloads inspection of one exact uninstall registration.</summary>
    public Task<Win32Program?> GetProgramAsync(ProgramRegistryLocation location, CancellationToken cancellationToken = default)
        => Task.Run(() => GetProgram(location, cancellationToken), cancellationToken);

    private static bool IsReadFailure(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is SecurityException || error is NotSupportedException;
}
