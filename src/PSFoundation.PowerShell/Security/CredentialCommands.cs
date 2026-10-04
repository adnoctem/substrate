using System.Management.Automation;
using PSFoundation.IO;
using PSFoundation.Security;

namespace PSFoundation.PowerShell.Security;

[Cmdlet(VerbsCommon.Get, "EncryptedCredentialFile"), OutputType(typeof(PSCredential))]
public sealed class GetEncryptedCredentialFileCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)] public string Path { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1)] public string KeyPath { get; set; } = "";
    protected override void ProcessRecord()
    {
        using var credential = new CredentialFileManager().Read(FileSystemPath.Parse(SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path)), FileSystemPath.Parse(SessionState.Path.GetUnresolvedProviderPathFromPSPath(KeyPath)));
        WriteObject(new PSCredential(credential.UserName, credential.GetPassword()));
    }
}
