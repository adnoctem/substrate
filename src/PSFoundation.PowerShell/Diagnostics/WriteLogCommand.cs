using System;
using System.Globalization;
using System.Management.Automation;

namespace PSFoundation.PowerShell.Diagnostics;

[Cmdlet(VerbsCommunications.Write, "Log")]
public sealed class WriteLogCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Message { get; set; } = "";
    [Parameter(Position = 1)]
    [ValidateSet("Black", "DarkBlue", "DarkGreen", "DarkCyan", "DarkRed", "DarkMagenta", "DarkYellow", "Gray", "DarkGray", "Blue", "Green", "Cyan", "Red", "Magenta", "Yellow", "White")]
    public ConsoleColor Color { get; set; } = ConsoleColor.White;
    [Parameter]
    public SwitchParameter Timestamps { get; set; }

    protected override void ProcessRecord()
    {
        if (string.Equals(SessionState.PSVariable.GetValue("PSEdition") as string, "Desktop", StringComparison.OrdinalIgnoreCase)
            && MyInvocation.BoundParameters.TryGetValue("InformationAction", out var preference) && (ActionPreference)preference == ActionPreference.Ignore)
        {
            // Windows PowerShell 5.1's nested Write-Host rejects this inherited preference. Preserve its established error contract.
            const string message = "The value Ignore is not supported for an ActionPreference variable. The provided value should be used only as a value for a preference parameter, and has been replaced by the default value. For more information, see the Help topic, \"about_Preference_Variables.\"";
            ThrowTerminatingError(new ErrorRecord(new NotSupportedException(message), "System.NotSupportedException", ErrorCategory.NotSpecified, null));
            return;
        }
        // Host presentation belongs exclusively to this adapter; the C# log sinks never select a console or clock.
        var text = Timestamps ? "[" + DateTime.Now.ToString("F", CultureInfo.CurrentCulture) + "]: " + Message : Message;
        WriteInformation(new HostInformationMessage { Message = text, ForegroundColor = Color, NoNewLine = false }, new[] { "PSHOST" });
    }
}
