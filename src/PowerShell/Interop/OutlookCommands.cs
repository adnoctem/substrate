using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using AdNoctem.Substrate.Interop;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.PowerShell.Windows;

namespace AdNoctem.Substrate.PowerShell.Interop;

public abstract class OutlookCommand : PSCmdlet, IDisposable
{
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();
    private protected readonly OutlookManager Manager = new OutlookManager();
    private protected CancellationToken Cancellation => stopping.Token;

    private protected static object Unwrap(object value) =>
        value is PSObject wrapper ? wrapper.BaseObject : value;

    private protected FileSystemPath PathValue(string path)
    {
        var resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(
            path,
            out var provider,
            out _
        );

        if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A filesystem path is required.", nameof(path));

        return FileSystemPath.Parse(resolved);
    }

    private protected static PSObject Tool(OutlookRepairToolInfo info) =>
        SystemOutput.Object(
            "Name",
            info.Name,
            "Path",
            info.Path,
            "InstallationPath",
            info.InstallationPath,
            "FileVersion",
            info.FileVersion,
            "SupportsFileArgument",
            info.SupportsFileArgument
        );

    protected override void StopProcessing()
    {
        try
        {
            stopping.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    protected override void EndProcessing() => Dispose();

    public void Dispose() => stopping.Dispose();
}

[Cmdlet(VerbsCommon.Get, "OutlookInstallation"), OutputType(typeof(PSObject[]))]
public sealed class GetOutlookInstallationCommand : OutlookCommand
{
    protected override void ProcessRecord()
    {
        foreach (var value in Manager.GetInstallations(Cancellation))
            WriteObject(
                SystemOutput.Object(
                    "Path",
                    value.Path,
                    "OutlookPath",
                    value.OutlookPath,
                    "ScanPstPath",
                    value.ScanPstPath
                )
            );
    }
}

[Cmdlet(VerbsCommon.Get, "OutlookRepairToolInfo"), OutputType(typeof(PSObject))]
public sealed class GetOutlookRepairToolInfoCommand : OutlookCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public string LiteralPath { get; set; } = "";

    protected override void ProcessRecord() =>
        WriteObject(Tool(Manager.GetRepairToolInfo(PathValue(LiteralPath))));
}

[Cmdlet(VerbsCommon.Find, "OutlookRepairTool"), OutputType(typeof(PSObject[]))]
public sealed class FindOutlookRepairToolCommand : OutlookCommand
{
    [Parameter(Position = 0), ValidateSet("ScanPST")]
    public string Name { get; set; } = "ScanPST";

    protected override void ProcessRecord()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var info in Manager.FindRepairTools(cancellationToken: Cancellation))
            if (seen.Add(info.Path))
                WriteObject(Tool(info));

        foreach (
            var command in InvokeCommand.GetCommands(Name + ".exe", CommandTypes.Application, false)
        )
        {
            Cancellation.ThrowIfCancellationRequested();
            var info = Manager.GetRepairToolInfo(PathValue(command.Source));

            if (seen.Add(info.Path))
                WriteObject(Tool(info));
        }
    }
}

[Cmdlet(VerbsCommunications.Connect, "Outlook"), OutputType(typeof(PSObject))]
public sealed class ConnectOutlookCommand : OutlookCommand
{
    protected override void ProcessRecord()
    {
        var context = Manager.Connect(cancellationToken: Cancellation);

        // Preserve the historical transfer of these two raw references to the PowerShell caller.
        try
        {
            WriteObject(
                SystemOutput.Object("App", context.Application, "Namespace", context.Namespace)
            );
        }
        catch
        {
            context.Dispose();

            throw;
        }
    }
}

[Cmdlet(VerbsCommon.Get, "OutlookStoreRoot")]
public sealed class GetOutlookStoreRootCommand : OutlookCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public object Namespace { get; set; } = null!;

    [Parameter(Position = 1)]
    public string? Name { get; set; }

    protected override void ProcessRecord()
    {
        WriteVerbose("Available Outlook stores:");
        WriteObject(
            Manager.GetStoreRoot(
                Unwrap(Namespace),
                Name,
                value => WriteVerbose("  - " + value),
                Cancellation
            )
        );
    }
}

[Cmdlet(VerbsCommon.Add, "OutlookStoreRoot")]
public sealed class AddOutlookStoreRootCommand : OutlookCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public object Namespace { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1)]
    public string Path { get; set; } = "";

    protected override void ProcessRecord() =>
        WriteObject(Manager.AddStoreRoot(Unwrap(Namespace), PathValue(Path), Cancellation));
}

[Cmdlet(VerbsCommon.Get, "OutlookSubFolder")]
public sealed class GetOutlookSubFolderCommand : OutlookCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public object ParentFolder { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1)]
    public string Name { get; set; } = "";

    [Parameter]
    public SwitchParameter Create { get; set; }

    protected override void ProcessRecord() =>
        WriteObject(Manager.GetSubFolder(Unwrap(ParentFolder), Name, Create, Cancellation));
}

[Cmdlet(VerbsCommon.Get, "OutlookStandardFolderIdentity")]
public sealed class GetOutlookStandardFolderIdentityCommand : OutlookCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public object Namespace { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1)]
    public object StoreRoot { get; set; } = null!;

    protected override void ProcessRecord()
    {
        foreach (
            var value in Manager.GetStandardFolderIdentities(
                Unwrap(Namespace),
                Unwrap(StoreRoot),
                Cancellation,
                identity =>
                {
                    var message =
                        "Standard folder '"
                        + identity.Kind
                        + "' in store '"
                        + identity.StoreId
                        + "': "
                        + identity.State
                        + ". "
                        + identity.Evidence;
                    if (identity.State == OutlookFolderIdentityState.Unresolved)
                        WriteWarning(message);
                    else
                        WriteVerbose(message);
                }
            )
        )
            WriteObject(
                SystemOutput.Object(
                    "Kind",
                    value.Kind.ToString(),
                    "StoreID",
                    value.StoreId,
                    "EntryID",
                    value.EntryId,
                    "State",
                    value.State.ToString(),
                    "Evidence",
                    value.Evidence
                )
            );
    }
}

