using System;
using System.Management.Automation;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.PowerShell.Core;

[Cmdlet(VerbsDiagnostic.Resolve, "LongPath")]
[OutputType(typeof(string))]
public sealed class ResolveLongPathCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0), ValidateNotNullOrEmpty]
    public string LiteralPath { get; set; } = "";

    protected override void ProcessRecord()
    {
        System.Collections.ObjectModel.Collection<string> paths;
        ProviderInfo provider;

        try
        {
            paths = SessionState.Path.GetResolvedProviderPathFromPSPath(
                WildcardPattern.Escape(LiteralPath),
                out provider
            );
        }
        catch (ItemNotFoundException error)
        {
            ThrowTerminatingError(
                new ErrorRecord(error, "PathNotFound", ErrorCategory.ObjectNotFound, LiteralPath)
            );

            return;
        }
        catch (RuntimeException error)
        {
            ThrowTerminatingError(
                new ErrorRecord(
                    error,
                    error.ErrorRecord.FullyQualifiedErrorId,
                    error.ErrorRecord.CategoryInfo.Category,
                    error.ErrorRecord.TargetObject
                )
            );

            return;
        }

        if (provider.Name != "FileSystem")
        {
            const string message = "LiteralPath must identify an existing filesystem item.";
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

        foreach (var path in paths)
            WriteObject(FileSystemPath.Parse(path).ExpandLongName().Value);
    }
}
