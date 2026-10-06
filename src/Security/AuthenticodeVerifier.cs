using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Security;

public enum SignatureNetworkAccess
{
    Online,
    CacheOnly,
}

public enum AuthenticodeStatus
{
    Valid,
    NotSigned,
    HashMismatch,
    NotTrusted,
    UnknownError,
}

/// <summary>A detached public certificate, without a private key or native handle.</summary>
public sealed class CertificateInfo
{
    private readonly byte[] encoded;
    public string Subject { get; }
    public string Issuer { get; }
    public string Thumbprint { get; }
    public string SerialNumber { get; }
    public DateTime NotBefore { get; }
    public DateTime NotAfter { get; }
    public byte[] EncodedCertificate => (byte[])encoded.Clone();

    public CertificateInfo(X509Certificate2 certificate)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        encoded = certificate.RawData;
        Subject = certificate.Subject;
        Issuer = certificate.Issuer;
        Thumbprint = certificate.Thumbprint;
        SerialNumber = certificate.SerialNumber;
        NotBefore = certificate.NotBefore;
        NotAfter = certificate.NotAfter;
    }

    /// <summary>Creates a certificate owned by the caller, who must dispose it.</summary>
    public X509Certificate2 OpenCertificate() => new X509Certificate2(encoded);
}

/// <summary>Windows trust status and public signer evidence; an acceptable signer must still match the caller's publisher policy.</summary>
public sealed class AuthenticodeVerification
{
    public FileSystemPath Path { get; }
    public uint NativeStatus { get; }
    public AuthenticodeStatus Status { get; }
    public CertificateInfo? Signer { get; }
    public CertificateInfo? TimestampSigner { get; }
    public bool Valid => NativeStatus == 0 && Signer != null;
    public SignatureNetworkAccess NetworkAccess { get; }

    internal AuthenticodeVerification(
        FileSystemPath path,
        uint nativeStatus,
        CertificateInfo? signer,
        CertificateInfo? timestampSigner,
        SignatureNetworkAccess networkAccess
    )
    {
        Path = path;
        NativeStatus = nativeStatus;
        Signer = signer;
        TimestampSigner = timestampSigner;
        NetworkAccess = networkAccess;
        Status =
            nativeStatus == 0 && signer != null ? AuthenticodeStatus.Valid
            : nativeStatus == 0x800B0100 ? AuthenticodeStatus.NotSigned
            : nativeStatus == 0x80096010 ? AuthenticodeStatus.HashMismatch
            : nativeStatus == 0x800B0004 || nativeStatus == 0x800B0111 || nativeStatus == 0x800B0109
                ? AuthenticodeStatus.NotTrusted
            : AuthenticodeStatus.UnknownError;
    }
}

/// <summary>Windows Authenticode verification of embedded signatures, with no UI. Only an exact native success with signer evidence is trusted.</summary>
/// <remarks>Does not search external catalogs. Revocation checking remains enabled in CacheOnly mode; unavailable evidence is a failure,
/// not permission to bypass trust. Windows applies its Authenticode timestamp and trust policy. Verification does not execute the file.
/// A result describes the file at observation time and is not reusable execution authorization.</remarks>
public sealed class AuthenticodeVerifier
{
    /// <summary>Evaluates Windows Authenticode trust under the selected network-access policy and retains signature evidence.</summary>
    public AuthenticodeVerification Verify(
        FileSystemPath path,
        SignatureNetworkAccess networkAccess = SignatureNetworkAccess.Online,
        CancellationToken cancellationToken = default
    )
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        if (!Enum.IsDefined(typeof(SignatureNetworkAccess), networkAccess))
            throw new ArgumentOutOfRangeException(nameof(networkAccess));

        cancellationToken.ThrowIfCancellationRequested();

        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new PlatformNotSupportedException("Authenticode verification requires Windows.");

