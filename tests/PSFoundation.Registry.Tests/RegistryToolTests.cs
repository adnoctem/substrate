using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace PSFoundation.Registry.Tests;

public sealed class RegistryToolTests
{
    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("C:\\path\\", "\"C:\\path\\\\\"")]
    public void QuotesWindowsArguments(string value, string expected) => Assert.Equal(expected, RegistryTool.QuoteArgument(value));

    [Fact]
    public void RejectsNulArguments() => Assert.Throws<ArgumentException>(() => RegistryTool.QuoteArgument("a\0b"));

    [Theory]
    [InlineData("exit")]
    [InlineData("noexit")]
    [InlineData("timeout")]
    [InlineData("cancelled")]
    [InlineData("launch")]
    [InlineData("missing")]
    [InlineData("empty")]
    public void ExportFailuresPreserveDestinationAndCleanTemporaryFiles(string failure)
    {
        WithDirectory(directory =>
        {
            var destination = Path.Combine(directory, "old export.reg");
            File.WriteAllText(destination, "old");
            var tool = new FakeTool(arguments =>
            {
                if (failure == "launch")
                    throw new IOException("Synthetic launch failure");
                if (failure != "missing")
                    File.WriteAllText(arguments[2], failure == "empty" ? "" : "new");
                return new RegistryToolResult(failure == "noexit" ? (int?)null : failure == "exit" ? 1 : 0,
                    cancelled: failure == "cancelled", timedOut: failure == "timeout");
            });
            Assert.ThrowsAny<Exception>(() => new RegistryFileService(tool).Export("HKCU\\Synthetic", destination, CancellationToken.None));
            Assert.Equal("old", File.ReadAllText(destination));
            Assert.Empty(Directory.GetFiles(directory, ".psf-reg-*"));
        });
    }

    [Fact]
    public void ExportPublishesAndReplacesOnlyAfterSuccess()
    {
        WithDirectory(directory =>
        {
            var destination = Path.Combine(directory, "new export.reg");
            var tool = new FakeTool(arguments => { File.WriteAllText(arguments[2], "new"); return new RegistryToolResult(0); });
            var service = new RegistryFileService(tool);
            service.Export("HKCU\\Synthetic", destination, CancellationToken.None);
            Assert.Equal("new", File.ReadAllText(destination));
            File.WriteAllText(destination, "old");
            service.Export("HKCU\\Synthetic", destination, CancellationToken.None);
            Assert.Equal("new", File.ReadAllText(destination));
            Assert.Empty(Directory.GetFiles(directory, ".psf-reg-*"));
        });
    }

    [Fact]
    public void CancellationBeforePublicationPreservesDestination()
    {
        WithDirectory(directory =>
        {
            var destination = Path.Combine(directory, "old.reg");
            File.WriteAllText(destination, "old");
            using (var cancellation = new CancellationTokenSource())
            {
                var tool = new FakeTool(arguments => { File.WriteAllText(arguments[2], "new"); cancellation.Cancel(); return new RegistryToolResult(0); });
                Assert.Throws<OperationCanceledException>(() => new RegistryFileService(tool).Export("HKCU\\Synthetic", destination, cancellation.Token));
                Assert.Equal("old", File.ReadAllText(destination));
                Assert.Empty(Directory.GetFiles(directory, ".psf-reg-*"));
            }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MountRefusesMissingExistingOrDeclinedTargets(bool exists, bool mounted)
    {
        var tool = new FakeTool(_ => throw new Exception("Must not run"));
        var service = new DefaultUserHiveService(tool, _ => mounted, _ => exists);
        Assert.Null(service.Mount("Synthetic", "synthetic.dat", (_, _) => false, (_, _) => { }, CancellationToken.None));
        Assert.Equal(0, tool.Calls);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void DismountRetriesExactlyOnceAfterReclaimingHandles(int initialCode, int calls)
    {
        var logs = new List<string>();
        var reclaimed = 0;
        var tool = new FakeTool(_ => new RegistryToolResult(initialCode, "synthetic"));
        var service = new DefaultUserHiveService(tool, _ => true, reclaimHandles: () => reclaimed++);
        service.Dismount("Synthetic", (_, _) => true, (message, _) => logs.Add(message), CancellationToken.None);
        Assert.Equal(calls, tool.Calls);
        Assert.Equal(calls - 1, reclaimed);
        Assert.Contains(initialCode == 0 ? "Unloaded" : "Investigate", logs[logs.Count - 1]);
    }

    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PSFoundation.Registry.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class FakeTool : IRegistryTool
    {
        private readonly Func<string[], RegistryToolResult> run;
        public int Calls;
        public FakeTool(Func<string[], RegistryToolResult> run) => this.run = run;
        public RegistryToolResult Run(string[] arguments, CancellationToken cancellation)
        {
            Calls++;
            return run(arguments);
        }
    }
}
