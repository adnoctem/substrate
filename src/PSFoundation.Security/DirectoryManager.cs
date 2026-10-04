using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.DirectoryServices.ActiveDirectory;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PSFoundation.Security;

public sealed class DirectoryRoleHolders
{
    public string SchemaMaster { get; }
    public string DomainNamingMaster { get; }
    public string InfrastructureMaster { get; }
    public string RidMaster { get; }
    public string PdcEmulator { get; }
    internal DirectoryRoleHolders(Domain domain, Forest forest)
    {
        using var schema = forest.SchemaRoleOwner;
        using var naming = forest.NamingRoleOwner;
        using var infrastructure = domain.InfrastructureRoleOwner;
        using var rid = domain.RidRoleOwner;
        using var pdc = domain.PdcRoleOwner;
        SchemaMaster = schema.Name;
        DomainNamingMaster = naming.Name;
        InfrastructureMaster = infrastructure.Name;
        RidMaster = rid.Name;
        PdcEmulator = pdc.Name;
    }
}
public sealed class AccountLockout
{
    public DateTime? TimeCreated { get; }
    public string UserName { get; }
    public string ClientName { get; }
    public string DomainController { get; }
    internal AccountLockout(WindowsEventSnapshot record, string controller)
    { TimeCreated = record.TimeCreated; UserName = record.Payload.GetValue("TargetUserName") ?? ""; ClientName = record.Payload.GetValue("CallerComputerName") ?? ""; DomainController = controller; }
}
/// <summary>Windows directory operations using the caller's identity unless a credential is explicitly supplied.</summary>
/// <remarks>Directory bind calls are synchronous. Cancellation is checked between calls; async methods offload them. Caller credentials are borrowed for the duration of an operation.</remarks>
public sealed class DirectoryManager
{
    public bool ValidateCredentials(NetworkCredential credential, string? domain = null)
    {
        if (credential == null)
            throw new ArgumentNullException(nameof(credential));
        using var password = credential.SecurePassword;
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(password);
        try
        {
            var selectedDomain = string.IsNullOrWhiteSpace(domain) ? credential.Domain : domain;
            var success = LogonUser(credential.UserName, string.IsNullOrEmpty(selectedDomain) ? null : selectedDomain, pointer, 3, 0, out var token);
            using (token)
            {
                if (success)
                    return true;
                var error = Marshal.GetLastWin32Error();
                if (error == 1326 || error == 1330 || error == 1331 || error == 1909 || error == 1793 || error == 1907)
                    return false;
                throw new Win32Exception(error, "Directory credential validation failed.");
            }
        }
        finally { Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
    }
    public DirectoryRoleHolders GetRoleHolders(string? domain = null, NetworkCredential? credential = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var directory = Domain.GetDomain(Context(domain, credential));
        cancellationToken.ThrowIfCancellationRequested();
        using var forest = directory.Forest;
        var roles = new DirectoryRoleHolders(directory, forest);
        cancellationToken.ThrowIfCancellationRequested();
        return roles;
    }
    public Task<DirectoryRoleHolders> GetRoleHoldersAsync(string? domain = null, NetworkCredential? credential = null, CancellationToken cancellationToken = default)
        => Task.Run(() => GetRoleHolders(domain, credential, cancellationToken), cancellationToken);
    public string GetPrimaryDomainController(string? domain = null, NetworkCredential? credential = null)
    { using var directory = Domain.GetDomain(Context(domain, credential)); using var controller = directory.PdcRoleOwner; return controller.Name; }
    public IReadOnlyList<AccountLockout> GetAccountLockouts(string? domain, DateTimeOffset startTime, NetworkCredential? credential = null, int maximumEvents = 10000, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var controller = GetPrimaryDomainController(domain, credential);
        using var password = credential?.SecurePassword;
        using var session = credential == null ? new EventLogSession(controller)
            : new EventLogSession(controller, credential.Domain, credential.UserName, password, SessionAuthentication.Negotiate);
        var manager = new EventLogManager(session);
        var events = manager.Read(new WindowsEventQuery("Security", new[] { 4740 }, startTime: startTime, maximumEvents: maximumEvents), cancellationToken: cancellationToken);
        var records = new List<AccountLockout>();
        foreach (var entry in events)
        { cancellationToken.ThrowIfCancellationRequested(); records.Add(new AccountLockout(entry, controller)); }
        return records.AsReadOnly();
    }
    public Task<IReadOnlyList<AccountLockout>> GetAccountLockoutsAsync(string? domain, DateTimeOffset startTime, NetworkCredential? credential = null, int maximumEvents = 10000, CancellationToken cancellationToken = default)
        => Task.Run(() => GetAccountLockouts(domain, startTime, credential, maximumEvents, cancellationToken), cancellationToken);
    private static DirectoryContext Context(string? domain, NetworkCredential? credential)
    {
        if (credential == null)
            return string.IsNullOrWhiteSpace(domain) ? new DirectoryContext(DirectoryContextType.Domain) : new DirectoryContext(DirectoryContextType.Domain, domain);
        var user = string.IsNullOrEmpty(credential.Domain) ? credential.UserName : credential.Domain + "\\" + credential.UserName;
        return string.IsNullOrWhiteSpace(domain) ? new DirectoryContext(DirectoryContextType.Domain, user, credential.Password)
            : new DirectoryContext(DirectoryContextType.Domain, domain, user, credential.Password);
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "LogonUserW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(string user, string? domain, IntPtr password, int logonType, int provider, out SafeAccessTokenHandle token);
}
