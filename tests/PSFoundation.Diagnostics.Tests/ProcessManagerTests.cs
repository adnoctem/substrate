using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSFoundation.Diagnostics;
using Xunit;

namespace PSFoundation.Diagnostics.Tests;

public sealed class ProcessManagerTests
{
    private static string Executable
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PSFoundation.slnx")))
                directory = directory.Parent;
            return Path.Combine(directory!.FullName, "build", "bin", "ProcessHost", "Release", "net48", "ProcessHost.exe");
        }
    }

    [Fact]
    public async Task ArgumentsRemainLiteralAcrossTheProcessBoundary()
    {
        var args = new[] { "", "a b", "a\"b", @"C:\folder with spaces\", "'quoted'", "$(whoami)", "a&b|c", "Grüße 日本語", "line\nbreak" };
        var request = new ProcessRequest(Executable, new[] { "arguments" }.Concat(args));
        var result = await new ProcessManager().RunAsync(request);
        Assert.Equal(0, result.ExitCode);
        var returned = result.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(4))));
        Assert.Equal(args, returned);
    }

    [Fact]
    public async Task DrainsBothFullPipesAndRetainsTheExitCode()
    {
        var result = await new ProcessManager().RunAsync(new ProcessRequest(Executable, new[] { "streams" }));
        Assert.Equal(17, result.ExitCode);
        Assert.Equal(new string('O', 524288), result.StandardOutput);
        Assert.Equal(new string('E', 524288), result.StandardError);
        Assert.False(result.OutputTruncated);
    }

    [Fact]
    public async Task BoundedCaptureStillDrainsToCompletion()
    {
        var result = await new ProcessManager().RunAsync(new ProcessRequest(Executable, new[] { "streams" }, maximumCapturedCharacters: 123));
        Assert.Equal(17, result.ExitCode);
        Assert.Equal(new string('O', 123), result.StandardOutput);
        Assert.Equal(new string('E', 123), result.StandardError);
        Assert.True(result.OutputTruncated);
        Assert.True(result.ErrorTruncated);
    }

    [Fact]
    public async Task CancellationBeforeLaunchDoesNotExecute()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessManager().RunAsync(new ProcessRequest("does-not-exist.exe"), new CancellationToken(true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopRequestsTerminateOwnedProcessesAndPreserveEvidence(bool cancel)
    {
        using (var cancellation = new CancellationTokenSource())
        {
            if (cancel)
                cancellation.CancelAfter(1500);
            var result = await new ProcessManager().RunAsync(new ProcessRequest(Executable, new[] { "wait" }, timeout: TimeSpan.FromSeconds(cancel ? 10 : 1)), cancellation.Token);
            Assert.Equal(cancel, result.Cancelled);
            Assert.Equal(!cancel, result.TimedOut);
            Assert.Contains(result.ProcessId.ToString(), result.StandardOutput);
            AssertStopped(result.ProcessId);
        }
    }

    [Fact]
    public async Task TreeTerminationCleansUpDescendantsOnBothRuntimes()
    {
        var result = await new ProcessManager().RunAsync(new ProcessRequest(Executable, new[] { "tree" }, timeout: TimeSpan.FromSeconds(2), stopBehavior: ProcessStopBehavior.TerminateTree));
        Assert.True(result.TimedOut);
        var identifiers = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        Assert.Equal(2, identifiers.Length);
        foreach (var identifier in identifiers)
            AssertStopped(identifier);
    }

    [Fact]
    public async Task WaitForExitRecordsCancellationWithoutInterruptingOwnedWork()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.CancelAfter(200);
            var result = await new ProcessManager().RunAsync(new ProcessRequest(Executable, new[] { "short-wait" }, stopBehavior: ProcessStopBehavior.WaitForExit), cancellation.Token);
            Assert.True(result.Cancelled);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("finished", result.StandardOutput);
        }
    }

    [Fact]
    public void RequestCopiesArgumentsAndEnvironmentAndRejectsNul()
    {
        var args = new[] { "original" };
        var environment = new Dictionary<string, string?> { ["VALUE"] = "before" };
        var request = new ProcessRequest(Executable, args, environment: environment);
        args[0] = "after";
        environment["VALUE"] = "after";
        Assert.Equal("original", request.Arguments[0]);
        Assert.Equal("before", request.Environment["VALUE"]);
        Assert.Throws<ArgumentException>(() => new ProcessRequest(Executable, new[] { "a\0b" }));
    }

    [Fact]
    public async Task ChildEnvironmentAndWorkingDirectoryDoNotMutateTheParent()
    {
        var before = Environment.GetEnvironmentVariable("PSF_SYNTHETIC_VALUE");
        var directory = Path.GetDirectoryName(Executable)!;
        var result = await new ProcessManager().RunAsync(new ProcessRequest(Executable, new[] { "environment" }, directory,
            new Dictionary<string, string?> { ["PSF_SYNTHETIC_VALUE"] = "synthetic" }));
        Assert.Equal(directory + Environment.NewLine + "synthetic" + Environment.NewLine, result.StandardOutput);
        Assert.Equal(before, Environment.GetEnvironmentVariable("PSF_SYNTHETIC_VALUE"));
    }

    private static void AssertStopped(int id)
    {
        try
        { using (var process = Process.GetProcessById(id)) Assert.True(process.HasExited); }
        catch (ArgumentException) { }
    }
}
