using System;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.Registry;
using AdNoctem.Substrate.Registry.Compatibility;
using Microsoft.Win32;
using RegistryPath = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryPath;
using RegistryReader = AdNoctem.Substrate.Registry.Compatibility.LegacyRegistryReader;

namespace AdNoctem.Substrate.PowerShell.Registry;

[Cmdlet(VerbsCommon.Set, "RegistryValue", SupportsShouldProcess = true)]
[OutputType(typeof(PSObject))]
public sealed class SetRegistryValueCommand : RegistryCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = "";

    [Parameter(Position = 1)]
    public string Name
    {
        get => name;
        set => name = value ?? "";
    }
    private string name = "";

    [Parameter(Mandatory = true, Position = 2), AllowNull]
    public object? Value { get; set; }

    [
        Parameter(Position = 3),
        ValidateSet("String", "ExpandString", "Binary", "DWord", "MultiString", "QWord", "Unknown")
    ]
    public RegistryValueKind Type { get; set; }

    protected override void ProcessRecord()
    {
        var path = Parse(Path);

        if (path == null)
        {
            WriteObject(null);

            return;
        }

        try
        {
            var value = Unwrap(Value);
            var kind = MyInvocation.BoundParameters.ContainsKey("Type")
                ? Type
                : RegistryOperations.InferKind(value);
            var pattern = WildcardPattern.ContainsWildcardCharacters(Name)
                ? new WildcardPattern(Name, WildcardOptions.IgnoreCase)
                : null;
            var status = new RegistryOperations(Store).Set(
                Path,
                path,
                Expand(path),
                Name,
                value,
                kind,
                selected => ProviderValue(value, selected),
                (target, action) => ShouldProcess(target, action),
                WriteVerbose,
                Text(Value),
                pattern == null ? null : (Func<string, bool>)pattern.IsMatch
            );
            Status(Path, Name, status);
        }
        catch (Exception error)
        {
            if (error.Message == $"Failed to ensure registry key exists: '{Path}'")
                Infrastructure.LegacyError.Write(this, error.Message);
            else
                Failure(
                    error,
                    $"Access denied setting registry value '{Name}' in '{Path}'",
                    $"Failed to set registry value '{Name}' in '{Path}': "
                );

            WriteObject(null);
        }
    }
}
