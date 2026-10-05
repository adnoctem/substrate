using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;
using Xunit;

namespace AdNoctem.Substrate.Windows.Tests;

public sealed class OfflineDomainJoinManagerTests
{
    [Fact]
    public async Task PackageFilesRoundTripAndOverwriteWithoutLeavingAnOldTail()
    {
        const string synthetic = "Synthetic test package";
        var encoded = DomainJoinPackageCodec.Encode(synthetic);
        Assert.Equal(new byte[] { 0xff, 0xfe }, encoded.Take(2));
        Assert.Equal(synthetic, DomainJoinPackageCodec.Decode(encoded));
        Assert.Throws<InvalidDataException>(() => DomainJoinPackageCodec.Decode(encoded.Take(encoded.Length - 1).ToArray()));
        var directory = Path.Combine(Path.GetTempPath(), "psf-provision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = FileSystemPath.Parse(Path.Combine(directory, "synthetic.djoin"));
            var manager = new OfflineDomainJoinManager();
            await manager.WritePackageAsync(synthetic, path);
            Assert.Throws<IOException>(() => manager.WritePackage("new", path));
            Assert.Equal(10, await manager.WritePackageAsync("new", path, overwrite: true));
            Assert.Equal("new", DomainJoinPackageCodec.Decode(File.ReadAllBytes(path.Value)));
            Assert.Single(Directory.GetFiles(directory));
            var request = new OfflineDomainJoinRequest("example.invalid", "SYNTHETIC");
            Assert.Equal(DomainJoinProvisionOptions.None, request.Options);
            Assert.Throws<OperationCanceledException>(() => manager.CreatePackage(request, cancellationToken: new CancellationToken(true)));
        }
        finally { Directory.Delete(directory, true); }
    }
}
