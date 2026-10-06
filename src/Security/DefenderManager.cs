using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Windows;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace AdNoctem.Substrate.Security;

public enum DefenderExclusionKind
{
    Path,
    Extension,
    Process,
}

/// <summary>A detached Defender threat observation with its native identifier and current state.</summary>
public sealed class DefenderThreat
{
    public CimRecord Data { get; }
    public long Id => Convert.ToInt64(Data.GetValue("ThreatID"), CultureInfo.InvariantCulture);
    public string? Name => Data.GetValue("ThreatName") as string;
    public bool IsActive => Data.GetValue("IsActive") is bool active && active;
    public string? DescriptionUrl =>
        string.IsNullOrEmpty(Name) ? null : DefenderManager.GetThreatDescriptionUrl(Name!);

    internal DefenderThreat(CimRecord data) => Data = data;
}

/// <summary>A detached Defender detection record, distinct from proof that remediation succeeded.</summary>
public sealed class DefenderDetection
{
    public CimRecord Data { get; }
    public string? Id => Data.GetValue("DetectionID") as string;
    public long ThreatId =>
        Convert.ToInt64(Data.GetValue("ThreatID"), CultureInfo.InvariantCulture);
    public DateTime? InitialDetectionTime => Data.GetValue("InitialDetectionTime") as DateTime?;
    public string? ProcessName => Data.GetValue("ProcessName") as string;
    public bool ActionSucceeded => Data.GetValue("ActionSuccess") is bool success && success;

    internal DefenderDetection(CimRecord data) => Data = data;
}

/// <summary>Observed Defender exclusion lists grouped by their native exclusion kind.</summary>
public sealed class DefenderExclusions
{
    public IReadOnlyList<string> Paths { get; }
    public IReadOnlyList<string> Extensions { get; }
    public IReadOnlyList<string> Processes { get; }

    internal DefenderExclusions(CimRecord data)
    {
        IReadOnlyList<string> Values(string name) =>
            Array.AsReadOnly(
                (data.GetValue(name) as IEnumerable<object?> ?? Array.Empty<object>())
                    .Cast<string>()
                    .ToArray()
            );
        Paths = Values("ExclusionPath");
        Extensions = Values("ExclusionExtension");
        Processes = Values("ExclusionProcess");
    }
}

/// <summary>Microsoft Defender inventory and explicit exclusion changes through its native provider. Does not elevate or change protection modes.</summary>
public sealed class DefenderManager
{
    private const string Namespace = @"root\Microsoft\Windows\Defender";
    private readonly CimSession? session;

    public DefenderManager(CimSession? session = null) => this.session = session;

    /// <summary>Reads Defender threat observations through its CIM provider.</summary>
    public IReadOnlyList<DefenderThreat> GetThreats(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) =>
        Array.AsReadOnly(
            Query("MSFT_MpThreat", timeout, cancellationToken)
                .Select(record => new DefenderThreat(record))
                .ToArray()
        );

