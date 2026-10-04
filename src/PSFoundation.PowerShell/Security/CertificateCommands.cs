using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;
using PSFoundation.IO;
using PSFoundation.Security;
using PSFoundation.PowerShell.Infrastructure;
using PSFoundation.PowerShell.Windows;

namespace PSFoundation.PowerShell.Security;

[Cmdlet(VerbsCommon.Get, "CertificateInventory")]
public sealed class GetCertificateInventoryCommand : SystemCommand, IDynamicParameters
{
    [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true), Alias("ComputerName", "Computer")] public string[] Name { get; set; } = new[] { "localhost" };
    [Parameter(Position = 1)] public string Subject { get; set; } = "*";
    [Parameter(Position = 2)] public int DaysLeft { get; set; }
    [Parameter] public SwitchParameter NotExpired { get; set; }
    [Parameter(Position = 3), Credential] public PSCredential? Credential { get; set; }
    private readonly RuntimeDefinedParameterDictionary dynamic = new RuntimeDefinedParameterDictionary();
    public object GetDynamicParameters()
    {
        if (dynamic.Count != 0)
            return dynamic;
        var stores = new List<string>();
        foreach (var location in new[] { "LocalMachine", "CurrentUser" })
            foreach (var store in SessionState.InvokeProvider.ChildItem.Get(@"Cert:\" + location, false))
                stores.Add(@"Cert:\" + location + "\\" + store.Properties["Name"].Value);
        dynamic.Add("StorePath", new RuntimeDefinedParameter("StorePath", typeof(string), new System.Collections.ObjectModel.Collection<Attribute>
        { new ParameterAttribute { Mandatory = true, Position = 1 }, new ValidateSetAttribute(stores.ToArray()) }));
        return dynamic;
    }
    protected override void ProcessRecord()
    {
        var path = ((string)dynamic["StorePath"].Value).Split('\\');
        var location = (StoreLocation)Enum.Parse(typeof(StoreLocation), path[1], true);
        var pattern = new WildcardPattern(Subject, WildcardOptions.IgnoreCase);
        var now = DateTime.Now;
        foreach (var computer in Name)
        {
            try
            {
                foreach (var item in new CertificateManager().GetCertificates(location, path[2], computer, Credential?.GetNetworkCredential(), cancellationToken: Cancellation))
                {
                    var days = (item.NotAfter - now).Days;
                    if (!pattern.IsMatch(item.Subject) || (NotExpired && item.NotAfter <= now) || (MyInvocation.BoundParameters.ContainsKey("DaysLeft") && days > DaysLeft))
                        continue;
                    var certificate = item.OpenCertificate();
                    var output = new PSObject(certificate);
                    output.Properties.Add(new PSNoteProperty("ExtensionsFormatted", certificate.Extensions.Cast<X509Extension>().Select(e => e.Format(true)).ToArray()));
                    output.Properties.Add(new PSNoteProperty("DaysUntilExpired", days));
                    output.Properties.Add(new PSNoteProperty("Template", certificate.Extensions.Cast<X509Extension>().FirstOrDefault(e => e.Oid.Value == "1.3.6.1.4.1.311.21.7" || e.Oid.Value == "1.3.6.1.4.1.311.20.2")?.Format(false)));
                    WriteObject(output);
                }
            }
            catch (Exception error) when (!(error is OperationCanceledException)) { WriteError(new ErrorRecord(error, "CertificateInventoryFailed", ErrorCategory.ReadError, computer)); }
        }
    }
}

[Cmdlet(VerbsCommon.Set, "ScriptSignature", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium), OutputType(typeof(PSObject))]
public sealed class SetScriptSignatureCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Path { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1)] public X509Certificate2 Certificate { get; set; } = null!;
    [Parameter(Position = 2)] public string? TimestampServer { get; set; }
    protected override void ProcessRecord()
    {
        var root = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Folder not found: " + root);
        var timestamp = string.IsNullOrEmpty(TimestampServer) ? null : new Uri(TimestampServer, UriKind.Absolute);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            Cancellation.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Signing does not traverse reparse points: " + folder);
            foreach (var child in Directory.EnumerateDirectories(folder))
                pending.Push(child);
            foreach (var file in Directory.EnumerateFiles(folder).Where(p => new[] { ".ps1", ".psm1", ".psd1" }.Contains(System.IO.Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)))
            {
                Cancellation.ThrowIfCancellationRequested();
                if (!ShouldProcess(file, "Sign script with Authenticode"))
                { if (LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference"))) WriteObject(OperationOutput.Create(file, "Authenticode", "Sign", "DryRun", new Dictionary<string, object?> { ["Detail"] = "No files signed." })); continue; }
                try
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Signing does not follow reparse points.");
                    var result = new AuthenticodeSigner().Sign(FileSystemPath.Parse(file), Certificate, timestamp, cancellationToken: Cancellation);
                    WriteObject(OperationOutput.Create(file, "Authenticode", "Sign", result.Valid ? "Completed" : "Failed", new Dictionary<string, object?> { [result.Valid ? "Detail" : "ErrorMessage"] = "Signing produced status: " + result.Status }));
                }
                catch (Exception error) when (!(error is OperationCanceledException)) { WriteObject(OperationOutput.Create(file, "Authenticode", "Sign", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message })); }
            }
        }
    }
}
