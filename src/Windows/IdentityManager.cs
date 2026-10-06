using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace AdNoctem.Substrate.Windows;

/// <summary>Detached Windows identity information without retaining a token handle.</summary>
public sealed class IdentitySnapshot
{
    public string Name { get; }
    public string? SecurityIdentifier { get; }
    public bool IsAdministrator { get; }

    internal IdentitySnapshot(string name, string? sid, bool isAdministrator)
    {
        Name = name;
        SecurityIdentifier = sid;
        IsAdministrator = isAdministrator;
    }
}

/// <summary>Reads the effective Windows identity without prompting, impersonating or elevating.</summary>
public sealed class IdentityManager
{
    public IdentitySnapshot GetCurrentIdentity()
    {
        SystemManager.RequireWindows();

        using (var identity = WindowsIdentity.GetCurrent())
            return new IdentitySnapshot(
                identity.Name,
                identity.User?.Value,
                new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)
            );
    }

    public bool IsElevated() => GetCurrentIdentity().IsAdministrator;

    public string GetSecurityIdentifier(string accountName)
    {
        SystemManager.RequireWindows();

        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("An account name is required.", nameof(accountName));

        return (
            (SecurityIdentifier)new NTAccount(accountName).Translate(typeof(SecurityIdentifier))
        ).Value;
    }

    public string GetAccountName(string securityIdentifier)
    {
        SystemManager.RequireWindows();

        if (string.IsNullOrWhiteSpace(securityIdentifier))
            throw new ArgumentException("A SID is required.", nameof(securityIdentifier));

        return (
            (NTAccount)new SecurityIdentifier(securityIdentifier).Translate(typeof(NTAccount))
        ).Value;
    }

    /// <remarks>Windows account translation cannot be interrupted once started. Cancellation prevents a queued lookup from starting;
    /// an active lookup completes before its task completes. No impersonation token is created or retained.</remarks>
    public Task<string> GetSecurityIdentifierAsync(
        string accountName,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => GetSecurityIdentifier(accountName), cancellationToken);

    public Task<string> GetAccountNameAsync(
        string securityIdentifier,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => GetAccountName(securityIdentifier), cancellationToken);
}