    /// <summary>Offloads threat inventory with a per-operation timeout.</summary>
    public Task<IReadOnlyList<DefenderThreat>> GetThreatsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => GetThreats(timeout, cancellationToken), cancellationToken);

    /// <summary>Reads detection records, optionally filtering by initial detection time.</summary>
    public IReadOnlyList<DefenderDetection> GetDetections(
        TimeSpan timeout,
        DateTime? detectedSince = null,
        CancellationToken cancellationToken = default
    ) =>
        Array.AsReadOnly(
            Query("MSFT_MpThreatDetection", timeout, cancellationToken)
                .Select(record => new DefenderDetection(record))
                .Where(record =>
                    !detectedSince.HasValue || record.InitialDetectionTime >= detectedSince.Value
                )
                .ToArray()
        );

    /// <summary>Offloads detection inventory without changing Defender configuration.</summary>
    public Task<IReadOnlyList<DefenderDetection>> GetDetectionsAsync(
        TimeSpan timeout,
        DateTime? detectedSince = null,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(() => GetDetections(timeout, detectedSince, cancellationToken), cancellationToken);

    /// <summary>Returns a detached record of Defender computer status.</summary>
    public CimRecord GetStatus(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        Query("MSFT_MpComputerStatus", timeout, cancellationToken).Single();

    /// <summary>Offloads the Defender status query.</summary>
    public Task<CimRecord> GetStatusAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => GetStatus(timeout, cancellationToken), cancellationToken);

    /// <summary>Reads configured exclusion paths, processes, extensions, and IP addresses.</summary>
    public DefenderExclusions GetExclusions(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => new DefenderExclusions(Query("MSFT_MpPreference", timeout, cancellationToken).Single());

    /// <summary>Offloads exclusion inventory without modifying it.</summary>
    public Task<DefenderExclusions> GetExclusionsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => GetExclusions(timeout, cancellationToken), cancellationToken);

    /// <summary>Adds explicitly supplied exclusions of one kind and returns the native provider status.</summary>
    public uint AddExclusions(
        DefenderExclusionKind kind,
        IEnumerable<string> values,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => ChangeExclusions("Add", kind, values, timeout, cancellationToken);

    /// <summary>Removes explicitly supplied exclusions of one kind and returns the native provider status.</summary>
    public uint RemoveExclusions(
        DefenderExclusionKind kind,
        IEnumerable<string> values,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => ChangeExclusions("Remove", kind, values, timeout, cancellationToken);

    /// <summary>Offloads the explicit exclusion addition; this can reduce protection for matching content.</summary>
    public Task<uint> AddExclusionsAsync(
        DefenderExclusionKind kind,
        IEnumerable<string> values,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => AddExclusions(kind, values, timeout, cancellationToken), cancellationToken);

    /// <summary>Offloads removal of the selected exclusions.</summary>
    public Task<uint> RemoveExclusionsAsync(
        DefenderExclusionKind kind,
        IEnumerable<string> values,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () => RemoveExclusions(kind, values, timeout, cancellationToken),
            cancellationToken
        );

    /// <summary>Builds an escaped Microsoft threat-information URL without opening a browser or contacting the site.</summary>
    public static string GetThreatDescriptionUrl(string threatName)
    {
        if (string.IsNullOrWhiteSpace(threatName))
            throw new ArgumentException("A threat name is required.", nameof(threatName));

        var encoded = Regex.Replace(
            WebUtility.UrlEncode(threatName),
            "%[0-9A-F]{2}",
            match => match.Value.ToLowerInvariant()
        );

        return "https://www.microsoft.com/en-us/wdsi/threats/threat/" + encoded;
    }

    private IReadOnlyList<CimRecord> Query(
        string className,
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) =>
        new CimManager(session).Query(
            Namespace,
            "SELECT * FROM " + className,
            timeout,
            cancellationToken
        );

    /// <remarks>Returns the native provider code; zero reports success. Provider policies/tamper protection remain authoritative.
    /// Cancellation cannot undo changes already accepted by the provider.</remarks>
    private uint ChangeExclusions(
        string method,
        DefenderExclusionKind kind,
        IEnumerable<string> values,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        if (!Enum.IsDefined(typeof(DefenderExclusionKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        var entries = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();

        if (
            entries.Length == 0
            || entries.Any(value => string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0)
        )
            throw new ArgumentException("Supply at least one nonempty exclusion.", nameof(values));

        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        cancellationToken.ThrowIfCancellationRequested();
        var active = session ?? CimSession.Create(null);

        try
        {
            using (
                var options = new CimOperationOptions
                {
                    Timeout = timeout,
                    CancellationToken = cancellationToken,
                }
            )
            {
                var parameters = new CimMethodParametersCollection
                {
                    CimMethodParameter.Create(
                        "Exclusion" + kind,
                        entries,
                        CimType.StringArray,
                        CimFlags.In
                    ),
                };

                using (
                    var result = active.InvokeMethod(
                        Namespace,
                        "MSFT_MpPreference",
                        method,
                        parameters,
                        options
                    )
                )
                    return unchecked(
                        (uint)
                            Convert.ToInt64(result.ReturnValue.Value, CultureInfo.InvariantCulture)
                    );
            }
        }
        finally
        {
            if (session == null)
                active.Dispose();
        }
    }
}
