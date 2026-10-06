using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.Diagnostics;
using Xunit;

namespace AdNoctem.Substrate.Diagnostics.Tests;

public sealed class ProcessManagerTests
{
    private static string Executable
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (
                directory != null
                && !File.Exists(Path.Combine(directory.FullName, "Substrate.slnx"))
            )
                directory = directory.Parent;

            return Path.Combine(
                directory!.FullName,
                "build",
                "bin",
                "ProcessHost",
                "Release",
                "net48",
                "ProcessHost.exe"
            );
        }
    }

    [Fact]
    public async Task ArgumentsRemainLiteralAcrossTheProcessBoundary()
    {
        var args = new[]
        {
            "",
            "a b",
            "a\"b",
            @"C:\folder with spaces\",
            "'quoted'",
            "$(whoami)",
            "a&b|c",
            "Grüße 日本語",
            "line\nbreak",
        };
        var request = new ProcessRequest(Executable, new[] { "arguments" }.Concat(args));
        var result = await new ProcessManager().RunAsync(request);
        Assert.Equal(0, result.ExitCode);
        var returned = result
            .StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(4))));
        Assert.Equal(args, returned);
    }

    [Fact]
    public async Task DrainsBothFullPipesAndRetainsTheExitCode()
    {
        var result = await new ProcessManager().RunAsync(
            new ProcessRequest(Executable, new[] { "streams" })
        );
        Assert.Equal(17, result.ExitCode);
        Assert.Equal(new string('O', 524288), result.StandardOutput);
        Assert.Equal(new string('E', 524288), result.StandardError);
        Assert.False(result.OutputTruncated);
    }

    [Fact]
    public async Task BoundedCaptureStillDrainsToCompletion()
    {
        var result = await new ProcessManager().RunAsync(
            new ProcessRequest(Executable, new[] { "streams" }, maximumCapturedCharacters: 123)
        );
        Assert.Equal(17, result.ExitCode);
        Assert.Equal(new string('O', 123), result.StandardOutput);
        Assert.Equal(new string('E', 123), result.StandardError);
        Assert.True(result.OutputTruncated);
        Assert.True(result.ErrorTruncated);
    }

    [Fact]
    public async Task CancellationBeforeLaunchDoesNotExecute()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ProcessManager().RunAsync(
                new ProcessRequest("does-not-exist.exe"),
                new CancellationToken(true)
            )
        );
    }

    [Fact]
    public async Task TimeoutTerminatesAProcessWithoutOutput()
    {
        var result = await new ProcessManager().RunAsync(
            new ProcessRequest(Executable, new[] { "sleep" }, timeout: TimeSpan.FromSeconds(1))
        );
        Assert.True(result.TimedOut);
        Assert.False(result.Cancelled);
        Assert.Empty(result.StandardOutput);
        AssertStopped(result.ProcessId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationTerminatesReadyProcessesAndPreservesEvidence(bool tree)
    {
        var eventName = @"Local\Substrate.ProcessTests." + Guid.NewGuid().ToString("N");

        using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset, eventName))
        using (var cancellation = new CancellationTokenSource())
        {
            var running = new ProcessManager().RunAsync(
                new ProcessRequest(
                    Executable,
                    new[] { tree ? "tree" : "wait", eventName },
                    stopBehavior: tree
                        ? ProcessStopBehavior.TerminateTree
                        : ProcessStopBehavior.TerminateProcess
                ),
                cancellation.Token
            );
            ProcessResult result;

            try
            {
                // Startup time on shared runners is unrelated to output capture or termination behavior.
                Assert.True(
                    await Task.Run(() => ready.WaitOne(TimeSpan.FromSeconds(30))),
                    "Process fixture did not signal readiness."
                );
            }
            finally
            {
                cancellation.Cancel();
                result = await running;
            }

            Assert.True(result.Cancelled);
            Assert.False(result.TimedOut);
            var identifiers = result
                .StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();
            Assert.Equal(tree ? 2 : 1, identifiers.Length);
            Assert.Equal(result.ProcessId, identifiers[0]);

            foreach (var identifier in identifiers)
                AssertStopped(identifier);
        }
    }

    [Fact]
    public async Task WaitForExitRecordsCancellationWithoutInterruptingOwnedWork()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.CancelAfter(200);
            var result = await new ProcessManager().RunAsync(
                new ProcessRequest(
                    Executable,
                    new[] { "short-wait" },
                    stopBehavior: ProcessStopBehavior.WaitForExit
                ),
                cancellation.Token
            );
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
        var before = Environment.GetEnvironmentVariable("SUBSTRATE_SYNTHETIC_VALUE");
        var directory = Path.GetDirectoryName(Executable)!;
        var result = await new ProcessManager().RunAsync(
            new ProcessRequest(
                Executable,
                new[] { "environment" },
                directory,
                new Dictionary<string, string?> { ["SUBSTRATE_SYNTHETIC_VALUE"] = "synthetic" }
            )
        );
        Assert.Equal(
            directory + Environment.NewLine + "synthetic" + Environment.NewLine,
            result.StandardOutput
        );
        Assert.Equal(before, Environment.GetEnvironmentVariable("SUBSTRATE_SYNTHETIC_VALUE"));
    }

    private static void AssertStopped(int id)
    {
        try
        {
            using (var process = Process.GetProcessById(id))
                Assert.True(process.HasExited);
        }
        catch (ArgumentException) { }
    }
}
