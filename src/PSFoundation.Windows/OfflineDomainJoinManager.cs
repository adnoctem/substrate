using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using PSFoundation.IO;

namespace PSFoundation.Windows;

[Flags]
public enum DomainJoinProvisionOptions : uint
{
    None = 0, DownlevelPrivilegeSupport = 1, ReuseAccount = 2, UseDefaultPassword = 4, SkipAccountSearch = 8, IncludeRootCaCertificates = 16
}

/// <summary>Explicit offline domain-join provisioning inputs; resulting join data must be treated as sensitive.</summary>
public sealed class OfflineDomainJoinRequest
{
    public string Domain { get; }
    public string ComputerName { get; }
    public string? MachineAccountOu { get; }
    public string? DomainController { get; }
    public DomainJoinProvisionOptions Options { get; }
    public IReadOnlyList<string> CertificateTemplates { get; }
    public IReadOnlyList<string> MachinePolicyNames { get; }
    public IReadOnlyList<FileSystemPath> MachinePolicyPaths { get; }
    /// <remarks>No account reuse or default-password policy is selected implicitly. The native provider validates option combinations.</remarks>
    public OfflineDomainJoinRequest(string domain, string computerName, string? machineAccountOu = null, string? domainController = null,
        DomainJoinProvisionOptions options = DomainJoinProvisionOptions.None, IEnumerable<string>? certificateTemplates = null,
        IEnumerable<string>? machinePolicyNames = null, IEnumerable<FileSystemPath>? machinePolicyPaths = null)
    {
        string Text(string value, string name)
        { if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0) throw new ArgumentException("A nonempty value without NUL characters is required.", name); return value; }
        Domain = Text(domain, nameof(domain));
        ComputerName = Text(computerName, nameof(computerName));
        MachineAccountOu = string.IsNullOrEmpty(machineAccountOu) ? null : Text(machineAccountOu!, nameof(machineAccountOu));
        DomainController = string.IsNullOrEmpty(domainController) ? null : Text(domainController!, nameof(domainController));
        Options = options;
        CertificateTemplates = Array.AsReadOnly((certificateTemplates ?? Array.Empty<string>()).Select(value => Text(value, nameof(certificateTemplates))).ToArray());
        MachinePolicyNames = Array.AsReadOnly((machinePolicyNames ?? Array.Empty<string>()).Select(value => Text(value, nameof(machinePolicyNames))).ToArray());
        var paths = (machinePolicyPaths ?? Array.Empty<FileSystemPath>()).ToArray();
        if (paths.Any(path => path == null))
            throw new ArgumentException("Policy paths cannot contain null.", nameof(machinePolicyPaths));
        MachinePolicyPaths = Array.AsReadOnly(paths);
    }
}

/// <summary>Encodes the UTF-16LE BOM and NUL-terminated text accepted by djoin. The package text contains machine credentials.</summary>
public static class DomainJoinPackageCodec
{
    private static readonly Encoding TextEncoding = new UnicodeEncoding(false, true, true);
    public static byte[] Encode(string packageText)
    {
        if (string.IsNullOrWhiteSpace(packageText) || packageText.IndexOf('\0') >= 0)
            throw new ArgumentException("A nonempty package without embedded NUL characters is required.", nameof(packageText));
        var bytes = new byte[checked(TextEncoding.GetByteCount(packageText) + 4)];
        bytes[0] = 0xff;
        bytes[1] = 0xfe;
        TextEncoding.GetBytes(packageText, 0, packageText.Length, bytes, 2);
        return bytes;
    }
    public static string Decode(byte[] package)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        if (package.Length < 6 || package.Length % 2 != 0 || package[0] != 0xff || package[1] != 0xfe
            || package[package.Length - 1] != 0 || package[package.Length - 2] != 0)
            throw new InvalidDataException("Expected a BOM-prefixed, NUL-terminated UTF-16LE provisioning package.");
        var text = TextEncoding.GetString(package, 2, package.Length - 4);
        if (string.IsNullOrWhiteSpace(text) || text.IndexOf('\0') >= 0)
            throw new InvalidDataException("The provisioning package text is empty or contains embedded terminators.");
        return text;
    }
}

