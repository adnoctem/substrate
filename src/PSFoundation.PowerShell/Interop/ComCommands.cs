using System;
using System.Management.Automation;
using PSFoundation.Interop;

namespace PSFoundation.PowerShell.Interop;

[Cmdlet(VerbsCommon.Remove, "ComObject")]
public sealed class RemoveComObjectCommand : PSCmdlet
{
    [Parameter(Position = 0, ValueFromRemainingArguments = true), AllowNull] public object?[]? InputObject { get; set; }
    protected override void ProcessRecord()
    {
        var manager = new ComManager();
        foreach (var input in InputObject ?? Array.Empty<object>())
        {
            var value = input is PSObject wrapper ? wrapper.BaseObject : input;
            // The legacy teardown helper intentionally ignores release failures.
            try
            { manager.ReleaseReference(value); }
            catch (Exception) { }
        }
    }
}
[Cmdlet(VerbsLifecycle.Invoke, "ComGarbageCollection")]
public sealed class InvokeComGarbageCollectionCommand : PSCmdlet
{
    protected override void ProcessRecord() => new ComManager().CollectAndWaitForFinalizers();
}
