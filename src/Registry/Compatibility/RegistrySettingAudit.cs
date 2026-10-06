using System;

namespace AdNoctem.Substrate.Registry.Compatibility;

/// <summary>Post-apply audit decisions used by existing configuration consumers.</summary>
internal sealed class RegistrySettingAudit
{
    public string Action { get; }
    public string Status { get; }
    public string? Detail { get; }

    private RegistrySettingAudit(string action, string status, string? detail)
    {
        Action = action;
        Status = status;
        Detail = detail;
    }

    public static RegistrySettingAudit Inspect(
        string path,
        object? target,
        bool undo,
        bool dryRun,
        string? description,
        Func<bool> hiveAvailable,
        Func<bool> exists,
        Func<object?> read,
        Func<object?, string> display
    )
    {
        var action = undo && target == null ? "RemoveValue" : "SetValue";

        if (dryRun)
            return new RegistrySettingAudit(action, "Skipped", "DryRun");

        if (
            path.StartsWith(
                @"Registry::HKEY_USERS\DefaultUser\",
                StringComparison.OrdinalIgnoreCase
            ) && !hiveAvailable()
        )
            return new RegistrySettingAudit(action, "Skipped", "DefaultUserHiveUnavailable");

        if (action == "RemoveValue")
            return exists()
                ? new RegistrySettingAudit(action, "Failed", "Value still exists after undo.")
                : new RegistrySettingAudit(action, "Removed", description);

        if (!exists())
            return new RegistrySettingAudit(action, "Failed", "Value is missing after apply.");

        var current = read();

        return RegistryOperations.LegacyEquals(current, target, true)
            ? new RegistrySettingAudit(action, "Completed", description)
            : new RegistrySettingAudit(
                action,
                "Failed",
                $"Expected '{display(target)}' but found '{display(current)}'."
            );
    }
}
