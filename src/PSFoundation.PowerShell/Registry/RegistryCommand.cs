using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Microsoft.Win32;
using PSFoundation.Registry;
using PSFoundation.PowerShell.Infrastructure;

namespace PSFoundation.PowerShell.Registry;

/// <summary>PowerShell-only mapping, path expansion and stream handling.</summary>
public abstract class RegistryCommand : PSCmdlet
{
    protected readonly RegistryStore Store = new RegistryStore();
    protected IRegistryStateStore SnapshotStore => new StateBoundary(this);

    private sealed class StateBoundary : IRegistryStateStore
    {
        private readonly RegistryCommand command;
        public StateBoundary(RegistryCommand command) => this.command = command;
        public RegistryState Read(RegistryPath path, string name, RegistryView view) => command.ReadState(path, name, view);
        public void Apply(RegistryState desired) => command.Store.Apply(desired);
    }

    protected RegistryState ReadState(RegistryPath path, string name, RegistryView view)
    {
        try
        { return Store.Read(path, name, view); }
        catch (Exception error)
        {
            var message = error is UnauthorizedAccessException ? $"Access denied opening registry key: '{path.ProviderPath}'"
                : $"Failed to resolve registry path '{path.ProviderPath}': {error.Message}";
            ThrowTerminatingError(LegacyError.Create(message));
            throw;
        }
    }

    protected RegistryPath ParseRequired(string path)
    {
        try
        { return RegistryPath.Parse(path); }
        catch (ArgumentException)
        {
            ThrowTerminatingError(LegacyError.Create($"Unable to resolve registry hive from path: '{path}'"));
            throw;
        }
    }
    protected RegistryPath? Parse(string path)
    {
        try
        {
            return RegistryPath.Parse(path);
        }
        catch (ArgumentException) { LegacyError.Write(this, $"Unable to resolve registry hive from path: '{path}'"); return null; }
    }

    protected RegistryPath[] Expand(RegistryPath path)
    {
        if (!WildcardPattern.ContainsWildcardCharacters(path.ProviderPath))
            return new[] { path };
        try
        {
            ProviderInfo provider;
            return SessionState.Path.GetResolvedProviderPathFromPSPath(path.ProviderPath, out provider).Select(RegistryPath.Parse).ToArray();
        }
        catch (ItemNotFoundException) { return Array.Empty<RegistryPath>(); }
    }

    protected bool ValueExists(RegistryPath path, string name)
    {
        if (!WildcardPattern.ContainsWildcardCharacters(name))
            return Store.Read(path, name, RegistryView.Default).Exists;
        var pattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
        return Store.ValueNames(path).Any(pattern.IsMatch);
    }

    protected object ProviderValue(object? value, RegistryValueKind kind)
    {
        try
        {
            return NativeValue(value, kind);
        }
        catch (PSInvalidCastException) when (kind == RegistryValueKind.DWord) { return unchecked((int)LanguagePrimitives.ConvertTo<uint>(value)); }
        catch (PSInvalidCastException) when (kind == RegistryValueKind.QWord) { return unchecked((long)LanguagePrimitives.ConvertTo<ulong>(value)); }
    }

    protected void Failure(Exception error, string denied, string failed)
    {
        if (error is PipelineStoppedException || error is ActionPreferenceStopException)
            throw error;
        LegacyError.Write(this, error is UnauthorizedAccessException ? denied : failed + error.Message);
    }

    internal static PSObject Shape(params object?[] fields)
    {
        var result = new PSObject();
        for (var index = 0; index < fields.Length; index += 2)
            result.Properties.Add(new PSNoteProperty((string)fields[index]!, fields[index + 1]));
        return result;
    }

    internal static PSObject State(RegistryState state)
    {
        var result = Shape("SnapshotVersion", 1, "Path", state.Path.ProviderPath, "Name", state.Name, "View", state.View.ToString());
        if (state.KeyExists.HasValue)
            result.Properties.Add(new PSNoteProperty("KeyExists", state.KeyExists.Value));
        result.Properties.Add(new PSNoteProperty("Exists", state.Exists));
        result.Properties.Add(new PSNoteProperty("Type", state.Kind?.ToString()));
        result.Properties.Add(new PSNoteProperty("Preferred", state.Value));
        return result;
    }

