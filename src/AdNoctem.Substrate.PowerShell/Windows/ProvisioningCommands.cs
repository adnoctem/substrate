using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.PowerShell.Infrastructure;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Windows;

[Cmdlet(VerbsCommon.New, "OfflineDomainJoinBlob", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High), OutputType(typeof(PSObject))]
public sealed class NewOfflineDomainJoinBlobCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0), Alias("MachineName")] public string ComputerName { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1)] public string Domain { get; set; } = "";
    [Parameter(Position = 2), Credential] public PSCredential? Credential { get; set; }
    [Parameter(Position = 3)] public string? MachineAccountOU { get; set; }
    [Parameter(Position = 4)] public string? DCName { get; set; }
    [Parameter(Position = 5), ValidateRange(0, 4096)] public uint ProvisionOptions { get; set; } = 2;
    protected override void ProcessRecord()
    {
        if (!ShouldProcess(ComputerName + " in " + Domain, "Provision computer account and generate offline-join blob"))
        {
            if (MyInvocation.BoundParameters.TryGetValue("WhatIf", out var whatIf) ? LanguagePrimitives.IsTrue(whatIf) : LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference")))
                WriteObject(OperationOutput.Create(ComputerName, "NetAPI32", "ProvisionAccount", "DryRun", new Dictionary<string, object?> { ["Detail"] = "Would provision '" + ComputerName + "' in '" + Domain + "'." }));
            return;
        }
        try
        {
            var request = new OfflineDomainJoinRequest(Domain, ComputerName, MachineAccountOU, DCName, (DomainJoinProvisionOptions)ProvisionOptions);
            var blob = new OfflineDomainJoinManager().CreatePackage(request, Credential?.GetNetworkCredential(), Cancellation);
            WriteObject(OperationOutput.Create(ComputerName, "NetAPI32", "ProvisionAccount", "Completed",
                new Dictionary<string, object?> { ["Detail"] = "Provisioned '" + ComputerName + "' in '" + Domain + "'." }, new Hashtable { ["Blob"] = blob }));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { WriteObject(OperationOutput.Create(ComputerName, "NetAPI32", "ProvisionAccount", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message })); }
    }
}

[Cmdlet(VerbsCommon.New, "DjoinFile", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium), OutputType(typeof(PSObject))]
public sealed class NewDjoinFileCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Blob { get; set; } = "";
    [Parameter(Position = 1)] public FileInfo DestinationFile { get; set; } = new FileInfo(@"C:\temp\djoin.tmp");
    protected override void ProcessRecord()
    {
        var path = DestinationFile.FullName;
        if (!ShouldProcess(path, "Write offline-join blob file"))
        {
            if (MyInvocation.BoundParameters.TryGetValue("WhatIf", out var whatIf) ? LanguagePrimitives.IsTrue(whatIf) : LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference")))
                WriteObject(OperationOutput.Create(path, "DJoin", "WriteBlob", "DryRun", new Dictionary<string, object?> { ["Detail"] = "No file written." }));
            return;
        }
        try
        {
            var bytes = new OfflineDomainJoinManager().WritePackage(Blob, FileSystemPath.Parse(path), overwrite: true, cancellationToken: Cancellation);
            WriteObject(OperationOutput.Create(path, "DJoin", "WriteBlob", "Completed", new Dictionary<string, object?> { ["Detail"] = "Wrote " + bytes + " bytes." }));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { WriteObject(OperationOutput.Create(path, "DJoin", "WriteBlob", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message })); }
    }
}
