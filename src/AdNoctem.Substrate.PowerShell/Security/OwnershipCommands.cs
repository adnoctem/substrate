using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Security.Principal;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.PowerShell.Infrastructure;
using AdNoctem.Substrate.PowerShell.Windows;
using AdNoctem.Substrate.Registry;
using AdNoctem.Substrate.Security;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.PowerShell.Security;

[Cmdlet(VerbsCommon.Set, "RegistryOwner", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High), OutputType(typeof(PSObject))]
public sealed class SetRegistryOwnerCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Hive { get; set; } = "";
    [Parameter(Mandatory = true, Position = 1)] public string Key { get; set; } = "";
    [Parameter] public SecurityIdentifier? SID { get; set; }
    [Parameter] public SwitchParameter Recurse { get; set; } = true;
    protected override void ProcessRecord()
    {
        if (!new IdentityManager().IsElevated())
            throw new InvalidOperationException("Set-RegistryOwner requires an elevated process (administrator token).");
        using var identity = WindowsIdentity.GetCurrent();
        var owner = SID ?? identity.User ?? throw new InvalidOperationException("The current identity has no SID.");
        var path = RegistryPath.Parse(Hive + "\\" + Key);
        var label = Hive + "\\" + Key;
        if (!ShouldProcess(label, "Take ownership and grant FullControl"))
        {
            if (LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference")))
                WriteObject(OperationOutput.Create(label, "Registry", "TakeOwnership", "DryRun", new Dictionary<string, object?> { ["Detail"] = "No changes applied." }));
            return;
        }
        new OwnershipManager().SetRegistryOwner(path, owner, recurse: Recurse, cancellationToken: Cancellation);
        WriteObject(OperationOutput.Create(label, "Registry", "TakeOwnership", "Completed", new Dictionary<string, object?> { ["Detail"] = "Ownership granted to " + owner.Value + "." }, new Hashtable { ["SID"] = owner.Value }));
    }
}
[Cmdlet(VerbsCommon.Set, "ItemOwner", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High), OutputType(typeof(PSObject))]
public sealed class SetItemOwnerCommand : SystemCommand
{
    [Parameter(Mandatory = true, Position = 0)] public string Path { get; set; } = "";
    [Parameter] public string Group { get; set; } = "administrators";
    protected override void ProcessRecord()
    {
        if (!new IdentityManager().IsElevated())
            throw new InvalidOperationException("Set-ItemOwner requires an elevated process (administrator token).");
        var resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        if (!File.Exists(resolved) && !Directory.Exists(resolved))
        { WriteObject(OperationOutput.Create(Path, "FileSystem", "TakeOwnership", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = "Path does not exist." })); return; }
        if (!ShouldProcess(Path, "Take ownership"))
        {
            if (LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference")))
                WriteObject(OperationOutput.Create(Path, "FileSystem", "TakeOwnership", "DryRun", new Dictionary<string, object?> { ["Detail"] = "No changes applied." }));
            return;
        }
        try
        {
            var owner = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var group = string.Equals(Group, "administrators", StringComparison.OrdinalIgnoreCase) ? owner : (SecurityIdentifier)new NTAccount(Group).Translate(typeof(SecurityIdentifier));
            new OwnershipManager().SetFileOwner(FileSystemPath.Parse(resolved), owner, group, true, cancellationToken: Cancellation);
            WriteObject(OperationOutput.Create(Path, "FileSystem", "TakeOwnership", "Completed", new Dictionary<string, object?> { ["Detail"] = "FullControl granted to '" + Group + "'." }));
        }
        catch (Exception error) when (!(error is OperationCanceledException))
        { WriteObject(OperationOutput.Create(Path, "FileSystem", "TakeOwnership", "Failed", new Dictionary<string, object?> { ["ErrorMessage"] = error.Message })); }
    }
}
