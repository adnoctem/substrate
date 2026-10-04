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
        var type = Type.GetType("Microsoft.PowerShell.Commands.WriteErrorException, Microsoft.PowerShell.Commands.Utility", false);
        if (type == null)
        {
            // A clean Windows PowerShell host may not have loaded Utility yet. Resolve the built-in command through
            // the host, rather than depending on a third-party module to have incidentally loaded its assembly.
            using var pipeline = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            var commands = pipeline.AddCommand("Microsoft.PowerShell.Core\\Get-Command").AddParameter("Name", "Microsoft.PowerShell.Utility\\Write-Error")
                .AddParameter("CommandType", CommandTypes.Cmdlet).AddParameter("ErrorAction", ActionPreference.Stop).Invoke<CmdletInfo>();
            if (commands.Count != 1)
                throw new InvalidOperationException("The host's built-in Write-Error command is unavailable.");
            type = commands[0].ImplementingType.Assembly.GetType("Microsoft.PowerShell.Commands.WriteErrorException", true)!;
        }
        var exception = (Exception)Activator.CreateInstance(type, message)!;
        return new ErrorRecord(exception, "Microsoft.PowerShell.Commands.WriteErrorException", ErrorCategory.NotSpecified, null);
    }
}
