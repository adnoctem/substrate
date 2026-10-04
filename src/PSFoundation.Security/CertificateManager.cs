using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace PSFoundation.Security;

/// <summary>Read-only native certificate-store inventory. Returned certificates contain public data only.</summary>
public sealed class CertificateManager
{
    /// <remarks>Remote access uses Windows certificate-store RPC/Remote Registry. Remote CurrentUser selects a loaded user hive by SID;
    /// it does not log on interactively or load a profile. Credentials are borrowed and used only for outbound authentication.</remarks>
    public IReadOnlyList<CertificateInfo> GetCertificates(StoreLocation location, string storeName, string? computerName = null,
        NetworkCredential? credential = null, string? userSid = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(StoreLocation), location))
            throw new ArgumentOutOfRangeException(nameof(location));
        if (string.IsNullOrWhiteSpace(storeName) || storeName.IndexOfAny(new[] { '\\', '/', '\0' }) >= 0)
            throw new ArgumentException("An exact store name is required.", nameof(storeName));
        cancellationToken.ThrowIfCancellationRequested();
        var local = string.IsNullOrEmpty(computerName) || computerName == "." || computerName == "127.0.0.1" || computerName == "::1"
            || string.Equals(computerName, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(computerName!.TrimEnd('.'), Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        var result = new List<CertificateInfo>();
        if (local)
        {
            using (var store = new X509Store(storeName, location))
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var certificates = store.Certificates;
                try
                { foreach (var certificate in certificates) { cancellationToken.ThrowIfCancellationRequested(); result.Add(new CertificateInfo(certificate)); } }
                finally { foreach (var certificate in certificates) certificate.Dispose(); }
            }
        }
        else
        {
            if (computerName!.IndexOfAny(new[] { '\\', '/', '\0' }) >= 0)
                throw new ArgumentException("An exact computer name is required.", nameof(computerName));
            if (location == StoreLocation.CurrentUser && userSid == null)
            {
                if (credential == null)
                { using (var identity = WindowsIdentity.GetCurrent()) userSid = identity.User?.Value; }
                else
                    userSid = ((SecurityIdentifier)new NTAccount(string.IsNullOrEmpty(credential.Domain) ? credential.UserName : credential.Domain + "\\" + credential.UserName).Translate(typeof(SecurityIdentifier))).Value;
            }
            if (location == StoreLocation.CurrentUser)
                userSid = new SecurityIdentifier(userSid ?? throw new InvalidOperationException("A user SID is required for a remote user store.")).Value;
            using (var identity = credential == null ? null : new NetworkIdentityScope(credential))
            {
                var name = @"\\" + computerName + "\\" + (location == StoreLocation.CurrentUser ? userSid + "\\" : "") + storeName;
                var flags = (location == StoreLocation.LocalMachine ? 0x20000u : 0x60000u) | 0x4000u | 0x8000u;
                var handle = CertOpenStore(new IntPtr(10), 0, IntPtr.Zero, flags, name);
                if (handle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var context = IntPtr.Zero;
                try
                {
                    while ((context = CertEnumCertificatesInStore(handle, context)) != IntPtr.Zero)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using (var certificate = new X509Certificate2(context))
                            result.Add(new CertificateInfo(certificate));
                    }
                    var error = Marshal.GetLastWin32Error();
                    if (error != unchecked((int)0x80092004) && error != 0)
                        throw new Win32Exception(error);
                }
                finally { if (context != IntPtr.Zero) CertFreeCertificateContext(context); CertCloseStore(handle, 0); }
            }
        }
        return result.AsReadOnly();
    }
    /// <remarks>Native store RPC cannot be interrupted in flight; cancellation is checked between certificates.</remarks>
    public Task<IReadOnlyList<CertificateInfo>> GetCertificatesAsync(StoreLocation location, string storeName, string? computerName = null,
        NetworkCredential? credential = null, string? userSid = null, CancellationToken cancellationToken = default)
        => Task.Run(() => GetCertificates(location, storeName, computerName, credential, userSid, cancellationToken), cancellationToken);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CertOpenStore(IntPtr provider, uint encoding, IntPtr cryptProvider, uint flags, string name);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("crypt32.dll", SetLastError = true)] private static extern IntPtr CertEnumCertificatesInStore(IntPtr store, IntPtr previous);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("crypt32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CertFreeCertificateContext(IntPtr certificate);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("crypt32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CertCloseStore(IntPtr store, uint flags);
}

// Kept synchronous: native impersonation is scoped to the acquiring thread, including restoration of an existing identity.
internal sealed class NetworkIdentityScope : IDisposable
{
    private readonly int thread = Thread.CurrentThread.ManagedThreadId;
    private SafeAccessTokenHandle? previous;
    private SafeAccessTokenHandle? active;
    internal NetworkIdentityScope(NetworkCredential credential)
    {
        using var securePassword = credential.SecurePassword;
        var password = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
        try
        {
            if (OpenThreadToken(GetCurrentThread(), 0xC, true, out var current))
                previous = current;
            else
            { var error = Marshal.GetLastWin32Error(); current.Dispose(); if (error != 1008) throw new Win32Exception(error); }
            if (!LogonUser(credential.UserName, string.IsNullOrEmpty(credential.Domain) ? null : credential.Domain, password, 9, 3, out active))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            // LogonUser can return a primary token; ImpersonateLoggedOnUser accepts either token kind.
            if (!ImpersonateLoggedOnUser(active))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch { active?.Dispose(); previous?.Dispose(); throw; }
        finally { Marshal.ZeroFreeGlobalAllocUnicode(password); }
    }
    public void Dispose()
    {
        if (active == null)
            return;
        if (Thread.CurrentThread.ManagedThreadId != thread)
            throw new InvalidOperationException("Identity scopes must be disposed on the acquiring thread.");
        if (!SetThreadToken(IntPtr.Zero, previous?.DangerousGetHandle() ?? IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot restore the thread identity.");
        active.Dispose();
        active = null;
        previous?.Dispose();
        previous = null;
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenThreadToken(IntPtr thread, uint access, [MarshalAs(UnmanagedType.Bool)] bool self, out SafeAccessTokenHandle token);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", EntryPoint = "LogonUserW", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool LogonUser(string name, string? domain, IntPtr password, uint type, uint provider, out SafeAccessTokenHandle token);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetThreadToken(IntPtr thread, IntPtr token);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool ImpersonateLoggedOnUser(SafeAccessTokenHandle token);
}
