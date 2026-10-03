using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Xunit;

namespace PSFoundation.Registry.Tests;

public sealed class RegistryServicesTests
{
    [Theory]
    [InlineData(RegistryView.Registry32)]
    [InlineData(RegistryView.Registry64)]
    public async Task FilesRoundTripRealRegistryDataWithExplicitViewAndOverwrite(RegistryView view)
    {
        using (var fixture = new RegistryFixture(view))
        using (var directory = new FileFixture())
        {
            var manager = fixture.Manager;
            manager.SetValue(fixture.Root, "Quoted value", RegistryValue.String("data with \"quotes\" and \\slashes"), true);
            manager.SetValue(fixture.Root, "Raw", RegistryValue.ExpandString("%TEMP%\\raw"));
            var destination = Path.Combine(directory.Path, "export with spaces.reg");
            await manager.Files.ExportAsync(fixture.Root, destination);
            var original = File.ReadAllBytes(destination);
            await Assert.ThrowsAsync<IOException>(() => manager.Files.ExportAsync(fixture.Root, destination));
            Assert.Equal(original, File.ReadAllBytes(destination));
            manager.SetValue(fixture.Root, "New", RegistryValue.DWord(-1));
            manager.Files.Export(fixture.Root, destination, overwrite: true);
            manager.DeleteKey(fixture.Root, true);
            await manager.Files.ImportAsync(destination);
            Assert.Equal(-1, manager.GetValue(fixture.Root, "New")!.GetData<int>());
            Assert.Equal("%TEMP%\\raw", manager.GetValue(fixture.Root, "Raw")!.GetString());
            Assert.Equal("data with \"quotes\" and \\slashes", manager.GetValue(fixture.Root, "Quoted value")!.GetString());
            Assert.Empty(Directory.GetFiles(directory.Path, ".psf-reg-*"));
            using (var handle = manager.OpenKey(fixture.Root))
                Assert.Equal(view, handle!.View);
        }
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("cancel")]
    public async Task FailedFilePublicationPreservesOldBytes(string failure)
    {
        using (var directory = new FileFixture())
        using (var cancellation = new CancellationTokenSource())
        {
            var destination = Path.Combine(directory.Path, "existing.reg");
            File.WriteAllText(destination, "previous");
            var runner = new FakeRunner((args, _) =>
            {
                if (failure != "missing")
                    File.WriteAllText(args[2], failure == "empty" ? "" : "replacement");
                if (failure == "cancel")
                    cancellation.Cancel();
                return new RegistryCommandResult(failure == "exit" ? 1 : 0);
            });
            var files = new RegistryFileService(new RegistryManager(), runner);
            await Assert.ThrowsAnyAsync<Exception>(() => files.ExportAsync(RegistryPath.Parse(@"HKCU\Synthetic"), destination, true, cancellation.Token));
            Assert.Equal("previous", File.ReadAllText(destination));
            Assert.Empty(Directory.GetFiles(directory.Path, ".psf-reg-*"));
        }
    }

    [Fact]
    public async Task FileImportRejectsRemoteAndReportsNativeFailureWithoutHidingPartialMutation()
    {
        using (var directory = new FileFixture())
        {
            var source = Path.Combine(directory.Path, "input.reg");
            File.WriteAllText(source, "synthetic");
            var runner = new FakeRunner((args, _) => { Assert.Equal("/reg:32", args[2]); return new RegistryCommandResult(1, "out", standardError: "error"); });
            var failure = await Assert.ThrowsAsync<RegistryCommandException>(() => new RegistryFileService(new RegistryManager(RegistryView.Registry32), runner).ImportAsync(source));
            Assert.Equal(1, failure.ExitCode);
            Assert.Equal("error", failure.StandardError);
            await Assert.ThrowsAsync<NotSupportedException>(() => new RegistryManager(machineName: "synthetic.invalid").Files.ImportAsync(source));
        }
    }

