using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Security;

public static class ElevationCompatibility
{
    public static int Run(
        string executable,
        string script,
        string workingDirectory,
        IDictionary? parameters,
        object[]? arguments
    )
    {
        var named = new List<string>();

        if (parameters != null)
            foreach (DictionaryEntry entry in parameters)
            {
                var name = LanguagePrimitives.ConvertTo<string>(entry.Key);

                if (string.Equals(name, "Elevated", StringComparison.OrdinalIgnoreCase))
                    continue;

                named.Add(Literal(name) + "=" + Literal(entry.Value));
            }

        named.Add("'Elevated'=$true");
        var command =
            "$global:LASTEXITCODE=0; try { Set-Location -LiteralPath "
            + Literal(workingDirectory)
            + " -ErrorAction Stop; $psfNamed=@{"
            + string.Join(";", named)
            + "}; $psfArguments="
            + Literal(arguments ?? Array.Empty<object>())
            + "; & "
            + Literal(script)
            + " @psfNamed @psfArguments; $psfSucceeded=$?; if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}; if(-not $psfSucceeded){exit 1}; exit $LASTEXITCODE } catch { Write-Error -ErrorRecord $_ -ErrorAction Continue; exit 1 }";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        if (encoded.Length > 30000)
            throw new ArgumentException(
                "Elevation arguments exceed the Windows command-line limit. Pass a configuration file path instead."
            );

        return new ElevationManager().Run(
            FileSystemPath.Parse(executable),
            new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded },
            FileSystemPath.Parse(workingDirectory)
        );
    }

    public static string Literal(object? value)
    {
        if (value is PSObject wrapped)
            value = wrapped.BaseObject;

        if (value == null)
            return "$null";

        if (value is SwitchParameter parameter)
            return parameter ? "$true" : "$false";

        if (value is bool flag)
            return flag ? "$true" : "$false";

        if (value is string || value is char)
            return "'"
                + CodeGeneration.EscapeSingleQuotedStringContent(
                    Convert.ToString(value, CultureInfo.InvariantCulture)!
                )
                + "'";

        if (value is Array array)
            return "@(" + string.Join(",", array.Cast<object?>().Select(Literal)) + ")";

        if (
            value is byte
            || value is sbyte
            || value is short
            || value is ushort
            || value is int
            || value is uint
            || value is long
            || value is ulong
            || value is float
            || value is double
            || value is decimal
        )
            return "([System."
                + value.GetType().Name
                + "]'"
                + Convert.ToString(value, CultureInfo.InvariantCulture)
                + "')";

        throw new ArgumentException(
            "Cannot forward elevation argument of type '"
                + value.GetType().FullName
                + "'. Use strings, numbers, booleans, switches, or arrays of these values."
        );
    }
}
