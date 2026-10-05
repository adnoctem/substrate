using System;
using System.Management.Automation;

namespace AdNoctem.Substrate.PowerShell.Infrastructure;

internal static class HostJsonSerializer
{
    // Serialization of arbitrary ETS objects is a PowerShell presentation contract. Use the installed host's built-in converter
    // in its current runspace, never source a script or delegate domain work to PowerShell. A separate serializer changes v1 date,
    // enum, extended-property, depth and escaping behavior between Desktop and Core.
    public static string Serialize(PSCmdlet owner, PSObject value)
    {
        using (var pipeline = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
        {
            pipeline.AddCommand("Microsoft.PowerShell.Utility\\ConvertTo-Json")
                .AddParameter("InputObject", value).AddParameter("Compress").AddParameter("Depth", 8);
            var result = pipeline.Invoke();
            foreach (var warning in pipeline.Streams.Warning)
                owner.WriteWarning(warning.Message);
            foreach (var error in pipeline.Streams.Error)
                owner.WriteError(error);
            return result.Count == 0 ? "" : (string)result[0].BaseObject;
        }
    }
}
