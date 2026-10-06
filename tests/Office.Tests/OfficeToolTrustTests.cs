using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Security;
using Xunit;

namespace AdNoctem.Substrate.Office.Tests;

public sealed class OfficeToolTrustTests
{
    [Fact]
    public async Task DeploymentTrustRejectsUntrustedWritesAndUnsignedTools()
    {
        var guard = new OfficePathGuard();
        guard.CheckDescriptor(FileSecurityDescriptor.FromSddl("O:S-1-5-32-544G:S-1-5-32-544D:P(A;;FA;;;S-1-5-18)(A;;FA;;;S-1-5-32-544)(A;;FR;;;S-1-5-32-545)"), @"C:\Synthetic");
        var mutation = Assert.Throws<OfficeException>(() => guard.CheckDescriptor(FileSecurityDescriptor.FromSddl("O:S-1-5-32-544G:S-1-5-32-544D:P(A;;FA;;;S-1-5-32-544)(A;;DC;;;S-1-5-32-545)"), @"C:\Synthetic"));
        Assert.Equal(OfficeFailureReason.UntrustedMedia, mutation.Reason);
        Assert.Equal("WriteGrant", ((OfficePathDiagnostic)mutation.Data["OfficeDiagnostic"]!).Stage);
        var inherited = Assert.Throws<OfficeException>(() => guard.CheckDescriptor(FileSecurityDescriptor.FromSddl("O:S-1-5-32-544G:S-1-5-32-544D:P(A;;FA;;;S-1-5-32-544)(A;ID;0x1301bf;;;S-1-5-11)"), @"C:\Synthetic", "MediaDirectory"));
        var diagnostic = Assert.IsType<OfficePathDiagnostic>(inherited.Data["OfficeDiagnostic"]);
        Assert.True(diagnostic.IsInherited);
        Assert.Equal(InheritanceFlags.None, diagnostic.InheritanceFlags);
        Assert.Equal(PropagationFlags.None, diagnostic.PropagationFlags);
        Assert.Equal(@"Untrusted write grant for SID S-1-5-11 on MediaDirectory at 'C:\Synthetic': Modify, Synchronize; inherited=True; inheritance=None; propagation=None.", inherited.Message);
        Assert.Throws<OfficeException>(() => guard.CheckDescriptor(FileSecurityDescriptor.FromSddl("O:BAG:BAD:NO_ACCESS_CONTROL"), @"C:\Synthetic"));
        Assert.Throws<OfficeException>(() => guard.ValidatePath(@"C:\Media\..\Windows"));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Substrate.slnx")))
            root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
        var path = FileSystemPath.Parse(Path.Combine(root.FullName, "build", "bin", "ProcessHost", "Release", "net48", "ProcessHost.exe"));
        var assessment = new OdtTool().Check(path, SignatureNetworkAccess.CacheOnly);
        Assert.False(assessment.Valid);
        Assert.Equal(OfficeFailureReason.UntrustedTool, assessment.Reason);
        Assert.Equal(AuthenticodeStatus.NotSigned, assessment.Signature!.Status);
        // No vendor binary is executed: every mode stops at the same trust boundary.
        var tool = new OdtTool();
        foreach (OdtMode mode in Enum.GetValues(typeof(OdtMode)))
        {
            var rejection = await Assert.ThrowsAsync<OfficeException>(() => tool.InvokeAsync(path, mode, mode == OdtMode.Help ? null : path));
            Assert.Equal(OfficeFailureReason.UntrustedTool, rejection.Reason);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.HelpAsync(path, new CancellationToken(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.InstallAsync(path, TimeSpan.FromSeconds(1), cancellationToken: new CancellationToken(true)));
        var configuration = new OfficeConfiguration(OfficeProduct.Standard2019Volume, version: new Version("16.0.10417.20095"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OfficeMediaManager().PrepareAsync(configuration, path, path, new CancellationToken(true)));
        var document = new OdtConfigurationBuilder().Install(configuration, root.FullName);
        document.SelectSingleNode("/Configuration/Add/Product")!.Attributes!.Append(document.CreateAttribute("PIDKEY")).Value = "synthetic";
        var embeddedKey = await Assert.ThrowsAsync<OfficeException>(() => tool.InvokeConfigurationAsync(path, document, FileSystemPath.Parse(root.FullName)));
        Assert.Equal(OfficeFailureReason.InvalidAuthority, embeddedKey.Reason);
        Assert.Throws<OfficeException>(() => guard.RemoveWorkDirectory(root.FullName, root.FullName));
        Assert.Equal(OfficeFailureReason.UnsafePath, Assert.Throws<OfficeException>(() => guard.RemoveWorkDirectory(root.FullName.ToUpperInvariant() + Path.DirectorySeparatorChar, root.FullName)).Reason);
        var existingDirectory = Path.Combine(root.FullName, "build", "test-fixtures", "office-tool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(existingDirectory);
        try
        {
            var existing = Path.Combine(existingDirectory, "setup.exe");
            File.Copy(path.Value, existing);
            var rejection = await Assert.ThrowsAsync<OfficeException>(() => tool.InstallAsync(FileSystemPath.Parse(existingDirectory), TimeSpan.FromSeconds(5)));
            Assert.Equal(OfficeFailureReason.UntrustedTool, rejection.Reason);
            Assert.Equal(new FileInfo(path.Value).Length, new FileInfo(existing).Length);
            File.Delete(existing);
        }
        finally { Directory.Delete(existingDirectory, false); }
        using (var handler = new HeadHandler())
        using (var client = new HttpClient(handler))
        {
            var available = await new OdtTool(client).CheckSourceAsync(TimeSpan.FromSeconds(5));
            Assert.True(available.Available);
            Assert.Equal(3, available.ContentLength);
            Assert.Equal(1, handler.RequestCount);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OdtTool(client).CheckSourceAsync(TimeSpan.FromSeconds(5), cancellationToken: new CancellationToken(true)));
            Assert.Equal(1, handler.RequestCount);
        }
        using (var handler = new RedirectHandler())
        using (var client = new HttpClient(handler))
        {
            var rejected = await new OdtTool(client).CheckSourceAsync(TimeSpan.FromSeconds(5));
            Assert.False(rejected.Available);
            Assert.IsType<InvalidDataException>(rejected.Error);
            Assert.Equal(1, handler.RequestCount);
        }
    }
    private sealed class RedirectHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.Equal(OdtSource.Reviewed.DownloadUri, request.RequestUri);
            var response = new HttpResponseMessage(HttpStatusCode.Redirect) { RequestMessage = request };
            response.Headers.Location = new Uri("http://download.microsoft.com/unsigned.exe");
            return Task.FromResult(response);
        }
    }
    private sealed class HeadHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            Assert.Equal(OdtSource.Reviewed.DownloadUri, request.RequestUri);
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) });
        }
    }
}
