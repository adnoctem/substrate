using System;
using System.IO;
using System.Security.Principal;
using System.Threading;
using AdNoctem.Substrate.IO;
using Xunit;

namespace AdNoctem.Substrate.Security.Tests;

public sealed class FileTrustTests
{
    [Fact]
    public void NativeFileSecurityAndUnsignedExecutableVerificationReleaseResources()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Substrate.slnx")))
            root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
        var parent = Path.Combine(root.FullName, "build", "test-fixtures", "file-security");
        Directory.CreateDirectory(parent);
        var path = FileSystemPath.Parse(Path.Combine(parent, Guid.NewGuid().ToString("N")));
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var sid = identity.User!.Value;
            var descriptor = FileSecurityDescriptor.FromSddl("O:" + sid + "D:P(A;OICI;FA;;;" + sid + ")");
            var manager = new FileSecurityManager();
            manager.CreateDirectory(path, descriptor);
            try
            {
                var actual = manager.Read(path);
                Assert.Equal(sid, actual.ToRawDescriptor().Owner!.Value);
                manager.Write(path, descriptor, FileSecurityParts.Access, protectAccessRules: true);
                Assert.Contains("D:P", manager.Read(path).GetSddl());
                Assert.Throws<System.ComponentModel.Win32Exception>(() => manager.CreateDirectory(path, descriptor));
            }
            finally { Directory.Delete(path.Value); }
        }
        var executable = FileSystemPath.Parse(Path.Combine(parent, Guid.NewGuid().ToString("N") + ".exe"));
        File.Copy(Path.Combine(root.FullName, "build", "bin", "ProcessHost", "Release", "net48", "ProcessHost.exe"), executable.Value);
        try
        {
            var verifier = new AuthenticodeVerifier();
            var result = verifier.Verify(executable, SignatureNetworkAccess.CacheOnly);
            Assert.False(result.Valid);
            Assert.Equal(AuthenticodeStatus.NotSigned, result.Status);
            Assert.Null(result.Signer);
            Assert.Throws<OperationCanceledException>(() => verifier.Verify(executable, cancellationToken: new CancellationToken(true)));
            using (var exclusive = new FileStream(executable.Value, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.True(exclusive.Length > 0);
        }
        finally { File.Delete(executable.Value); }
    }
}
