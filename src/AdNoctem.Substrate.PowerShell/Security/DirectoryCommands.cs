using System;
using System.Management.Automation;
using AdNoctem.Substrate.PowerShell.Windows;
using AdNoctem.Substrate.Security;

namespace AdNoctem.Substrate.PowerShell.Security;

[Cmdlet(VerbsDiagnostic.Test, "ADCredential"), OutputType(typeof(bool))]
public sealed class TestADCredentialCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true), Alias("PSCredential"), ValidateNotNull, Credential] public PSCredential Credential { get; set; } = null!;
    [Parameter(Position = 1)] public string Domain { get; set; } = "";
    protected override void ProcessRecord()
    {
        try
        { WriteObject(new DirectoryManager().ValidateCredentials(Credential.GetNetworkCredential(), Domain)); }
        catch (Exception error) { WriteVerbose("Credential validation failed: " + error.Message); WriteObject(false); }
    }
}
[Cmdlet(VerbsCommon.Get, "ADAccountLockoutSource"), OutputType(typeof(PSObject))]
public sealed class GetADAccountLockoutSourceCommand : SystemCommand
{
    [Parameter(Position = 0)] public string DomainName { get; set; } = Environment.GetEnvironmentVariable("USERDOMAIN") ?? "";
    [Parameter(Position = 1), ValidateNotNullOrEmpty] public string UserName { get; set; } = "*";
    [Parameter(Position = 2)] public DateTime StartTime { get; set; } = DateTime.Now.AddDays(-1);
    [Parameter(Position = 3), Credential] public PSCredential? Credential { get; set; }
    protected override void ProcessRecord()
    {
        var filter = new WildcardPattern(UserName, WildcardOptions.IgnoreCase);
        foreach (var entry in new DirectoryManager().GetAccountLockouts(DomainName, StartTime, Credential?.GetNetworkCredential(), int.MaxValue, Cancellation))
            if (filter.IsMatch(entry.UserName))
                WriteObject(SystemOutput.Object("TimeCreated", entry.TimeCreated, "UserName", entry.UserName, "ClientName", entry.ClientName));
    }
}
[Cmdlet(VerbsCommon.Get, "ADFSMORoleHolder"), OutputType(typeof(PSObject))]
public sealed class GetADFSMORoleHolderCommand : SystemCommand
{
    [Parameter(Position = 0), Alias("RunAs"), Credential] public PSCredential? Credential { get; set; }
    protected override void ProcessRecord()
    {
        var roles = new DirectoryManager().GetRoleHolders(credential: Credential?.GetNetworkCredential(), cancellationToken: Cancellation);
        WriteObject(SystemOutput.Object("SchemaMaster", roles.SchemaMaster, "DomainNamingMaster", roles.DomainNamingMaster, "InfrastructureMaster", roles.InfrastructureMaster, "RIDMaster", roles.RidMaster, "PDCEmulator", roles.PdcEmulator));
    }
}