/// <summary>Explicit AD account provisioning and package serialization. Does not join this computer, select credentials, prompt or elevate.</summary>
public sealed class OfflineDomainJoinManager
{
    /// <returns>Credential-bearing package text. The caller must protect its storage and transport; never write it to logs.</returns>
    /// <remarks>The native operation may create or modify an AD account. Cancellation prevents invocation but cannot interrupt an accepted
    /// provisioning request; its result is returned even if cancellation arrives during the call. No rollback is implied.</remarks>
    public string CreatePackage(OfflineDomainJoinRequest request, NetworkCredential? credential = null, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        SystemManager.RequireWindows();
        cancellationToken.ThrowIfCancellationRequested();
        if (credential == null)
            return Provision(request, cancellationToken);
        var user = credential.UserName;
        string? domain = string.IsNullOrEmpty(credential.Domain) ? null : credential.Domain;
        var separator = user.IndexOf('\\');
        if (separator >= 0)
        { domain = user.Substring(0, separator); user = user.Substring(separator + 1); }
        if (string.IsNullOrWhiteSpace(user) || user.IndexOf('\0') >= 0 || (domain != null && domain.IndexOf('\0') >= 0))
            throw new ArgumentException("A valid credential username is required.", nameof(credential));
        using (var password = credential.SecurePassword)
        {
            var pointer = Marshal.SecureStringToGlobalAllocUnicode(password);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!LogonUser(user, domain, pointer, 9, 3, out var token))
                {
                    var error = Marshal.GetLastWin32Error();
                    token?.Dispose();
                    throw new Win32Exception(error, "LogonUser could not create the requested network credential context.");
                }
                using (token)
                    return WindowsIdentity.RunImpersonated(token, () => Provision(request, cancellationToken));
            }
            finally { Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
        }
    }
    /// <remarks>Offloads the synchronous provider operation. The caller must retain an optional credential until completion.</remarks>
    public Task<string> CreatePackageAsync(OfflineDomainJoinRequest request, NetworkCredential? credential = null, CancellationToken cancellationToken = default)
        => Task.Run(() => CreatePackage(request, credential, cancellationToken), cancellationToken);
    /// <summary>Writes one package atomically. The caller selects a secure destination directory and explicit overwrite policy.</summary>
    public int WritePackage(string packageText, FileSystemPath destination, bool overwrite = false, CancellationToken cancellationToken = default)
        => WritePackageAsync(packageText, destination, overwrite, cancellationToken).GetAwaiter().GetResult();
    public async Task<int> WritePackageAsync(string packageText, FileSystemPath destination, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = DomainJoinPackageCodec.Encode(packageText);
        try
        {
            using (var content = new MemoryStream(bytes, false))
                await new FileManager().WriteAtomicallyAsync(destination, content, overwrite, cancellationToken).ConfigureAwait(false);
            return bytes.Length;
        }
        finally { Array.Clear(bytes, 0, bytes.Length); }
    }
    private static string Provision(OfflineDomainJoinRequest request, CancellationToken cancellationToken)
    {
        using (var certificates = new NativeStringArray(request.CertificateTemplates))
        using (var policyNames = new NativeStringArray(request.MachinePolicyNames))
        using (var policyPaths = new NativeStringArray(request.MachinePolicyPaths.Select(path => path.Value)))
        {
            var parameters = new ProvisioningParameters
            {
                Version = 1,
                Domain = request.Domain,
                HostName = request.ComputerName,
                MachineAccountOu = request.MachineAccountOu,
                DomainController = request.DomainController,
                Options = (uint)request.Options,
                CertificateTemplates = certificates.Pointer,
                CertificateTemplateCount = certificates.Count,
                MachinePolicyNames = policyNames.Pointer,
                MachinePolicyNameCount = policyNames.Count,
                MachinePolicyPaths = policyPaths.Pointer,
                MachinePolicyPathCount = policyPaths.Count
            };
            IntPtr package = IntPtr.Zero;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var status = NetCreateProvisioningPackage(parameters, IntPtr.Zero, IntPtr.Zero, out package);
                if (status != 0)
                    throw new Win32Exception(unchecked((int)status), "NetCreateProvisioningPackage failed with error " + status + ".");
                var text = package == IntPtr.Zero ? null : Marshal.PtrToStringUni(package);
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidDataException("The API returned an empty blob.");
                return text!;
            }
            finally { if (package != IntPtr.Zero) NetApiBufferFree(package); }
        }
    }
    private sealed class NativeStringArray : IDisposable
    {
        private readonly List<IntPtr> strings = new List<IntPtr>();
        internal IntPtr Pointer { get; private set; }
        internal uint Count => (uint)strings.Count;
        internal NativeStringArray(IEnumerable<string> values)
        {
            try
            {
                foreach (var value in values)
                    strings.Add(Marshal.StringToHGlobalUni(value));
                if (strings.Count == 0)
                    return;
                Pointer = Marshal.AllocHGlobal(checked(strings.Count * IntPtr.Size));
                Marshal.Copy(strings.ToArray(), 0, Pointer, strings.Count);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            foreach (var value in strings)
                Marshal.FreeHGlobal(value);
            strings.Clear();
            if (Pointer != IntPtr.Zero)
            { Marshal.FreeHGlobal(Pointer); Pointer = IntPtr.Zero; }
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class ProvisioningParameters
    {
        internal uint Version;
        [MarshalAs(UnmanagedType.LPWStr)] internal string Domain = "";
        [MarshalAs(UnmanagedType.LPWStr)] internal string HostName = "";
        [MarshalAs(UnmanagedType.LPWStr)] internal string? MachineAccountOu;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? DomainController;
        internal uint Options;
        internal IntPtr CertificateTemplates;
        internal uint CertificateTemplateCount;
        internal IntPtr MachinePolicyNames;
        internal uint MachinePolicyNameCount;
        internal IntPtr MachinePolicyPaths;
        internal uint MachinePolicyPathCount;
        internal IntPtr NetbiosName = IntPtr.Zero;
        internal IntPtr SiteName = IntPtr.Zero;
        internal IntPtr PrimaryDnsDomain = IntPtr.Zero;
    }
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint NetCreateProvisioningPackage([In] ProvisioningParameters parameters, IntPtr binaryData, IntPtr binarySize, out IntPtr textData);
    [DllImport("netapi32.dll", ExactSpelling = true)] private static extern uint NetApiBufferFree(IntPtr buffer);
    [DllImport("advapi32.dll", EntryPoint = "LogonUserW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(string userName, string? domain, IntPtr password, int logonType, int provider, out SafeAccessTokenHandle token);
}
