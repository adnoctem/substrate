using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using AdNoctem.Substrate.PowerShell.Infrastructure;
using AdNoctem.Substrate.PowerShell.Windows;
using AdNoctem.Substrate.Security;

namespace AdNoctem.Substrate.PowerShell.Security;

[Cmdlet(VerbsLifecycle.Enable, "WSLFirewallRule", SupportsShouldProcess = true), OutputType(typeof(PSObject))]
public sealed class EnableWSLFirewallRuleCommand : SystemCommand
{
    [Parameter(Position = 0)] public string DisplayName { get; set; } = "WSL";
    [Parameter(Position = 1)] public string InterfaceAlias { get; set; } = "vEthernet (WSL)";
    protected override void ProcessRecord()
    {
        void Result(string status, string detail) => WriteObject(OperationOutput.Create(DisplayName, "Firewall", "AllowInbound", status, new Dictionary<string, object?> { ["Detail"] = detail }));
        try
        {
            var manager = new FirewallManager();
            var pattern = new WildcardPattern(DisplayName, WildcardOptions.IgnoreCase);
            if (manager.GetRules(Cancellation).Any(rule => pattern.IsMatch(rule.Name)))
            { Result("Skipped", "AlreadyExists"); return; }
            if (!ShouldProcess(DisplayName, "Allow inbound traffic on " + InterfaceAlias))
            { Result("Skipped", "WhatIf"); return; }
            manager.AddInterfaceRule(DisplayName, InterfaceAlias, FirewallDirection.Inbound, FirewallAction.Allow, cancellationToken: Cancellation);
            Result("Completed", InterfaceAlias);
        }
        catch (Exception error) when (!(error is OperationCanceledException)) { Result("Failed", error.Message); }
    }
}
[Cmdlet(VerbsLifecycle.Disable, "JetBrainsFirewallRule", SupportsShouldProcess = true), OutputType(typeof(PSObject[]))]
public sealed class DisableJetBrainsFirewallRuleCommand : SystemCommand
{
    [Parameter(Position = 0)] public string[] Prefix { get; set; } = { "PhpStorm", "IntelliJ", "PyCharm", "RubyMine", "WebStorm", "DataGrip", "GoLand", "Rider" };
    protected override void ProcessRecord()
    {
        void Result(string target, string status, string detail) => WriteObject(OperationOutput.Create(target, "Firewall", "Disable", status, new Dictionary<string, object?> { ["Detail"] = detail }));
        if (LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference")))
        { foreach (var prefix in Prefix) Result(prefix + "*", "Skipped", "WhatIf"); return; }
        try
        {
            var manager = new FirewallManager();
            var patterns = Prefix.Select(prefix => new WildcardPattern(prefix + "*", WildcardOptions.IgnoreCase)).ToArray();
            var rules = manager.GetRules(Cancellation).Where(rule => (rule.Profiles & FirewallProfile.Public) != 0 && patterns.Any(pattern => pattern.IsMatch(rule.Name))).ToArray();
            if (rules.Length == 0)
            { Result("JetBrainsPublicFirewallRules", "Skipped", "NoMatch"); return; }
            foreach (var rule in rules)
            {
                if (!ShouldProcess(rule.Name, "Disable public firewall rule"))
                { Result(rule.Name, "Skipped", "WhatIf"); continue; }
                try
                { manager.SetEnabled(rule.Name, false, FirewallProfile.Public, Cancellation); Result(rule.Name, "Completed", "Public profile rule disabled."); }
                catch (Exception error) when (!(error is OperationCanceledException)) { Result(rule.Name, "Failed", error.Message); }
            }
        }
        catch (Exception error) when (!(error is OperationCanceledException)) { Result("JetBrainsPublicFirewallRules", "Failed", error.Message); }
    }
}
