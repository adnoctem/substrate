using System;
using System.Management.Automation;

namespace PSFoundation.PowerShell.Infrastructure;

internal static class LegacyError
{
    public static void Write(PSCmdlet command, string message)
        => command.WriteError(Create(message));

    public static ErrorRecord Create(string message)
    {
        // Write-Error's exception is supplied by the active host, never shipped here.
        var type = Type.GetType("Microsoft.PowerShell.Commands.WriteErrorException, Microsoft.PowerShell.Commands.Utility", true)!;
        var exception = (Exception)Activator.CreateInstance(type, message)!;
        return new ErrorRecord(exception, "Microsoft.PowerShell.Commands.WriteErrorException", ErrorCategory.NotSpecified, null);
    }
}