[Cmdlet(VerbsCommon.Get, "OutlookFolderPlan")]
public sealed class GetOutlookFolderPlanCommand : OutlookCommand
{
    [Parameter(Mandatory = true, Position = 0)]
    public object Namespace { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1)]
    public object StoreRoot { get; set; } = null!;

    [Parameter(Position = 2), AllowEmptyString]
    public string FolderName { get; set; } = "Inbox";

    [Parameter]
    public SwitchParameter Recurse { get; set; }

    [
        Parameter(Position = 3),
        ValidateSet(
            "DeletedItems",
            "Outbox",
            "SentItems",
            "Inbox",
            "Calendar",
            "Contacts",
            "Journal",
            "Notes",
            "Tasks",
            "Drafts",
            "AllPublicFolders",
            "Conflicts",
            "SyncIssues",
            "LocalFailures",
            "ServerFailures",
            "Junk",
            "RssFeeds",
            "ToDo",
            "ManagedEmail",
            "SuggestedContacts"
        )
    ]
    public string[] Include { get; set; } = new[] { "Inbox" };

    [Parameter(Position = 4)]
    public string[] Exclusions { get; set; } = Array.Empty<string>();

    [Parameter(Position = 5)]
    public int ProgressId { get; set; }

    protected override void ProcessRecord()
    {
        var result = Manager.GetFolderPlan(
            Unwrap(Namespace),
            Unwrap(StoreRoot),
            MyInvocation.BoundParameters.ContainsKey("FolderName") ? FolderName : null,
            Recurse,
            (Include ?? Array.Empty<string>()).Select(kind =>
                (OutlookFolderKind)Enum.Parse(typeof(OutlookFolderKind), kind, true)
            ),
            Exclusions,
            value =>
                WriteProgress(
                    new ProgressRecord(
                        ProgressId,
                        "Outlook folders",
                        "Reading folder identities and applying exclusions"
                    )
                    {
                        CurrentOperation = value,
                    }
                ),
            Cancellation,
            WriteWarning
        );

        foreach (var value in result)
            WriteObject(
                SystemOutput.Object(
                    "EntryID",
                    value.EntryId,
                    "StoreID",
                    value.StoreId,
                    "FolderPath",
                    value.FolderPath,
                    "RelativePath",
                    value.RelativePath,
                    "StandardKind",
                    value.StandardKind?.ToString(),
                    "Process",
                    value.Process,
                    "Traverse",
                    value.Traverse,
                    "Reason",
                    value.Reason
                )
            );
    }
}

[
    Cmdlet(
        VerbsCommon.Open,
        "OutlookPstStore",
        SupportsShouldProcess = true,
        ConfirmImpact = ConfirmImpact.Medium
    ),
    OutputType(typeof(PSObject))
]
public sealed class OpenOutlookPstStoreCommand : OutlookCommand
{
    [Parameter(Mandatory = true, Position = 0), ValidateNotNull]
    public object Namespace { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1), ValidateNotNullOrEmpty]
    public string LiteralPath { get; set; } = "";

    protected override void ProcessRecord()
    {
        var path = PathValue(LiteralPath);
        var context = Manager.OpenPstStore(Unwrap(Namespace), path, false, Cancellation);

        if (
            context == null
            && ShouldProcess(
                path.ExpandLongName().Value,
                "Attach existing PST to the current Outlook profile"
            )
        )
            context = Manager.OpenPstStore(Unwrap(Namespace), path, true, Cancellation);

        if (context == null)
            return;

        try
        {
            var value = SystemOutput.Object(
                "Path",
                context.Path,
                "StoreId",
                context.StoreId,
                "DisplayName",
                context.DisplayName,
                "Root",
                context.Root,
                "Namespace",
                context.Namespace,
                "AttachedByCall",
                context.AttachedByCall,
                "Closed",
                context.Closed,
                "_State",
                context
            );
            value.TypeNames.Insert(0, "PSFoundation.OutlookPstStoreContext");
            WriteObject(value);
        }
        catch (Exception failure)
        {
            try
            {
                context.Dispose();
            }
            catch (Exception cleanup)
            {
                failure.Data["OutlookPstCleanupError"] = cleanup.Message;
            }

            throw;
        }
    }
}

[Cmdlet(VerbsCommon.Close, "OutlookPstStore")]
public sealed class CloseOutlookPstStoreCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0), ValidateNotNull]
    public object Context { get; set; } = null!;

    protected override void ProcessRecord()
    {
        var wrapper = PSObject.AsPSObject(Context);
        var stateValue = wrapper.Properties["_State"]?.Value;

        if (stateValue is PSObject stateWrapper)
            stateValue = stateWrapper.BaseObject;

        if (
            !wrapper.TypeNames.Contains("PSFoundation.OutlookPstStoreContext")
            || !(stateValue is OutlookPstStore state)
        )
            throw new ArgumentException(
                "Context must be a live object returned by Open-OutlookPstStore."
            );

        try
        {
            state.Dispose();
        }
        finally
        {
            wrapper.Properties["Root"].Value = state.Root;
            wrapper.Properties["Closed"].Value = state.Closed;
        }
    }
}
