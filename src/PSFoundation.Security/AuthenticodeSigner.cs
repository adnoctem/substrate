using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;

namespace PSFoundation.Security;

/// <summary>Explicit Windows Authenticode signing with SHA-256, a borrowed signing certificate and no signing wizard.</summary>
public sealed class AuthenticodeSigner
{
    /// <summary>Signs one file with the supplied private-key certificate and returns verification evidence for the resulting signature.</summary>
    /// <remarks>Signs one existing file. Timestamping uses the Authenticode timestamp protocol; no trust stores are modified.
    /// The certificate and its private key remain caller-owned. Native signing cannot be interrupted once started.</remarks>
    public AuthenticodeVerification Sign(FileSystemPath path, X509Certificate2 certificate, Uri? timestampServer = null,
        SignatureNetworkAccess verificationNetworkAccess = SignatureNetworkAccess.Online, CancellationToken cancellationToken = default)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));
        if (!Enum.IsDefined(typeof(SignatureNetworkAccess), verificationNetworkAccess))
            throw new ArgumentOutOfRangeException(nameof(verificationNetworkAccess));
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("Signing requires a certificate with a private key.", nameof(certificate));
        if (timestampServer != null && (!timestampServer.IsAbsoluteUri || (timestampServer.Scheme != "http" && timestampServer.Scheme != "https") || !string.IsNullOrEmpty(timestampServer.UserInfo)))
            throw new ArgumentException("An HTTP(S) timestamp URL without credentials is required.", nameof(timestampServer));
        if (!File.Exists(path.Value))
            throw new FileNotFoundException("The signing target does not exist.", path.Value);
        cancellationToken.ThrowIfCancellationRequested();
        var extended = new ExtendedInfo { Size = (uint)Marshal.SizeOf(typeof(ExtendedInfo)), HashAlgorithm = "2.16.840.1.101.3.4.2.1" };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ExtendedInfo)));
        var initialized = false;
        try
        {
            Marshal.StructureToPtr(extended, pointer, false);
            initialized = true;
            var info = new SignInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(SignInfo)),
                SubjectChoice = 1,
                FileName = path.Value,
                CertificateChoice = 1,
                Certificate = certificate.Handle,
                TimestampUrl = timestampServer?.AbsoluteUri,
                AdditionalCertificates = 1,
                Extended = pointer
            };
            if (!CryptUIWizDigitalSign(1, IntPtr.Zero, null, ref info, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            GC.KeepAlive(certificate);
        }
        finally { if (initialized) Marshal.DestroyStructure(pointer, typeof(ExtendedInfo)); Marshal.FreeHGlobal(pointer); }
        // Always report the result of a completed signing operation, even if cancellation arrives during the native call.
        return new AuthenticodeVerifier().Verify(path, verificationNetworkAccess);
    }
    /// <summary>Offloads signing and verification; the borrowed certificate must remain alive until the task completes.</summary>
    /// <remarks>Cancellation before signing prevents the operation. Once native signing starts, its result is verified and returned even if cancellation arrives.</remarks>
    public Task<AuthenticodeVerification> SignAsync(FileSystemPath path, X509Certificate2 certificate, Uri? timestampServer = null,
        SignatureNetworkAccess verificationNetworkAccess = SignatureNetworkAccess.Online, CancellationToken cancellationToken = default)
        => Task.Run(() => Sign(path, certificate, timestampServer, verificationNetworkAccess, cancellationToken), cancellationToken);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SignInfo
    {
        internal uint Size, SubjectChoice; [MarshalAs(UnmanagedType.LPWStr)] internal string FileName;
        internal uint CertificateChoice; internal IntPtr Certificate; [MarshalAs(UnmanagedType.LPWStr)] internal string? TimestampUrl;
        internal uint AdditionalCertificates; internal IntPtr Extended;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ExtendedInfo
    {
        internal uint Size, Flags; internal IntPtr Description, MoreInfo;
        [MarshalAs(UnmanagedType.LPStr)] internal string HashAlgorithm;
        internal IntPtr DisplayName, AdditionalStore, Authenticated, Unauthenticated;
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("cryptui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUIWizDigitalSign(uint flags, IntPtr window, string? title, ref SignInfo info, IntPtr context);
}
