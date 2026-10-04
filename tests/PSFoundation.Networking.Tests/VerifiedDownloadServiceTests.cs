using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.IO;
using Xunit;

namespace PSFoundation.Networking.Tests;

public sealed class VerifiedDownloadServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PSF-Download-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] payload = Encoding.UTF8.GetBytes("synthetic verified content");
    private static readonly Uri Source = new Uri("https://example.invalid/tool.zip");
    public VerifiedDownloadServiceTests() => Directory.CreateDirectory(root);
    private FileSystemPath Destination => FileSystemPath.Parse(Path.Combine(root, "tool.zip"));
    private string Digest { get { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(payload)).Replace("-", "").ToLowerInvariant(); } }
    private HttpResponseMessage Response(HttpRequestMessage request) => new HttpResponseMessage(HttpStatusCode.OK)
    { RequestMessage = request, Content = new ByteArrayContent(payload) };

    [Fact]
    public async Task PublishesVerifiedContentAndBorrowsTheClient()
    {
        using (var handler = new Handler((request, _) => Task.FromResult(Response(request))))
        using (var client = new HttpClient(handler))
        {
            var service = new VerifiedDownloadService(client);
            await service.DownloadAsync(Source, Destination, Digest, 1024, TimeSpan.FromSeconds(5));
            Assert.Equal(payload, File.ReadAllBytes(Destination.Value));
            await service.DownloadAsync(Source, Destination, Digest, 1024, TimeSpan.FromSeconds(5), true);
            using (var response = await client.GetAsync(Source))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, handler.Calls);
            Assert.Single(Directory.GetFiles(root));
        }
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("unreported-size")]
    [InlineData("status")]
    [InlineData("redirect")]
    [InlineData("transport")]
    public async Task FailuresKeepExistingDestinationAndCleanTemporaryFiles(string failure)
    {
        File.WriteAllText(Destination.Value, "existing");
        using (var handler = new Handler((request, _) =>
        {
            if (failure == "transport")
                throw new HttpRequestException("Synthetic transport failure");
            var response = Response(request);
            if (failure == "status")
                response.StatusCode = HttpStatusCode.ServiceUnavailable;
            if (failure == "redirect")
                response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://example.invalid/tool.zip");
            if (failure == "unreported-size")
                response.Content.Headers.ContentLength = 1;
            return Task.FromResult(response);
        }))
        using (var client = new HttpClient(handler))
        {
            await Assert.ThrowsAnyAsync<Exception>(() => new VerifiedDownloadService(client).DownloadAsync(Source, Destination,
                failure == "hash" ? new string('0', 64) : Digest, failure.Contains("size") ? 3 : 1024, TimeSpan.FromSeconds(5), true));
            Assert.Equal("existing", File.ReadAllText(Destination.Value));
            Assert.Single(Directory.GetFiles(root));
        }
    }

    [Fact]
    public async Task RejectsUntrustedInputsAndExistingFilesBeforeNetworkAccess()
    {
        using (var handler = new Handler((request, _) => Task.FromResult(Response(request))))
        using (var client = new HttpClient(handler))
        {
            var service = new VerifiedDownloadService(client);
            await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadAsync(Source, Destination, "PLACEHOLDER", 1024, TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadAsync(new Uri("http://example.invalid"), Destination, Digest, 1024, TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadAsync(new Uri("https://user:secret@example.invalid"), Destination, Digest, 1024, TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadAsync(Source, Destination, Digest, 1024, TimeSpan.FromSeconds(1), cancellationToken: new CancellationToken(true)));
            File.WriteAllText(Destination.Value, "existing");
            await Assert.ThrowsAsync<IOException>(() => service.DownloadAsync(Source, Destination, Digest, 1024, TimeSpan.FromSeconds(1)));
            Assert.Equal(0, handler.Calls);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelsDuringBodyReadAndPreservesTheDestination(bool useTimeout)
    {
        File.WriteAllText(Destination.Value, "existing");
        var body = new SlowBody();
        using (var stop = new CancellationTokenSource())
        using (var handler = new Handler((request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { RequestMessage = request, Content = new StreamContent(body) })))
        using (var client = new HttpClient(handler))
        {
            if (!useTimeout)
                stop.CancelAfter(100);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new VerifiedDownloadService(client).DownloadAsync(
                Source, Destination, Digest, 1024, TimeSpan.FromMilliseconds(useTimeout ? 100 : 5000), true, stop.Token));
            Assert.Equal("existing", File.ReadAllText(Destination.Value));
            Assert.Single(Directory.GetFiles(root));
            Assert.True(body.Disposed);
        }
    }

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
        internal int Calls;
        internal Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => this.send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }
    private sealed class SlowBody : MemoryStream
    {
        internal bool Disposed;
        public override bool CanSeek => false;
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        { await Task.Delay(10000, cancellationToken); return 0; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    public void Dispose() => Directory.Delete(root, true);
}
