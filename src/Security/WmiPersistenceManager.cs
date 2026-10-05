using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using AdNoctem.Substrate.Windows;

namespace AdNoctem.Substrate.Security;

/// <summary>A WMI subscription class whose inventory failed, together with the underlying error.</summary>
public sealed class WmiPersistenceReadError
{
    public string ClassName { get; }
    public Exception Error { get; }
    internal WmiPersistenceReadError(string className, Exception error) { ClassName = className; Error = error; }
}
/// <summary>Permanent subscription filters, consumers, and bindings; retained errors indicate incomplete evidence.</summary>
public sealed class WmiPersistenceInventory
{
    public IReadOnlyList<CimRecord> EventFilters { get; }
    public IReadOnlyList<CimRecord> Consumers { get; }
    public IReadOnlyList<CimRecord> Bindings { get; }
    public IReadOnlyList<WmiPersistenceReadError> Errors { get; }
    public bool IsComplete => Errors.Count == 0;
    internal WmiPersistenceInventory(IReadOnlyList<CimRecord> filters, IReadOnlyList<CimRecord> consumers, IReadOnlyList<CimRecord> bindings, List<WmiPersistenceReadError> errors)
    { EventFilters = filters; Consumers = consumers; Bindings = bindings; Errors = errors.AsReadOnly(); }
}
/// <summary>Read-only inventory of permanent WMI subscriptions. Presence is evidence of a subscription, not proof of malicious activity.</summary>
public sealed class WmiPersistenceManager
{
    private readonly CimManager manager;
    public WmiPersistenceManager(CimSession? session = null) => manager = new CimManager(session);
    /// <summary>Reads permanent event filters, consumers, and bindings; optional continuation retains class-specific CIM failures.</summary>
    public WmiPersistenceInventory Read(TimeSpan timeout, bool continueOnError = false, CancellationToken cancellationToken = default)
    {
        var errors = new List<WmiPersistenceReadError>();
        IReadOnlyList<CimRecord> Query(string name)
        {
            try
            { return manager.Query(@"root\subscription", "SELECT * FROM " + name, timeout, cancellationToken); }
            catch (CimException error) when (continueOnError) { errors.Add(new WmiPersistenceReadError(name, error)); return Array.Empty<CimRecord>(); }
        }
        var filters = Query("__EventFilter");
        var consumers = Query("__EventConsumer");
        var bindings = Query("__FilterToConsumerBinding");
        return new WmiPersistenceInventory(filters, consumers, bindings, errors);
    }
    /// <summary>Offloads subscription inventory without modifying or deleting persistence entries.</summary>
    public Task<WmiPersistenceInventory> ReadAsync(TimeSpan timeout, bool continueOnError = false, CancellationToken cancellationToken = default)
        => Task.Run(() => Read(timeout, continueOnError, cancellationToken), cancellationToken);
}
