using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Security;

/// <summary>Caller-owned credential. Dispose it after use; GetPassword returns a separate caller-owned secure copy.</summary>
public sealed class StoredCredential : IDisposable
{
    private SecureString? password;
    public string UserName { get; }
    internal StoredCredential(string userName, SecureString password) { UserName = userName; this.password = password; }
    /// <summary>Returns an independent secure-string copy that the caller must dispose.</summary>
    /// <summary>Returns a caller-owned copy of the password; dispose it independently of this credential.</summary>
    public SecureString GetPassword() => (password ?? throw new ObjectDisposedException(nameof(StoredCredential))).Copy();
    public void Dispose() { password?.Dispose(); password = null; }
}
/// <summary>Reads and writes the v1 AES credential/key format without a PowerShell runtime.</summary>
/// <remarks>The legacy format is not authenticated encryption. Possession of both files permits decryption.
/// Parents must be caller-controlled. Publishing two files is not transactional: a failure after key replacement requires recreating the pair.</remarks>
public sealed class CredentialFileManager
{
    private const string Header = "76492d1116743f0423413b16050a5345";
    /// <summary>Writes a protected credential and key pair in the legacy format; replacing the pair is not atomic.</summary>
    public void Write(FileSystemPath credentialPath, FileSystemPath keyPath, string userName, SecureString password, bool overwrite = false)
    {
        ValidatePaths(credentialPath, keyPath);
        if (string.IsNullOrWhiteSpace(userName) || userName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            throw new ArgumentException("A single-line username is required.", nameof(userName));
        if (password == null)
            throw new ArgumentNullException(nameof(password));
        if (!overwrite && (File.Exists(credentialPath.Value) || File.Exists(keyPath.Value)))
            throw new IOException("A credential or key file already exists.");
        var key = new byte[32];
        using (var random = RandomNumberGenerator.Create())
            random.GetBytes(key);
        var keyTemporary = FileSystemPath.Parse(Path.Combine(Path.GetDirectoryName(keyPath.Value)!, ".psf-key-" + Guid.NewGuid().ToString("N")));
        var credentialTemporary = FileSystemPath.Parse(Path.Combine(Path.GetDirectoryName(credentialPath.Value)!, ".psf-credential-" + Guid.NewGuid().ToString("N")));
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User ?? throw new InvalidOperationException("The current identity has no SID.");
            var descriptor = FileSecurityDescriptor.FromSddl("O:" + sid.Value + "D:P(A;;FA;;;" + sid.Value + ")");
            var security = new FileSecurityManager();
            using (var stream = security.CreateFile(keyTemporary, descriptor))
            using (var writer = new StreamWriter(stream, Encoding.ASCII))
                writer.WriteLine(Convert.ToBase64String(key));
            using (var stream = security.CreateFile(credentialTemporary, descriptor))
            using (var writer = new StreamWriter(stream, Encoding.ASCII))
            { writer.WriteLine(userName); writer.WriteLine(Encrypt(password, key)); }
            // Restrict an existing destination before Replace, which retains its original DACL.
            Publish(keyTemporary, keyPath, overwrite, security, descriptor);
            Publish(credentialTemporary, credentialPath, overwrite, security, descriptor);
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
            if (File.Exists(keyTemporary.Value))
                File.Delete(keyTemporary.Value);
            if (File.Exists(credentialTemporary.Value))
                File.Delete(credentialTemporary.Value);
        }
    }
    /// <summary>Reads the credential pair and returns a caller-owned credential that must be disposed.</summary>
    public StoredCredential Read(FileSystemPath credentialPath, FileSystemPath keyPath)
    {
        ValidatePaths(credentialPath, keyPath);
        var lines = ReadBounded(credentialPath, 1024 * 1024).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length < 2 || string.IsNullOrEmpty(lines[0]))
            throw new InvalidDataException("Expected a username and encrypted password on separate lines.");
        var key = Convert.FromBase64String(ReadBounded(keyPath, 4096).Trim());
        try
        { return new StoredCredential(lines[0], Decrypt(string.Concat(lines.Skip(1)), key)); }
        finally { Array.Clear(key, 0, key.Length); }
    }
    /// <summary>Encodes a password using the supplied AES key and a fresh IV in the legacy unauthenticated envelope.</summary>
    public string Encrypt(SecureString password, byte[] key)
    {
        if (password == null)
            throw new ArgumentNullException(nameof(password));
        ValidateKey(key);
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(password);
        var plain = new byte[checked(password.Length * 2)];
        try
        {
            Marshal.Copy(pointer, plain, 0, plain.Length);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            using var transform = aes.CreateEncryptor();
            var encrypted = transform.TransformFinalBlock(plain, 0, plain.Length);
            var hex = BitConverter.ToString(encrypted).Replace("-", "").ToLowerInvariant();
            return Header + Convert.ToBase64String(Encoding.Unicode.GetBytes("2|" + Convert.ToBase64String(aes.IV) + "|" + hex));
        }
        finally { Array.Clear(plain, 0, plain.Length); Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
    }
    /// <summary>Returns a caller-owned secure string. Rejects malformed or unsupported envelopes.</summary>
    public SecureString Decrypt(string encrypted, byte[] key)
    {
        ValidateKey(key);
        if (encrypted == null || encrypted.Length > 1024 * 1024 || !encrypted.StartsWith(Header, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsupported credential envelope.");
        var parts = Encoding.Unicode.GetString(Convert.FromBase64String(encrypted.Substring(Header.Length))).Split('|');
        if (parts.Length != 3 || parts[0] != "2" || parts[2].Length % 2 != 0)
            throw new InvalidDataException("Malformed credential envelope.");
        var cipher = new byte[parts[2].Length / 2];
        for (var i = 0; i < cipher.Length; i++)
            cipher[i] = byte.Parse(parts[2].Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(parts[1]);
        using var transform = aes.CreateDecryptor();
        var plain = transform.TransformFinalBlock(cipher, 0, cipher.Length);
        var result = new SecureString();
        try
        {
            if (plain.Length % 2 != 0)
                throw new InvalidDataException("Malformed decrypted credential.");
            for (var i = 0; i < plain.Length; i += 2)
                result.AppendChar((char)(plain[i] | plain[i + 1] << 8));
            result.MakeReadOnly();
            return result;
        }
        catch { result.Dispose(); throw; }
        finally { Array.Clear(plain, 0, plain.Length); }
    }
    private static void ValidateKey(byte[] key)
    { if (key == null || (key.Length != 16 && key.Length != 24 && key.Length != 32)) throw new ArgumentException("An AES key must contain 16, 24 or 32 bytes.", nameof(key)); }
    private static void ValidatePaths(FileSystemPath credential, FileSystemPath key)
    {
        if (credential == null)
            throw new ArgumentNullException(nameof(credential));
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (string.Equals(credential.Value, key.Value, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Credential and key paths must differ.");
        foreach (var path in new[] { credential, key })
            if (File.Exists(path.Value) && (File.GetAttributes(path.Value) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Credential files must not be reparse points.");
    }
    private static string ReadBounded(FileSystemPath path, int maximum)
    {
        using var stream = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximum)
            throw new InvalidDataException("Credential file exceeds the size limit.");
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }
    private static void Publish(FileSystemPath temporary, FileSystemPath destination, bool overwrite, FileSecurityManager security, FileSecurityDescriptor descriptor)
    {
        if (File.Exists(destination.Value))
        {
            if (!overwrite)
                throw new IOException("The credential destination already exists.");
            if ((File.GetAttributes(destination.Value) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Credential files must not be reparse points.");
            security.Write(destination, descriptor, FileSecurityParts.Access, true);
            File.Replace(temporary.Value, destination.Value, null);
        }
        else
            File.Move(temporary.Value, destination.Value);
    }
}