        // Deny modification/replacement for the lifetime of the verification, including signer extraction.
        using (
            var file = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read)
        )
            return VerifyFile(path, file, networkAccess, cancellationToken);
    }

    /// <summary>Offloads native signature verification; cancellation is observed at supported checkpoints.</summary>
    /// <remarks>Offloads the native call. Cancellation is checked before and after it; Windows trust-provider calls cannot be interrupted.</remarks>
    public Task<AuthenticodeVerification> VerifyAsync(
        FileSystemPath path,
        SignatureNetworkAccess networkAccess = SignatureNetworkAccess.Online,
        CancellationToken cancellationToken = default
    ) => Task.Run(() => Verify(path, networkAccess, cancellationToken), cancellationToken);

    private static AuthenticodeVerification VerifyFile(
        FileSystemPath path,
        FileStream file,
        SignatureNetworkAccess networkAccess,
        CancellationToken cancellationToken
    )
    {
        var name = IntPtr.Zero;
        var fileInfo = IntPtr.Zero;

        try
        {
            name = Marshal.StringToCoTaskMemUni(path.Value);
            var info = new TrustFileInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(TrustFileInfo)),
                Path = name,
                File = file.SafeFileHandle.DangerousGetHandle(),
            };
            fileInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TrustFileInfo)));
            Marshal.StructureToPtr(info, fileInfo, false);
            var data = new TrustData
            {
                Size = (uint)Marshal.SizeOf(typeof(TrustData)),
                UiChoice = 2,
                RevocationChecks = 1,
                UnionChoice = 1,
                FileInfo = fileInfo,
                StateAction = 1,
                ProviderFlags =
                    0x80u
                    | 0x2000u
                    | (networkAccess == SignatureNetworkAccess.CacheOnly ? 0x1000u : 0u),
            };
            var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

            try
            {
                var status = unchecked((uint)WinVerifyTrust(new IntPtr(-1), ref action, ref data));
                cancellationToken.ThrowIfCancellationRequested();
                var provider =
                    data.StateData == IntPtr.Zero
                        ? IntPtr.Zero
                        : WTHelperProvDataFromStateData(data.StateData);

                return new AuthenticodeVerification(
                    path,
                    status,
                    Certificate(provider, false),
                    Certificate(provider, true),
                    networkAccess
                );
            }
            finally
            {
                data.StateAction = 2; // WTD_STATEACTION_CLOSE is required even when verification fails.
                _ = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            }
        }
        finally
        {
            if (fileInfo != IntPtr.Zero)
                Marshal.FreeHGlobal(fileInfo);

            if (name != IntPtr.Zero)
                Marshal.FreeCoTaskMem(name);
        }
    }

    private static CertificateInfo? Certificate(IntPtr provider, bool timestamp)
    {
        if (provider == IntPtr.Zero)
            return null;

        var signer = WTHelperGetProvSignerFromChain(provider, 0, timestamp, 0);

        if (signer == IntPtr.Zero)
            return null;

        var item = WTHelperGetProvCertFromChain(signer, 0);

        if (item == IntPtr.Zero)
            return null;

        // Read the documented structure prefix; subsequent trust-provider fields are not needed.
        var prefix = (ProviderCertificate)
            Marshal.PtrToStructure(item, typeof(ProviderCertificate))!;

        if (prefix.Certificate == IntPtr.Zero)
            return null;

        using (var certificate = new X509Certificate2(prefix.Certificate))
            return new CertificateInfo(certificate);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustFileInfo
    {
        public uint Size;
        public IntPtr Path;
        public IntPtr File;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallback;
        public IntPtr SipClient;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProviderCertificate
    {
        public uint Size;
        public IntPtr Certificate;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvSignerFromChain(
        IntPtr provider,
        uint signerIndex,
        [MarshalAs(UnmanagedType.Bool)] bool counterSigner,
        uint counterSignerIndex
    );

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint certificateIndex);
}
