using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using PSFoundation.Diagnostics;

namespace PSFoundation.PowerShell.Diagnostics;

[Cmdlet(VerbsLifecycle.Invoke, "SafeProcess")]
[OutputType(typeof(string), typeof(PSObject))]
public sealed class InvokeSafeProcessCommand : PSCmdlet, IDisposable
{
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();
    [Parameter(Mandatory = true, Position = 0)] public string FilePath { get; set; } = "";
    [Parameter(Position = 1)] public string[]? ArgumentList { get; set; }
    [Parameter(Position = 2)] public string OutputPath { get; set; } = "";
    [Parameter] public SwitchParameter PassThru { get; set; }
    [Parameter] public SwitchParameter AsResult { get; set; }
    [Parameter(Position = 3), ValidateRange(0, int.MaxValue)] public int TimeoutSeconds { get; set; }
    [Parameter(Position = 4)] public CancellationToken CancellationToken { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token, CancellationToken))
            {
                var request = new ProcessRequest(FilePath, ArgumentList?.Select(a => a ?? ""),
                    timeout: TimeoutSeconds == 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(TimeoutSeconds),
                    stopBehavior: ProcessStopBehavior.TerminateTree, maximumCapturedCharacters: int.MaxValue);
                var result = new ProcessManager().Run(request, linked.Token);
                var text = (string.IsNullOrWhiteSpace(result.StandardOutput) ? "" : result.StandardOutput)
                    + (string.IsNullOrWhiteSpace(result.StandardError) ? "" : "\r\n--- STDERR ---\r\n" + result.StandardError)
                    + "\r\n--- EXITCODE: " + result.ExitCode + " ---\r\n";
                if (!string.IsNullOrEmpty(OutputPath))
                    WriteFile(text);
                if (AsResult)
                {
                    var output = new PSObject();
                    output.Properties.Add(new PSNoteProperty("ExitCode", result.ExitCode));
                    output.Properties.Add(new PSNoteProperty("StdOut", result.StandardOutput));
                    output.Properties.Add(new PSNoteProperty("StdErr", result.StandardError));
                    output.Properties.Add(new PSNoteProperty("Duration", result.Duration));
                    output.Properties.Add(new PSNoteProperty("TimedOut", result.TimedOut));
                    output.Properties.Add(new PSNoteProperty("Cancelled", result.Cancelled));
                    WriteObject(output);
                }
                else if (PassThru)
                    WriteObject(text);
            }
        }
        catch (PipelineStoppedException) { throw; }
        catch (Exception error)
        {
            // v1 exposed the PowerShell method invocation boundary, including its outer exception and native short error ID.
            var id = error.GetType().FullName;
            Exception legacy = error;
            var method = error is System.ComponentModel.Win32Exception ? "Start" : error is OperationCanceledException ? "ThrowIfCancellationRequested" : null;
            if (method != null)
            {
                id = error.GetType().Name;
                legacy = new MethodInvocationException("Exception calling \"" + method + "\" with \"0\" argument(s): \"" + error.Message + "\"", error);
            }
            if (AsResult)
            { ThrowTerminatingError(new ErrorRecord(legacy, id, ErrorCategory.NotSpecified, null)); return; }
            var message = "ERROR running " + FilePath + ": " + legacy.Message;
            if (!string.IsNullOrEmpty(OutputPath))
                WriteFile(message);
            if (PassThru)
                WriteObject(message);
            else
                Infrastructure.LegacyError.Write(this, message);
        }
    }

    private void WriteFile(string text)
    {
        var path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(OutputPath, out ProviderInfo provider, out _);
        if (provider.Name != "FileSystem")
            throw new InvalidOperationException("OutputPath must identify a filesystem file.");
        var desktop = string.Equals(SessionState.PSVariable.GetValue("PSEdition") as string, "Desktop", StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(path, text + Environment.NewLine, new UTF8Encoding(desktop));
    }

    protected override void StopProcessing() { try { stopping.Cancel(); } catch (ObjectDisposedException) { } }
    protected override void EndProcessing() => Dispose();
    public void Dispose() => stopping.Dispose();
}
