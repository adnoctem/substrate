using System;
using System.Globalization;
using System.Management.Automation;
using AdNoctem.Substrate.Diagnostics;

namespace AdNoctem.Substrate.PowerShell.Diagnostics;

[Cmdlet(VerbsCommon.Get, "ErrorTranslation", DefaultParameterSetName = "Record")]
[OutputType(typeof(PSObject))]
public sealed class GetErrorTranslationCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Record")]
    public ErrorRecord ErrorRecord { get; set; } = null!;

    [Parameter(Mandatory = true, ParameterSetName = "Code"), ValidateNotNullOrEmpty]
    public object Code { get; set; } = null!;

    [Parameter(Mandatory = true, ParameterSetName = "Code")]
    [Parameter(ParameterSetName = "Record")]
    [ValidateSet("Appx", "Winget", "Dism", "Msi")]
    public string Domain { get; set; } = "";

    protected override void ProcessRecord()
    {
        var translator = new ErrorTranslator();
        var domain = string.IsNullOrEmpty(Domain)
            ? (ErrorDomain?)null
            : (ErrorDomain)Enum.Parse(typeof(ErrorDomain), Domain, true);
        ErrorTranslation? result;

        if (ParameterSetName == "Code")
        {
            var text =
                Convert
                    .ToString(
                        Code is PSObject wrapped ? wrapped.BaseObject : Code,
                        CultureInfo.InvariantCulture
                    )
                    ?.Trim()
                ?? "";

            if (!ErrorTranslator.TryParseCode(text, out var code))
            {
                var message = System.Text.RegularExpressions.Regex.IsMatch(text, @"^-?\d{1,10}$")
                    ? "Error code is outside the signed/unsigned 32-bit range."
                    : "Error code must be a 32-bit integer or hexadecimal code.";
                ThrowTerminatingError(
                    new ErrorRecord(
                        new RuntimeException(message),
                        message,
                        ErrorCategory.OperationStopped,
                        message
                    )
                );

                return;
            }

            result = translator.Translate(code, domain!.Value);
        }
        else
            result = translator.Translate(
                ErrorRecord.Exception,
                domain,
                ErrorRecord.ErrorDetails?.Message
            );

        if (result == null)
            return;

        var output = new PSObject();
        output.Properties.Add(new PSNoteProperty("Code", result.DisplayCode));
        output.Properties.Add(new PSNoteProperty("Domain", result.Domain.ToString()));
        output.Properties.Add(new PSNoteProperty("Benign", result.Benign));
        output.Properties.Add(new PSNoteProperty("Detail", result.Detail));
        output.Properties.Add(new PSNoteProperty("Matched", true));
        WriteObject(output);
    }
}
