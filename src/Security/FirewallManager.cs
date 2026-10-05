using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AdNoctem.Substrate.Interop;

namespace AdNoctem.Substrate.Security;

[Flags] public enum FirewallProfile { Domain = 1, Private = 2, Public = 4, All = int.MaxValue }
public enum FirewallDirection { Inbound = 1, Outbound = 2 }
public enum FirewallAction { Block = 0, Allow = 1 }
/// <summary>A detached observation of a Windows firewall rule and its selected profiles.</summary>
public sealed class FirewallRuleInfo
{
    public string Name { get; }
    public bool Enabled { get; }
    public FirewallProfile Profiles { get; }
    public FirewallDirection Direction { get; }
    public FirewallAction Action { get; }
    public string ApplicationPath { get; }
    internal FirewallRuleInfo(AutomationObject rule)
    {
        Name = Convert.ToString(rule.Get("Name")) ?? "";
        Enabled = Convert.ToBoolean(rule.Get("Enabled"));
        Profiles = (FirewallProfile)Convert.ToInt32(rule.Get("Profiles"));
        Direction = (FirewallDirection)Convert.ToInt32(rule.Get("Direction"));
        Action = (FirewallAction)Convert.ToInt32(rule.Get("Action"));
        ApplicationPath = Convert.ToString(rule.Get("ApplicationName")) ?? "";
    }
}
/// <summary>Windows Firewall automation with explicit rule/profile selection. Does not elevate or enable a firewall profile.</summary>
public sealed class FirewallManager
{
    public IReadOnlyList<FirewallRuleInfo> GetRules(CancellationToken cancellationToken = default)
    {
        var result = new List<FirewallRuleInfo>();
        Visit(rule => result.Add(new FirewallRuleInfo(rule)), cancellationToken);
        return result.AsReadOnly();
    }
    /// <summary>Changes exact-name matches in the selected profiles, returning the number changed. Duplicate display names are all considered.</summary>
    public int SetEnabled(string name, bool enabled, FirewallProfile profiles = FirewallProfile.All, CancellationToken cancellationToken = default)
    {
        Require(name, nameof(name));
        if ((int)profiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(profiles));
        var changed = 0;
        Visit(rule =>
        {
            if (!string.Equals(Convert.ToString(rule.Get("Name")), name, StringComparison.OrdinalIgnoreCase) || (Convert.ToInt32(rule.Get("Profiles")) & (int)profiles) == 0)
                return;
            rule.Set("Enabled", enabled);
            changed++;
        }, cancellationToken);
        return changed;
    }
    public void AddInterfaceRule(string name, string interfaceAlias, FirewallDirection direction, FirewallAction action, FirewallProfile profiles = FirewallProfile.All, CancellationToken cancellationToken = default)
    {
        Require(name, nameof(name));
        Require(interfaceAlias, nameof(interfaceAlias));
        if (!Enum.IsDefined(typeof(FirewallDirection), direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (!Enum.IsDefined(typeof(FirewallAction), action))
            throw new ArgumentOutOfRangeException(nameof(action));
        if ((int)profiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(profiles));
        cancellationToken.ThrowIfCancellationRequested();
        using var policy = AutomationObject.Create("HNetCfg.FwPolicy2");
        using var rules = policy.Child("Rules");
        using var rule = AutomationObject.Create("HNetCfg.FWRule");
        rule.Set("Name", name);
        rule.Set("Direction", (int)direction);
        rule.Set("Action", (int)action);
        rule.Set("Profiles", (int)profiles);
        rule.Set("Protocol", 256);
        rule.Set("Interfaces", new object[] { interfaceAlias });
        rule.Set("Enabled", true);
        cancellationToken.ThrowIfCancellationRequested();
        rules.Call("Add", rule.Value);
    }
    private static void Visit(Action<AutomationObject> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var policy = AutomationObject.Create("HNetCfg.FwPolicy2");
        using var rules = policy.Child("Rules");
        var enumerator = ((IEnumerable)rules.Value).GetEnumerator();
        try
        { while (enumerator.MoveNext()) { token.ThrowIfCancellationRequested(); using var rule = new AutomationObject(enumerator.Current); action(rule); } }
        finally { if (enumerator is IDisposable disposable) disposable.Dispose(); else if (Marshal.IsComObject(enumerator)) Marshal.ReleaseComObject(enumerator); }
    }
    private static void Require(string value, string name)
    { if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0) throw new ArgumentException("A nonempty exact value is required.", name); }
}