    [Fact]
    public async Task HiveLeaseOwnsOnlySuccessfulLoadsAndAllowsUnloadRetry()
    {
        using (var directory = new FileFixture())
        using (var cancellation = new CancellationTokenSource())
        {
            var source = Path.Combine(directory.Path, "synthetic.hiv");
            File.WriteAllText(source, "synthetic");
            var mount = RegistryPath.Parse(@"HKU\PSFoundationSynthetic");
            var unloads = 0;
            var runner = new FakeRunner((args, token) =>
            {
                if (args[0] == "load")
                { Assert.False(token.CanBeCanceled); cancellation.Cancel(); return new RegistryCommandResult(0); }
                return new RegistryCommandResult(++unloads == 1 ? 1 : 0);
            });
            var service = new RegistryHiveService(new RegistryManager(), runner, _ => false);
            var lease = await service.MountAsync(mount, source, cancellation.Token);
            Assert.Equal(mount, lease.MountPoint);
            Assert.Throws<RegistryCommandException>(() => lease.Dispose());
            lease.Dispose();
            lease.Dispose();
            Assert.Equal(2, unloads);
            Assert.Equal(new[] { "load", "unload", "unload" }, runner.Calls.Select(c => c[0]));
            var never = new FakeRunner((_, _) => throw new Exception("Must not execute"));
            var occupied = new RegistryHiveService(new RegistryManager(), never, _ => true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => occupied.MountAsync(mount, source));
            await Assert.ThrowsAsync<ArgumentException>(() => service.MountAsync(RegistryPath.Parse(@"HKCU\Invalid"), source));
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.MountAsync(mount, source, cancellation.Token));
        }
    }

    [Fact]
    public async Task WatcherSignalsRepeatedChangesAndSupportsCancellationAndDisposal()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root);
            using (var watcher = manager.Watch(fixture.Root, includeSubKeys: true))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var first = watcher.WaitForChangeAsync(timeout.Token);
                await Assert.ThrowsAsync<InvalidOperationException>(() => watcher.WaitForChangeAsync());
                manager.SetValue(fixture.Root, "A", RegistryValue.String("one"));
                await first;
                var second = watcher.WaitForChangeAsync(timeout.Token);
                manager.CreateKey(fixture.Root.Combine("Child"));
                await second;
                using (var cancellation = new CancellationTokenSource())
                {
                    var cancelled = watcher.WaitForChangeAsync(cancellation.Token);
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
                }
                var afterCancellation = watcher.WaitForChangeAsync(timeout.Token);
                manager.SetValue(fixture.Root, "A", RegistryValue.String("two"));
                await afterCancellation;
                var disposed = watcher.WaitForChangeAsync(timeout.Token);
                watcher.Dispose();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => disposed);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => watcher.WaitForChangeAsync());
            }
        }
    }

    [Fact]
    public void SecurityRoundTripsAndReadDenialIsNotReportedAsAbsence()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root);
            var original = manager.Security.Read(fixture.Root);
            var bytes = original.GetBinaryForm();
            bytes[0] = 0;
            Assert.NotEqual(bytes[0], original.GetBinaryForm()[0]);
            manager.Security.Write(fixture.Root, original, RegistrySecurityParts.Access);
            Assert.Equal(original.GetSddl(), manager.Security.Read(fixture.Root).GetSddl());
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var sid = identity.User!.Value;
                try
                {
                    manager.Security.Write(fixture.Root, RegistrySecurityDescriptor.FromSddl("D:P(D;;KR;;;" + sid + ")(A;;KA;;;" + sid + ")"), RegistrySecurityParts.Access);
                    Assert.Throws<System.Security.SecurityException>(() => manager.KeyExists(fixture.Root));
                    Assert.Throws<System.Security.SecurityException>(() => manager.TryGetValue(fixture.Root, "missing", out _));
                }
                finally { manager.Security.Write(fixture.Root, original, RegistrySecurityParts.Access); }
            }
        }
    }

    [Fact]
    public void NativeWriteFailureRetainsEarlierCompletedChanges()
    {
        using (var fixture = new RegistryFixture())
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var manager = fixture.Manager;
            var child = fixture.Root.Combine("Denied");
            manager.CreateKey(child);
            manager.SetValue(fixture.Root, "Value", RegistryValue.String("desired"));
            manager.SetValue(child, "Value", RegistryValue.String("desired"));
            var desired = manager.Snapshots.CaptureTree(fixture.Root);
            manager.SetValue(fixture.Root, "Value", RegistryValue.String("before"));
            manager.SetValue(child, "Value", RegistryValue.String("before"));
            var plan = manager.Snapshots.PlanRestore(desired);
            var original = manager.Security.Read(child);
            try
            {
                var sid = identity.User!.Value;
                manager.Security.Write(child, RegistrySecurityDescriptor.FromSddl("D:P(D;;0x2;;;" + sid + ")(A;;KA;;;" + sid + ")"), RegistrySecurityParts.Access);
                var result = manager.Snapshots.Apply(plan);
                Assert.Equal(RegistryApplyStatus.Failed, result.Status);
                Assert.IsType<System.Security.SecurityException>(result.Error);
                Assert.Single(result.Completed);
                Assert.Equal("desired", manager.GetValue(fixture.Root, "Value")!.GetString());
                Assert.Equal("before", manager.GetValue(child, "Value")!.GetString());
            }
            finally { manager.Security.Write(child, original, RegistrySecurityParts.Access); }
        }
    }

    private sealed class FakeRunner : IRegistryCommandRunner
    {
        private readonly Func<string[], CancellationToken, RegistryCommandResult> run;
        public List<string[]> Calls { get; } = new List<string[]>();
        public FakeRunner(Func<string[], CancellationToken, RegistryCommandResult> run) => this.run = run;
        public RegistryCommandResult Run(string[] arguments, CancellationToken cancellation) { Calls.Add(arguments); return run(arguments, cancellation); }
        public Task<RegistryCommandResult> RunAsync(string[] arguments, CancellationToken cancellation) => Task.FromResult(Run(arguments, cancellation));
    }
    private sealed class FileFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PSFoundation.Registry.Tests", Guid.NewGuid().ToString("N"));
        public FileFixture() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
