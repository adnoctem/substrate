using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PSFoundation.IO.Tests;

public sealed class FileManagerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "psf-io-" + Guid.NewGuid().ToString("N"));
    public FileManagerTests() { Directory.CreateDirectory(directory); }
    public void Dispose() { Directory.Delete(directory, true); }

    [Fact]
    public void PathsUseExplicitBasesAndRejectAmbientDriveState()
    {
        Assert.Throws<ArgumentException>(() => FileSystemPath.Parse("relative"));
        Assert.Throws<ArgumentException>(() => FileSystemPath.Parse("C:relative", directory));
        Assert.Throws<ArgumentException>(() => FileSystemPath.Parse(@"\relative", directory));
        Assert.Equal(Path.Combine(directory, "b"), FileSystemPath.Parse(@"a\..\b", directory).Value);
        var path = FileSystemPath.Parse(directory);
        Assert.Equal(Path.Combine(directory, "a"), path.Combine("a").Value);
    }

    [Fact]
    public async Task AtomicWritesRequireExplicitOverwriteAndLeaveNoTemporaryFiles()
    {
        var path = FileSystemPath.Parse(Path.Combine(directory, "target"));
        var manager = new FileManager();
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("first")))
            await manager.WriteAtomicallyAsync(path, content);
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("second")))
            await Assert.ThrowsAsync<IOException>(() => manager.WriteAtomicallyAsync(path, content));
        Assert.Equal("first", File.ReadAllText(path.Value));
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("abc")))
        {
            await manager.WriteAtomicallyAsync(path, content, overwrite: true);
            Assert.True(content.CanRead);
        }
        Assert.Equal("abc", File.ReadAllText(path.Value));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", await manager.ComputeSha256Async(path));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task CancelledAndFailedContentNeverReplaceDestination()
    {
        var path = FileSystemPath.Parse(Path.Combine(directory, "target"));
        File.WriteAllText(path.Value, "original");
        using (var content = new MemoryStream(new byte[] { 1 }))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileManager().WriteAtomicallyAsync(path, content, true, new CancellationToken(true)));
        using (var content = new FailingStream())
            await Assert.ThrowsAsync<IOException>(() => new FileManager().WriteAtomicallyAsync(path, content, true));
        Assert.Equal("original", File.ReadAllText(path.Value));
        Assert.Single(Directory.GetFiles(directory));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => Task.FromException(new IOException("synthetic source failure"));
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromException<int>(new IOException("synthetic source failure"));
    }
}
