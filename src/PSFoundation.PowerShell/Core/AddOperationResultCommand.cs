using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using PSFoundation.Core.Compatibility;

namespace PSFoundation.PowerShell.Core;

[Cmdlet(VerbsCommon.Add, "OperationResult")]
public sealed class AddOperationResultCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0), AllowEmptyCollection, Infrastructure.UnwrapList]
    public IList Results { get; set; } = null!;
    [Parameter(Position = 1)] public string? Target { get; set; }
    [Parameter(Position = 2)] public string? Source { get; set; }
    [Parameter(Position = 3)] public string? Scope { get; set; }
    [Parameter(Position = 4)] public string? Action { get; set; }
    [Parameter(Position = 5)] public string? Status { get; set; }
    [Parameter(Position = 6)] public string? Detail { get; set; }
    [Parameter(Position = 7)] public string? SkippedReason { get; set; }
    [Parameter(Position = 8)] public string? ErrorMessage { get; set; }
    [Parameter(Position = 9)] public Hashtable? Property { get; set; }
    [Parameter(Position = 10)] public bool Changed { get; set; }
    [Parameter(Position = 11)] public bool AlreadyCompliant { get; set; }
    [Parameter(Position = 12), AllowNull] public object? Before { get; set; }
    [Parameter(Position = 13), AllowNull] public object? After { get; set; }
    [Parameter(Position = 14)] public int ExitCode { get; set; }
    [Parameter(Position = 15)] public bool RebootRequired { get; set; }
    [Parameter(Position = 16)] public TimeSpan Duration { get; set; }
    [Parameter(Position = 17)] public string? RunId { get; set; }
    [Parameter] public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        var supplied = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in MyInvocation.BoundParameters)
            supplied.Add(pair.Key, pair.Value);
        foreach (var name in new[] { "Source", "Scope", "Detail", "SkippedReason", "ErrorMessage", "RunId" })
            if (supplied.TryGetValue(name, out var value) && value == null)
                supplied[name] = "";
        var result = OperationResult.Create(Target ?? "", Action ?? "", Status ?? "", supplied, Property);
        var output = new PSObject();
        foreach (var pair in result.Fields)
            output.Properties.Add(new PSNoteProperty(pair.Key, pair.Value));
        try
        { Results.Add(output); }
        catch (Exception error)
        {
            var legacy = new MethodInvocationException("Exception calling \"Add\" with \"1\" argument(s): \"" + error.Message + "\"", error);
            ThrowTerminatingError(new ErrorRecord(legacy, error.GetType().Name, ErrorCategory.NotSpecified, null));
            return;
        }
        if (PassThru)
            WriteObject(output);
    }
}
