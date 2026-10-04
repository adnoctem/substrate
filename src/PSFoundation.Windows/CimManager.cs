using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace PSFoundation.Windows;

/// <summary>A detached CIM instance. Embedded instances and arrays are recursively copied into read-only values.</summary>
public sealed class CimRecord
{
    public string ClassName { get; }
    public string Namespace { get; }
    public string? ServerName { get; }
    public IReadOnlyDictionary<string, object?> Properties { get; }
    private CimRecord(CimInstance instance, Dictionary<string, object?> properties)
    {
        ClassName = instance.CimSystemProperties.ClassName;
        Namespace = instance.CimSystemProperties.Namespace;
        ServerName = instance.CimSystemProperties.ServerName;
        Properties = new ReadOnlyDictionary<string, object?>(properties);
    }
    public object? GetValue(string name) => Properties.TryGetValue(name, out var value) ? value : null;
    /// <summary>Copies the caller-owned instance. Does not retain its native handle.</summary>
    public static CimRecord Capture(CimInstance instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in instance.CimInstanceProperties)
        {
            var value = property.Value;
            try
            { values.Add(property.Name, Freeze(value)); }
            finally
            {
                if (value is CimInstance embedded)
                    embedded.Dispose();
                else if (value is Array array)
                    foreach (var item in array)
                        if (item is CimInstance child)
                            child.Dispose();
            }
        }
        return new CimRecord(instance, values);
    }
    private static object? Freeze(object? value)
    {
        if (value is CimInstance instance)
            return Capture(instance);
        if (value is Array array)
            return Array.AsReadOnly(array.Cast<object?>().Select(Freeze).ToArray());
        return value;
    }
}

/// <summary>Native CIM queries with detached output. A supplied session is borrowed; otherwise each operation owns a local session.</summary>
public sealed class CimManager
{
    private readonly CimSession? session;
    public CimManager(CimSession? session = null) => this.session = session;
    /// <remarks>WQL is an explicit caller-supplied query, not a shell command. Avoid concatenating untrusted values into query text.</remarks>
    public IReadOnlyList<CimRecord> Query(string namespaceName, string wql, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            throw new ArgumentException("A CIM namespace is required.", nameof(namespaceName));
        if (string.IsNullOrWhiteSpace(wql))
            throw new ArgumentException("A WQL query is required.", nameof(wql));
        ServiceManager.ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<CimRecord>();
        var active = session ?? CimSession.Create(null);
        try
        {
            using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
                foreach (var instance in active.QueryInstances(namespaceName, "WQL", wql, options))
                    using (instance)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        result.Add(CimRecord.Capture(instance));
                    }
        }
        finally { if (session == null) active.Dispose(); }
        return result.AsReadOnly();
    }
    /// <remarks>Cancellation is forwarded to CIM. The provider controls when an in-flight operation observes cancellation.</remarks>
    public Task<IReadOnlyList<CimRecord>> QueryAsync(string namespaceName, string wql, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => Query(namespaceName, wql, timeout, cancellationToken), cancellationToken);
}
