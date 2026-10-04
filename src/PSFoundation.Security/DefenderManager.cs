using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using PSFoundation.Windows;

namespace PSFoundation.Security;

public enum DefenderExclusionKind { Path, Extension, Process }
public sealed class DefenderThreat
{
    public CimRecord Data { get; }
    public long Id => Convert.ToInt64(Data.GetValue("ThreatID"), CultureInfo.InvariantCulture);
    public string? Name => Data.GetValue("ThreatName") as string;
    public bool IsActive => Data.GetValue("IsActive") is bool active && active;
    public string? DescriptionUrl => string.IsNullOrEmpty(Name) ? null : DefenderManager.GetThreatDescriptionUrl(Name!);
    internal DefenderThreat(CimRecord data) => Data = data;
}
public sealed class DefenderDetection
{
    public CimRecord Data { get; }
    public string? Id => Data.GetValue("DetectionID") as string;
    public long ThreatId => Convert.ToInt64(Data.GetValue("ThreatID"), CultureInfo.InvariantCulture);
    public DateTime? InitialDetectionTime => Data.GetValue("InitialDetectionTime") as DateTime?;
    public string? ProcessName => Data.GetValue("ProcessName") as string;
    public bool ActionSucceeded => Data.GetValue("ActionSuccess") is bool success && success;
    internal DefenderDetection(CimRecord data) => Data = data;
}
public sealed class DefenderExclusions
{
    public IReadOnlyList<string> Paths { get; }
    public IReadOnlyList<string> Extensions { get; }
    public IReadOnlyList<string> Processes { get; }
    internal DefenderExclusions(CimRecord data)
    {
        IReadOnlyList<string> Values(string name) => Array.AsReadOnly((data.GetValue(name) as IEnumerable<object?> ?? Array.Empty<object>()).Cast<string>().ToArray());
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
    public IReadOnlyList<DefenderThreat> GetThreats(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Array.AsReadOnly(Query("MSFT_MpThreat", timeout, cancellationToken).Select(record => new DefenderThreat(record)).ToArray());
    public Task<IReadOnlyList<DefenderThreat>> GetThreatsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetThreats(timeout, cancellationToken), cancellationToken);
    public IReadOnlyList<DefenderDetection> GetDetections(TimeSpan timeout, DateTime? detectedSince = null, CancellationToken cancellationToken = default)
        => Array.AsReadOnly(Query("MSFT_MpThreatDetection", timeout, cancellationToken).Select(record => new DefenderDetection(record))
            .Where(record => !detectedSince.HasValue || record.InitialDetectionTime >= detectedSince.Value).ToArray());
    public Task<IReadOnlyList<DefenderDetection>> GetDetectionsAsync(TimeSpan timeout, DateTime? detectedSince = null, CancellationToken cancellationToken = default)
        => Task.Run(() => GetDetections(timeout, detectedSince, cancellationToken), cancellationToken);
    public CimRecord GetStatus(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Query("MSFT_MpComputerStatus", timeout, cancellationToken).Single();
    public Task<CimRecord> GetStatusAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetStatus(timeout, cancellationToken), cancellationToken);
    public DefenderExclusions GetExclusions(TimeSpan timeout, CancellationToken cancellationToken = default)
        => new DefenderExclusions(Query("MSFT_MpPreference", timeout, cancellationToken).Single());
    public Task<DefenderExclusions> GetExclusionsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => GetExclusions(timeout, cancellationToken), cancellationToken);
    public uint AddExclusions(DefenderExclusionKind kind, IEnumerable<string> values, TimeSpan timeout, CancellationToken cancellationToken = default)
        => ChangeExclusions("Add", kind, values, timeout, cancellationToken);
    public uint RemoveExclusions(DefenderExclusionKind kind, IEnumerable<string> values, TimeSpan timeout, CancellationToken cancellationToken = default)
        => ChangeExclusions("Remove", kind, values, timeout, cancellationToken);
    public Task<uint> AddExclusionsAsync(DefenderExclusionKind kind, IEnumerable<string> values, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => AddExclusions(kind, values, timeout, cancellationToken), cancellationToken);
    public Task<uint> RemoveExclusionsAsync(DefenderExclusionKind kind, IEnumerable<string> values, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Task.Run(() => RemoveExclusions(kind, values, timeout, cancellationToken), cancellationToken);
    public static string GetThreatDescriptionUrl(string threatName)
    {
        if (string.IsNullOrWhiteSpace(threatName))
            throw new ArgumentException("A threat name is required.", nameof(threatName));
        var encoded = Regex.Replace(WebUtility.UrlEncode(threatName), "%[0-9A-F]{2}", match => match.Value.ToLowerInvariant());
        return "https://www.microsoft.com/en-us/wdsi/threats/threat/" + encoded;
    }
    private IReadOnlyList<CimRecord> Query(string className, TimeSpan timeout, CancellationToken cancellationToken)
        => new CimManager(session).Query(Namespace, "SELECT * FROM " + className, timeout, cancellationToken);
    /// <remarks>Returns the native provider code; zero reports success. Provider policies/tamper protection remain authoritative.
    /// Cancellation cannot undo changes already accepted by the provider.</remarks>
    private uint ChangeExclusions(string method, DefenderExclusionKind kind, IEnumerable<string> values, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(DefenderExclusionKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var entries = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
        if (entries.Length == 0 || entries.Any(value => string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0))
            throw new ArgumentException("Supply at least one nonempty exclusion.", nameof(values));
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        var active = session ?? CimSession.Create(null);
        try
        {
            using (var options = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken })
            {
                var parameters = new CimMethodParametersCollection { CimMethodParameter.Create("Exclusion" + kind, entries, CimType.StringArray, CimFlags.In) };
                using (var result = active.InvokeMethod(Namespace, "MSFT_MpPreference", method, parameters, options))
                    return unchecked((uint)Convert.ToInt64(result.ReturnValue.Value, CultureInfo.InvariantCulture));
            }
        }
        finally { if (session == null) active.Dispose(); }
    }
}
