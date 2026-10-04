using System;
using System.Runtime.InteropServices;

namespace PSFoundation.Interop;

public sealed class ComManager
{
    /// <summary>Releases one reference on an object the caller owns. Never final-releases a shared RCW.
    /// Returns null for null/non-COM objects. Call on the owning apartment; failures propagate.</summary>
    public int? ReleaseReference(object? value) => value != null && Marshal.IsComObject(value) ? Marshal.ReleaseComObject(value) : (int?)null;

    /// <summary>Explicit process-wide collection and finalizer wait. May block; never invoked implicitly by another library operation.</summary>
    public void CollectAndWaitForFinalizers() { GC.Collect(); GC.WaitForPendingFinalizers(); }
}
