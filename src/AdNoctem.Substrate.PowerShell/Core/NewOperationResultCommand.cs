using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using AdNoctem.Substrate.Core.Compatibility;

namespace AdNoctem.Substrate.PowerShell.Core;

[Cmdlet(VerbsCommon.New, "OperationResult")]
[OutputType(typeof(PSObject))]
public sealed class NewOperationResultCommand : PSCmdlet
{
    [Parameter(Position = 0)]
    public string? Target { get; set; }
    [Parameter(Position = 1)]
    public string? Source { get; set; }
    [Parameter(Position = 2)]
    public string? Scope { get; set; }
    [Parameter(Position = 3)]
    public string? Action { get; set; }
    [Parameter(Position = 4)]
    public string? Status { get; set; }
    [Parameter(Position = 5)]
    public string? Detail { get; set; }
    [Parameter(Position = 6)]
    public string? SkippedReason { get; set; }
    [Parameter(Position = 7)]
    public string? ErrorMessage { get; set; }
    [Parameter(Position = 8)]
    public Hashtable? Property { get; set; }
    [Parameter(Position = 9)]
    public bool Changed { get; set; }
    [Parameter(Position = 10)]
    public bool AlreadyCompliant { get; set; }
    [Parameter(Position = 11), AllowNull]
    public object? Before { get; set; }
    [Parameter(Position = 12), AllowNull]
    public object? After { get; set; }
    [Parameter(Position = 13)]
    public int ExitCode { get; set; }
    [Parameter(Position = 14)]
    public bool RebootRequired { get; set; }
    [Parameter(Position = 15)]
    public TimeSpan Duration { get; set; }
    [Parameter(Position = 16)]
    public string? RunId { get; set; }

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
        WriteObject(output);
    }
}
