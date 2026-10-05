using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.PowerShell.Infrastructure;

namespace AdNoctem.Substrate.PowerShell.Core;

[Cmdlet(VerbsCommunications.Write, "OperationResultLog")]
[OutputType(typeof(string))]
public sealed class WriteOperationResultLogCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0), AllowEmptyCollection] public IEnumerable Results { get; set; } = null!;
    [Parameter(Position = 1)] public string ScriptName { get; set; } = "";
    [Parameter(Position = 2), ValidatePattern("^[A-Za-z0-9._-]+$")] public string Name { get; set; } = "PSFoundation";
    [Parameter(Position = 3)] public string Path { get; set; } = "";
    [Parameter(Position = 4)] public string? RunId { get; set; }

    protected override void ProcessRecord()
    {
        var results = new List<object?>();
        var enumerator = LanguagePrimitives.GetEnumerator(Results);
        if (enumerator == null)
            results.Add(Results);
        else
            try
            { while (enumerator.MoveNext()) results.Add(enumerator.Current); }
            finally { (enumerator as IDisposable)?.Dispose(); }
        if (results.Count == 0)
        { WriteObject(null); return; }
        var script = string.IsNullOrWhiteSpace(ScriptName)
            ? string.IsNullOrEmpty(MyInvocation.ScriptName) ? Name + "-operation" : System.IO.Path.GetFileNameWithoutExtension(MyInvocation.ScriptName)
            : ScriptName;
        var path = Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            var temp = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrWhiteSpace(temp))
                temp = System.IO.Path.GetTempPath();
            path = System.IO.Path.Combine(temp, Name, "logs", Regex.Replace(script, "[^A-Za-z0-9._-]", "-") + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
        }
        path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out ProviderInfo provider, out _);
        if (provider.Name != "FileSystem")
        { Fail("The operation log path must identify a filesystem file."); return; }
        if (Directory.Exists(path))
        { Fail("The operation log path must identify a file, not a directory."); return; }
        if (File.Exists(path))
            path = FileSystemPath.Parse(path).ExpandLongName().Value;
        else
        {
            var parent = System.IO.Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(parent);
            path = System.IO.Path.Combine(FileSystemPath.Parse(parent).ExpandLongName().Value, System.IO.Path.GetFileName(path));
        }
        var lines = new List<string>();
        foreach (var result in results)
        {
            var entry = new PSObject();
            entry.Properties.Add(new PSNoteProperty("Timestamp", DateTime.Now.ToString("o")));
            entry.Properties.Add(new PSNoteProperty("Script", script));
            if (result != null)
                foreach (var property in PSObject.AsPSObject(result).Properties)
                {
                    var existing = entry.Properties[property.Name];
                    if (existing != null)
                        existing.Value = property.Value;
                    else
                        entry.Properties.Add(new PSNoteProperty(property.Name, property.Value));
                }
            if (MyInvocation.BoundParameters.ContainsKey("RunId") && entry.Properties["RunId"] == null)
                entry.Properties.Add(new PSNoteProperty("RunId", RunId ?? ""));
            lines.Add(HostJsonSerializer.Serialize(this, entry));
        }
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        WriteObject(path);
    }

    private void Fail(string message) => ThrowTerminatingError(new ErrorRecord(new RuntimeException(message), message, ErrorCategory.OperationStopped, message));
}
