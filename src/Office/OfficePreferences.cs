using System;
using System.Globalization;
using AdNoctem.Substrate.Registry;

namespace AdNoctem.Substrate.Office;

public enum OfficePreferenceValueType { String, DWord }
public enum OfficePreferenceApplication { Word, Excel, PowerPoint, Outlook, Access, OneNote }

/// <summary>A supported Office user preference. Constructing this value does not read or write the registry.</summary>
public sealed class OfficeApplicationPreference
{
    public RegistryPath Path { get; }
    public string Name { get; }
    public string Value { get; }
    public OfficePreferenceValueType Type { get; }
    public OfficePreferenceApplication Application { get; }
    public string Id { get; }
    public OfficeApplicationPreference(RegistryPath path, string name, string value, OfficePreferenceValueType type, OfficePreferenceApplication application, string id)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        if (path.Hive != Microsoft.Win32.RegistryHive.CurrentUser || !OfficeVersionResolver.IsMatch(path.SubKey, @"^software\\microsoft\\office\\16\.0\\(word|excel|powerpoint|outlook|access|onenote)(\\[a-z0-9 _-]+)+$")
            || !OfficeVersionResolver.IsMatch(name, "^[a-zA-Z0-9 _-]+$") || !OfficeVersionResolver.IsMatch(id, "^[a-zA-Z0-9_-]+$")
            || !Enum.IsDefined(typeof(OfficePreferenceValueType), type) || !Enum.IsDefined(typeof(OfficePreferenceApplication), application))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Application preference is outside the supported Office preference schema.");
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        if (type == OfficePreferenceValueType.DWord && (!OfficeVersionResolver.IsMatch(value, @"^\d{1,10}$") || !uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "REG_DWORD requires an unsigned 32-bit decimal value.");
        Name = name;
        Value = value;
        Type = type;
        Application = application;
        Id = id;
    }
    internal string ApplicationId => Application == OfficePreferenceApplication.PowerPoint ? "ppt16" : Application.ToString().ToLowerInvariant() + "16";
}

/// <summary>Explicit optional Office update settings; absent fields leave that setting unspecified.</summary>
public sealed class OfficeUpdateConfiguration
{
    public bool? Enabled { get; }
    public string? UpdatePath { get; }
    public Version? TargetVersion { get; }
    public OfficeChannel? Channel { get; }
    public OfficeUpdateConfiguration(bool? enabled = null, string? updatePath = null, Version? targetVersion = null, OfficeChannel? channel = null)
    {
        if (!enabled.HasValue && updatePath == null && targetVersion == null && !channel.HasValue)
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "Specify at least one update setting.");
        if (updatePath != null && !OfficeVersionResolver.IsMatch(updatePath, @"^(https://|[A-Za-z]:\\|\\\\)"))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "UpdatePath must be HTTPS or an absolute local/UNC path.");
        if (targetVersion != null && (targetVersion.Major != 16 || targetVersion.Minor != 0 || targetVersion.Build < 0 || targetVersion.Revision < 0))
            throw new OfficeException(OfficeFailureReason.InvalidConfiguration, "TargetVersion must be an exact Office build.");
        if (channel.HasValue && !Enum.IsDefined(typeof(OfficeChannel), channel.Value))
            throw new ArgumentOutOfRangeException(nameof(channel));
        Enabled = enabled;
        UpdatePath = updatePath;
        TargetVersion = targetVersion;
        Channel = channel;
    }
}
