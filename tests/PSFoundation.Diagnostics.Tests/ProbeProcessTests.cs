using System;
using System.IO;
using PSFoundation.DevTools;
using Xunit;

namespace PSFoundation.Diagnostics.Tests;

public sealed class ProbeProcessTests
{
    private static string Fixture
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
    public void RunnerBoundsCapturedOutputWhileDrainingBothStreams()
    {
        var result = ProbeProcess.Run(Fixture, new[] { "streams" }, 10000, 64);
        Assert.Equal(17, result.ExitCode);
        Assert.Equal(2 * 131072, result.Output.Length);
    }
    [Fact]
    public void RunnerTerminatesAStalledProbe()
        => Assert.Throws<TimeoutException>(() => ProbeProcess.Run(Fixture, new[] { "wait" }, 1000, 64));
    [Fact]
    public void WindowsEnforcesTheJobMemoryLimit()
    {
        var result = ProbeProcess.Run(Fixture, new[] { "bounded-memory" }, 10000, 64);
        Assert.True(result.ExitCode == 23, "Memory probe exit code: " + result.ExitCode + "; output: " + result.Output);
        Assert.Contains("MemoryLimitReached", result.Output);
    }
    [Fact]
    public void RunnerQuotesLiteralArguments()
    {
        var input = "space \" quote \\";
        var result = ProbeProcess.Run(Fixture, new[] { "arguments", input }, 10000, 64);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ARG:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input)), result.Output);
    }
}
