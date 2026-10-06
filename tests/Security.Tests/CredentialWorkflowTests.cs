using System;
using System.IO;
using System.Net;
using System.Security;
using System.Security.AccessControl;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Security;
using Xunit;

namespace AdNoctem.Substrate.Security.Tests;

public sealed class CredentialWorkflowTests
{
    [Fact]
    public void CredentialPairRoundTripsWithProtectedAccessAndExplicitReplacement()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "psf-credential-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);

        try
        {
            var credential = FileSystemPath.Parse(Path.Combine(root, "credential"));
            var key = FileSystemPath.Parse(Path.Combine(root, "key"));

            using var password = new SecureString();

            foreach (var c in "synthetic-ä-秘密")
                password.AppendChar(c);

            password.MakeReadOnly();
            var manager = new CredentialFileManager();
            manager.Write(credential, key, "EXAMPLE\\fixture", password);

            using (var stored = manager.Read(credential, key))
            using (var decoded = stored.GetPassword())
            {
                Assert.Equal("EXAMPLE\\fixture", stored.UserName);
                Assert.Equal("synthetic-ä-秘密", new NetworkCredential("", decoded).Password);
            }

            var descriptor = new FileSecurityManager()
                .Read(key, FileSecurityParts.Access)
                .ToRawDescriptor();
            Assert.True((descriptor.ControlFlags & ControlFlags.DiscretionaryAclProtected) != 0);
            Assert.Single(descriptor.DiscretionaryAcl!);
            Assert.Throws<IOException>(() => manager.Write(credential, key, "fixture", password));
            manager.Write(credential, key, "replacement", password, true);

            using var replacement = manager.Read(credential, key);

            Assert.Equal("replacement", replacement.UserName);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
