using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;
using Xunit;

namespace PSFoundation.Policies.Tests;

public sealed class LgpoInstallationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PSF-install-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("synthetic payload, never executed");
    public LgpoInstallationTests() => Directory.CreateDirectory(root);
    private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
    private static byte[] Archive(string entry = "LGPO_30/LGPO.exe", string? extra = null)
    {
        using (var memory = new MemoryStream())
        {
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                using (var output = zip.CreateEntry(entry).Open())
                    output.Write(Payload, 0, Payload.Length);
                if (extra != null)
                    using (var output = zip.CreateEntry(extra).Open())
                        output.WriteByte(1);
            }
            return memory.ToArray();
        }
    }
    private static LgpoSource Source(byte[] zip, string? binaryHash = null) => new LgpoSource("synthetic", new Uri("https://example.invalid/lgpo.zip"),
        "LGPO_30/LGPO.exe", Hash(zip), binaryHash ?? Hash(Payload), new Version(1, 0), DateTime.UtcNow);

    [Fact]
    public async Task InstallVerifiesBothLayersAndAvailabilityDoesNotDownload()
    {
        var zip = Archive();
        using (var handler = new Handler(zip))
        using (var client = new HttpClient(handler))
        {
            var tool = new LgpoTool(client);
            var source = Source(zip);
            var availability = await tool.CheckSourceAsync(TimeSpan.FromSeconds(5), source);
            Assert.True(availability.Available);
            Assert.Equal(HttpMethod.Head, handler.LastMethod);
            Assert.Empty(Directory.GetFiles(root));
            var result = await tool.InstallAsync(FileSystemPath.Parse(root), TimeSpan.FromSeconds(5), source: source);
            Assert.Equal(Payload, File.ReadAllBytes(result.Value));
            Assert.Single(Directory.GetFiles(root));
            await Assert.ThrowsAsync<IOException>(() => tool.InstallAsync(FileSystemPath.Parse(root), TimeSpan.FromSeconds(5), source: source));
            Assert.Equal(2, handler.Calls);
            await tool.InstallAsync(FileSystemPath.Parse(root), TimeSpan.FromSeconds(5), true, source);
            Assert.Single(Directory.GetFiles(root));
            using (var response = await client.GetAsync(source.DownloadUri))
                Assert.True(response.IsSuccessStatusCode);
        }
    }

    [Theory]
    [InlineData("archive-hash")]
    [InlineData("binary-hash")]
    [InlineData("wrong-entry")]
    [InlineData("traversal")]
    [InlineData("duplicate")]
    [InlineData("case-duplicate")]
    public async Task UntrustedContentCannotReplaceAnExistingExecutable(string failure)
    {
        var zip = Archive(failure == "wrong-entry" ? "unexpected/LGPO.exe" : "LGPO_30/LGPO.exe",
            failure == "traversal" ? "../escape.exe" : failure == "duplicate" ? "LGPO_30/LGPO.exe" : failure == "case-duplicate" ? "lgpo_30/lgpo.exe" : null);
        var source = Source(failure == "archive-hash" ? new byte[] { 0 } : zip, failure == "binary-hash" ? new string('0', 64) : null);
        var executable = Path.Combine(root, "LGPO.exe");
        File.WriteAllText(executable, "original");
        using (var client = new HttpClient(new Handler(zip)))
            await Assert.ThrowsAsync<InvalidDataException>(() => new LgpoTool(client).InstallAsync(FileSystemPath.Parse(root), TimeSpan.FromSeconds(5), true, source));
        Assert.Equal("original", File.ReadAllText(executable));
        Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public async Task CanceledOperationsDoNotCreateDestinationOrSendRequests()
    {
        using (var handler = new Handler(Archive()))
        using (var client = new HttpClient(handler))
        {
            var tool = new LgpoTool(client);
            var token = new CancellationToken(true);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.InstallAsync(FileSystemPath.Parse(Path.Combine(root, "absent")), TimeSpan.FromSeconds(1), cancellationToken: token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.CheckSourceAsync(TimeSpan.FromSeconds(1), cancellationToken: token));
            Assert.Equal(0, handler.Calls);
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArchiveLimitsKeepTheOldDestination(bool limitArchive)
    {
        var zip = Archive();
        var archive = FileSystemPath.Parse(Path.Combine(root, "archive.zip"));
        File.WriteAllBytes(archive.Value, zip);
        var destination = FileSystemPath.Parse(Path.Combine(root, "LGPO.exe"));
        File.WriteAllText(destination.Value, "original");
        Assert.Throws<InvalidDataException>(() => new VerifiedArchiveService().ExtractFile(archive, destination, "LGPO_30/LGPO.exe", Hash(zip), Hash(Payload),
            limitArchive ? 1 : 1024 * 1024, limitArchive ? 1024 * 1024 : 1, true));
        Assert.Equal("original", File.ReadAllText(destination.Value));
        Assert.Equal(2, Directory.GetFiles(root).Length);
    }

    private sealed class Handler : HttpMessageHandler
    {
        private readonly byte[] zip;
        internal int Calls;
        internal HttpMethod? LastMethod;
        internal Handler(byte[] zip) { this.zip = zip; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastMethod = request.Method;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(zip) });
        }
    }
    public void Dispose() => Directory.Delete(root, true);
}
