using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace AdNoctem.Substrate.Interop;

// Owns exactly the automation reference returned by activation/property/method access.
// Callers keep each object on its acquiring thread and dispose children before parents.
internal sealed class AutomationObject : IDisposable
{
    internal object Value { get; }
    private bool disposed;

    internal AutomationObject(object value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal static AutomationObject Create(string progId) =>
        new AutomationObject(Activator.CreateInstance(Type.GetTypeFromProgID(progId, true)!)!);

    internal object? Get(string name, params object?[] arguments) =>
        Invoke(name, BindingFlags.GetProperty, arguments);

    internal object? Call(string name, params object?[] arguments) =>
        Invoke(name, BindingFlags.InvokeMethod, arguments);

    internal void Set(string name, object? value) =>
        Invoke(name, BindingFlags.SetProperty, new[] { value });

    internal AutomationObject Child(string name, params object?[] arguments) =>
        new AutomationObject(Get(name, arguments)!);

    internal AutomationObject CallObject(string name, params object?[] arguments) =>
        new AutomationObject(Call(name, arguments)!);

    private object? Invoke(string name, BindingFlags flags, object?[] arguments)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(AutomationObject));

        try
        {
            return Value
                .GetType()
                .InvokeMember(
                    name,
                    flags
                        | BindingFlags.Public
                        | BindingFlags.Instance
                        | BindingFlags.OptionalParamBinding,
                    null,
                    Value,
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

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        if (Marshal.IsComObject(Value))
            Marshal.ReleaseComObject(Value);
    }
}
