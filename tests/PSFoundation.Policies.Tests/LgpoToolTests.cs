using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;
using PSFoundation.IO;
using Xunit;

namespace PSFoundation.Policies.Tests;

public sealed class LgpoToolTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PSF-Lgpo-" + Guid.NewGuid().ToString("N"));
    public LgpoToolTests() => Directory.CreateDirectory(root);
    private static FileSystemPath Executable
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PSFoundation.slnx")))
                directory = directory.Parent;
            return FileSystemPath.Parse(Path.Combine(directory!.FullName, "build", "bin", "ProcessHost", "Release", "net48", "ProcessHost.exe"));
        }
    }
    [Fact]
    public void CatalogPinsTheReviewedArchiveAndExecutableSeparately()
    {
        Assert.Equal("CB7159D134A0A1E7B1ED2ADA9A3CE8CE8F4DE391D14403D55438AF824247CC55", LgpoSource.Standalone.TrustedSha256);
        Assert.Equal("0C97F29543418B30340C4FF5D930D31E6196DD59C2CC74B6B890FA7B90C910C7", LgpoSource.Standalone.TrustedBinarySha256);
        Assert.Equal(new Version(3, 0, 2004, 13001), LgpoSource.Standalone.BinaryVersion);
        Assert.Equal(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), LgpoSource.Standalone.VerifiedAtUtc);
        Assert.Equal("LGPO_30/LGPO.exe", LgpoSource.Standalone.ExpectedBinaryPath);
        Assert.Equal("https", LgpoSource.Standalone.DownloadUri.Scheme);
    }
    [Fact]
    public async Task AppliesTextAndBackupWithLiteralPathsAndRetainsFailureEvidence()
    {
        var tool = new LgpoTool();
        var policy = FileSystemPath.Parse(Path.Combine(root, "policy with spaces & literal.txt"));
        File.WriteAllText(policy.Value, "fail");
        var request = tool.CreateApplyRequest(Executable, policy, TimeSpan.FromSeconds(5), ProcessStopBehavior.TerminateProcess);
        Assert.Equal(new[] { "/t", policy.Value }, request.Arguments);
        var result = await tool.ApplyAsync(Executable, policy, TimeSpan.FromSeconds(5), ProcessStopBehavior.TerminateProcess);
        Assert.Equal(7, result.ExitCode);
        Assert.Contains("ARG:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(policy.Value)), result.StandardOutput);
        Assert.Contains("Synthetic failure", result.StandardError);
        var backup = await tool.ApplyAsync(Executable, FileSystemPath.Parse(root), TimeSpan.FromSeconds(5), ProcessStopBehavior.TerminateProcess);
        Assert.Equal(0, backup.ExitCode);
        Assert.StartsWith("/g", backup.StandardOutput);
    }
    [Fact]
    public async Task RejectsMissingFilesAndHonorsCancellationBeforeLaunch()
    {
        var tool = new LgpoTool();
        var missing = FileSystemPath.Parse(Path.Combine(root, "missing.exe"));
        Assert.False(tool.IsInstalled(missing));
        Assert.False(tool.IsInstalled(FileSystemPath.Parse(root)));
        Assert.Throws<FileNotFoundException>(() => tool.CreateApplyRequest(missing, FileSystemPath.Parse(root), TimeSpan.FromSeconds(1), ProcessStopBehavior.TerminateProcess));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.ApplyAsync(Executable, missing, TimeSpan.FromSeconds(1),
            ProcessStopBehavior.TerminateProcess, new CancellationToken(true)));
    }
    public void Dispose() => Directory.Delete(root, true);
}
