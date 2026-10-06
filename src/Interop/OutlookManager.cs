using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Interop;

// Late binding avoids requiring an installed Office interop assembly. Calls stay on the caller's apartment.
internal static class OutlookDispatch
{
    internal static object? Get(object target, string name) =>
        Invoke(
            target,
            name,
            BindingFlags.GetProperty | (Marshal.IsComObject(target) ? 0 : BindingFlags.GetField),
            Array.Empty<object?>()
        );

    internal static object? Call(object target, string name, params object?[] arguments) =>
        Invoke(
            target,
            name,
            BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding,
            arguments
        );

    private static object? Invoke(
        object target,
        string name,
        BindingFlags flags,
        object?[] arguments
    )
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        try
        {
            return target
                .GetType()
                .InvokeMember(
                    name,
                    flags | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                    null,
                    target,
                    arguments,
                    CultureInfo.InvariantCulture
                );
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();

            throw;
        }
    }

    internal static object Require(object? value) =>
        value ?? throw new InvalidOperationException("Outlook returned an empty object reference.");

    internal static string Text(object target, string name) =>
        Convert.ToString(Get(target, name), CultureInfo.InvariantCulture) ?? "";

    internal static int Count(object collection)
    {
        var value = Get(collection, "Count");

        if (!(value is int count) || count < 0 || count > 100000)
            throw new InvalidOperationException("Outlook returned an invalid collection count.");

        return count;
    }

    internal static bool Equal(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal static void Release(object? value)
    {
        // Cleanup must not hide the original provider failure. Never final-release shared RCWs.
        try
        {
            new ComManager().ReleaseReference(value);
        }
        catch (Exception) { }
    }
}

/// <summary>Owns acquired application/namespace references. Dispose on the creating thread; never quits Outlook or logs off a shared session.</summary>
public sealed class OutlookSession : IDisposable
{
    private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
    public object? Application { get; private set; }
    public object? Namespace { get; private set; }

    internal OutlookSession(object application, object session)
    {
        Application = application;
        Namespace = session;
    }

    public void Dispose()
    {
        if (Thread.CurrentThread.ManagedThreadId != threadId)
            throw new InvalidOperationException(
                "Release Outlook references on their acquiring thread."
            );

        OutlookDispatch.Release(Namespace);
        Namespace = null;
        OutlookDispatch.Release(Application);
        Application = null;
    }
}

/// <summary>Classic Outlook automation. COM calls remain synchronous and apartment-bound; cancellation is checked between provider calls.</summary>
public sealed partial class OutlookManager
{
    public OutlookSession Connect(
        bool startIfNotRunning = true,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Classic Outlook requires Windows.");

        object? application = null;
        object? session = null;

        try
        {
            Marshal.ThrowExceptionForHR(CLSIDFromProgID("Outlook.Application", out var classId));
            var result = GetActiveObject(ref classId, IntPtr.Zero, out application);

            if (result < 0)
            {
                // Only an absent running object permits activation; access/provider failures remain failures.
                if (!startIfNotRunning || result != unchecked((int)0x800401e3))
                    Marshal.ThrowExceptionForHR(result);

                application = Activator.CreateInstance(Type.GetTypeFromCLSID(classId, true)!);
            }

            cancellationToken.ThrowIfCancellationRequested();
            session = OutlookDispatch.Require(
                OutlookDispatch.Call(OutlookDispatch.Require(application), "GetNamespace", "MAPI")
            );
            OutlookDispatch.Call(session, "Logon", null, null, false, false);
            cancellationToken.ThrowIfCancellationRequested();
            var owned = new OutlookSession(application!, session);
            application = null;
            session = null;

            return owned;
        }
        finally
        {
            OutlookDispatch.Release(session);
            OutlookDispatch.Release(application);
        }
    }

    /// <returns>A caller-owned root reference. The namespace is borrowed.</returns>
    public object GetStoreRoot(
        object session,
        string? name = null,
        Action<string>? storeObserved = null,
        CancellationToken cancellationToken = default
    )
    {
        var stores = OutlookDispatch.Require(OutlookDispatch.Get(session, "Stores"));

        try
        {
            var count = OutlookDispatch.Count(stores);

            for (var index = 1; index <= count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var store = OutlookDispatch.Require(OutlookDispatch.Call(stores, "Item", index));

                try
                {
                    var displayName = OutlookDispatch.Text(store, "DisplayName");
                    storeObserved?.Invoke(displayName);

                    if (
                        string.IsNullOrWhiteSpace(name)
                            && (bool)
                                OutlookDispatch.Require(OutlookDispatch.Get(store, "IsDefault"))
                        || OutlookDispatch.Equal(displayName, name)
                    )
                        return OutlookDispatch.Require(
                            OutlookDispatch.Call(store, "GetRootFolder")
                        );
                }
                finally
                {
                    OutlookDispatch.Release(store);
                }
            }
        }
        finally
        {
            OutlookDispatch.Release(stores);
        }

        throw new InvalidOperationException(
            "Outlook store '" + name + "' not found. Run with -Verbose to list available stores."
        );
    }

    /// <summary>Explicitly attaches or creates a Unicode PST. Use OpenPstStore when creation must be forbidden.</summary>
    public object AddStoreRoot(
        object session,
        FileSystemPath path,
        CancellationToken cancellationToken = default
    )
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        cancellationToken.ThrowIfCancellationRequested();
        OutlookDispatch.Call(session, "AddStoreEx", path.Value, 2);
        var stores = OutlookDispatch.Require(OutlookDispatch.Get(session, "Stores"));

        try
        {
            var count = OutlookDispatch.Count(stores);

            for (var index = 1; index <= count; index++)
            {
                var store = OutlookDispatch.Require(OutlookDispatch.Call(stores, "Item", index));

                try
                {
                    if (OutlookDispatch.Equal(OutlookDispatch.Text(store, "FilePath"), path.Value))
                        return OutlookDispatch.Require(
                            OutlookDispatch.Call(store, "GetRootFolder")
                        );
                }
                finally
                {
                    OutlookDispatch.Release(store);
                }
            }
        }
        finally
        {
            OutlookDispatch.Release(stores);
        }

        throw new InvalidOperationException(
            "Outlook store was added but could not be located by path: " + path.Value
        );
    }

    /// <returns>A caller-owned child reference, or null when absent and creation was not requested.</returns>
    public object? GetSubFolder(
        object parentFolder,
        string name,
        bool create = false,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A folder name is required.", nameof(name));

        var folders = OutlookDispatch.Require(OutlookDispatch.Get(parentFolder, "Folders"));

        try
        {
            var count = OutlookDispatch.Count(folders);

            for (var index = 1; index <= count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object? folder = OutlookDispatch.Require(
                    OutlookDispatch.Call(folders, "Item", index)
                );

                try
                {
                    if (OutlookDispatch.Equal(OutlookDispatch.Text(folder, "Name"), name))
                    {
                        var owned = folder;
                        folder = null;

                        return owned;
                    }
                }
                finally
                {
                    OutlookDispatch.Release(folder);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return create
                ? OutlookDispatch.Require(OutlookDispatch.Call(folders, "Add", name))
                : null;
        }
        finally
        {
            OutlookDispatch.Release(folders);
        }
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CLSIDFromProgID(string programId, out Guid classId);

    [DllImport("oleaut32.dll", ExactSpelling = true)]
    private static extern int GetActiveObject(
        ref Guid classId,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object? value
    );
}