    internal static object? Unwrap(object? value) => value is PSObject wrapped ? wrapped.BaseObject : value;
    internal static string Text(object? value) => LanguagePrimitives.ConvertTo<string>(value);

    internal static Dictionary<string, object?> Fields(object setting)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (Unwrap(setting) is IDictionary dictionary)
            foreach (DictionaryEntry item in dictionary)
                result[Text(item.Key)] = Unwrap(item.Value);
        else
            foreach (var property in PSObject.AsPSObject(setting).Properties)
                result[property.Name] = Unwrap(property.Value);
        return result;
    }

    internal static object NativeValue(object? value, RegistryValueKind kind)
    {
        value = Unwrap(value);
        switch (kind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                return Text(value);
            case RegistryValueKind.MultiString:
                return LanguagePrimitives.ConvertTo<string[]>(value);
            case RegistryValueKind.Binary:
            case RegistryValueKind.None:
                return LanguagePrimitives.ConvertTo<byte[]>(value);
            case RegistryValueKind.DWord:
                return LanguagePrimitives.ConvertTo<int>(value);
            case RegistryValueKind.QWord:
                return LanguagePrimitives.ConvertTo<long>(value);
            default:
                return value ?? "";
        }
    }

    protected object? Required(Dictionary<string, object?> fields, string name)
    {
        if (fields.TryGetValue(name, out var value))
            return value;
        var message = $"The property '{name}' cannot be found on this object. Verify that the property exists.";
        ThrowTerminatingError(new ErrorRecord(new PropertyNotFoundException(message), "PropertyNotFoundStrict", ErrorCategory.NotSpecified, null));
        return null;
    }

    protected void Invalid(string message) => ThrowTerminatingError(new ErrorRecord(new RuntimeException(message), message, ErrorCategory.OperationStopped, message));

    protected RegistryState Desired(object setting, bool requireVersion = false, bool expected = false)
    {
        var fields = Fields(setting);
        fields.TryGetValue("SnapshotVersion", out var version);
        if (requireVersion)
            version = Required(fields, "SnapshotVersion");
        if (requireVersion && !LanguagePrimitives.Equals(version, 1))
            Invalid(expected ? "ExpectedState requires detailed version 1 snapshots." : "Restoration requires detailed version 1 snapshots.");
        if (!fields.ContainsKey("Path") || !fields.ContainsKey("Name"))
            Invalid("Settings require Path and Name.");
        if (fields.ContainsKey("SnapshotVersion") && !LanguagePrimitives.Equals(version, 1))
            Invalid("Unsupported registry snapshot version.");
        if (fields.TryGetValue("Exists", out var existence) && !(existence is bool))
            Invalid("Exists must be a Boolean.");
        var exists = !fields.ContainsKey("Exists") || (bool)existence!;
        var value = Required(fields, "Preferred");
        fields.TryGetValue("Type", out var type);
        RegistryValueKind? kind = null;
        if (exists)
        {
            if (value == null || !LanguagePrimitives.IsTrue(type))
                Invalid("Existing values require Type and a non-null Preferred value. Use Exists = $false to represent absence.");
            kind = LanguagePrimitives.ConvertTo<RegistryValueKind>(type);
            if (kind == RegistryValueKind.Unknown || !Enum.IsDefined(typeof(RegistryValueKind), kind))
                Invalid("Unsupported registry value type: " + kind);
            value = NativeValue(value, kind.Value);
        }
        var view = fields.TryGetValue("View", out var suppliedView) ? LanguagePrimitives.ConvertTo<RegistryView>(suppliedView) : RegistryView.Default;
        return new RegistryState(ParseRequired(Text(fields["Path"])), Text(fields["Name"]), view, exists, kind, value);
    }

    protected void Status(string path, string? name, string? status)
    {
        if (status != null)
            WriteObject(name == null ? Shape("Path", path, "Status", status) : Shape("Path", path, "Name", name, "Status", status));
    }
}
