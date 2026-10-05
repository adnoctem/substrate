using AdNoctem.Substrate.Registry.Compatibility;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Xunit;

namespace AdNoctem.Substrate.Registry.Tests;

public sealed class RegistryStateTests
{
    private static RegistryState State(object? value, bool exists = true, RegistryValueKind kind = RegistryValueKind.String) =>
        new RegistryState(LegacyRegistryPath.Parse(@"HKCU\Software\Synthetic"), "Value", RegistryView.Registry32, exists, kind, value);

    [Fact]
    public void EqualityIncludesCaseOrderKindAndAbsence()
    {
        Assert.False(State("A").SameValue(State("a")));
        Assert.False(State(new[] { "A", "B" }, kind: RegistryValueKind.MultiString).SameValue(State(new[] { "B", "A" }, kind: RegistryValueKind.MultiString)));
        Assert.False(State("A").SameValue(State("A", kind: RegistryValueKind.ExpandString)));
        Assert.True(State(null, false).SameValue(State("ignored", false)));
        Assert.False(State(null, false).SameValue(State("")));
        Assert.True(State(new byte[] { 0, 255 }, kind: RegistryValueKind.Binary).SameValue(State(new byte[] { 0, 255 }, kind: RegistryValueKind.Binary)));
    }

    [Theory]
    [InlineData(false, false, "Skipped")]
    [InlineData(true, false, "Conflict")]
    [InlineData(false, true, "Restored")]
    public void ChecksExpectedAndAuthorizationBeforeWriting(bool checkExpected, bool allowed, string status)
    {
        var store = new FakeStore(State("before"));
        var result = new RegistryStateService(store).Restore(State("after"), checkExpected, null, (_, _) => allowed);
        Assert.Equal(status, result.Status);
        Assert.Equal(status == "Restored" ? 1 : 0, store.Writes);
    }

    [Fact]
    public void DetectsRaceAfterConfirmationWithoutWriting()
    {
        var store = new FakeStore(State("before"));
        var result = new RegistryStateService(store).Restore(State("after"), false, null, (_, _) => { store.Current = State("intervening"); return true; });
        Assert.Equal("Conflict", result.Status);
        Assert.Equal("intervening", result.After.Value);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void AlreadyCompliantDoesNotPromptOrRequireExpectedEntry()
    {
        var store = new FakeStore(State("same"));
        var result = new RegistryStateService(store).Restore(State("same"), true, null, (_, _) => throw new Exception("Must not prompt"));
        Assert.Equal("AlreadyCompliant", result.Status);
        Assert.True(result.AlreadyCompliant);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void FailedVerificationCannotBeReportedAsRestored()
    {
        var store = new FakeStore(State("before")) { IgnoreWrite = true };
        var result = new RegistryStateService(store).Restore(State("after"), false, null, (_, _) => true);
        Assert.Equal("Failed", result.Status);
        Assert.Contains("verification failed", result.Error);
        Assert.False(result.Changed);
    }

    [Fact]
    public void ReadDenialIsNotTreatedAsAbsence()
    {
        var store = new FakeStore(State("before")) { DenyRead = true };
        Assert.Throws<UnauthorizedAccessException>(() => new RegistryStateService(store).Compare(State("after")));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void CancellationAndHostControlFlowAreNotConvertedIntoFailedResults()
    {
        var store = new FakeStore(State("before")) { WriteFailure = new OperationCanceledException() };
        Assert.Throws<OperationCanceledException>(() => new RegistryStateService(store).Restore(State("after"), false, null, (_, _) => true));
        store.WriteFailure = new InvalidOperationException("Synthetic host stop");
        Assert.Throws<InvalidOperationException>(() => new RegistryStateService(store, _ => false).Restore(State("after"), false, null, (_, _) => true));
    }

    [Theory]
    [InlineData(RegistryView.Registry32)]
    [InlineData(RegistryView.Registry64)]
    public void NativeStateRoundTripsAllKindsAndRecreatesOnlySelectedValues(RegistryView view)
    {
        var relative = @"Software\AdNoctem.Substrate.Tests\" + Guid.NewGuid().ToString("N");
        var path = LegacyRegistryPath.Parse("HKCU\\" + relative);
        var store = new RegistryStore();
        var data = new Dictionary<RegistryValueKind, object>
        {
            [RegistryValueKind.String] = "",
            [RegistryValueKind.ExpandString] = @"%TEMP%\raw",
            [RegistryValueKind.DWord] = -1,
            [RegistryValueKind.QWord] = (long)4294967296,
            [RegistryValueKind.Binary] = new byte[] { 0, 255, 7 },
            [RegistryValueKind.None] = new byte[] { 1, 2 },
            [RegistryValueKind.MultiString] = new[] { "A", "b" }
        };
        using (var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view))
        {
            try
            {
                foreach (var pair in data)
                {
                    var desired = new RegistryState(path, pair.Key.ToString(), view, true, pair.Key, pair.Value);
                    store.Apply(desired);
                    var read = store.Read(path, desired.Name, view);
                    Assert.True(read.KeyExists);
                    Assert.True(desired.SameValue(read));
                    Assert.Equal(view, read.View);
                }
                var absent = new RegistryState(path, "String", view, false, null, null);
                store.Apply(absent);
                Assert.False(store.Read(path, "String", view).Exists);
                Assert.True(store.Read(path, "Binary", view).Exists);
                root.DeleteSubKeyTree(relative);
                var restore = new RegistryStateService(store).Restore(new RegistryState(path, "", view, true, RegistryValueKind.String, "default"), false, null, (_, _) => true);
                Assert.Equal("Restored", restore.Status);
                Assert.Equal("default", store.Read(path, "", view).Value);
            }
            finally { root.DeleteSubKeyTree(relative, false); }
        }
    }

    [Fact]
    public void DomainAssemblyHasNoPowerShellDependency()
    {
        Assert.DoesNotContain(typeof(RegistryStateService).Assembly.GetReferencedAssemblies(), reference =>
            reference.Name!.Contains("PowerShell") || reference.Name.Contains("Management.Automation"));
        Assert.DoesNotContain(typeof(RegistryStateService).Assembly.GetTypes(), type => type.BaseType?.FullName == "System.Management.Automation.PSCmdlet");
    }

    private sealed class FakeStore : IRegistryStateStore
    {
        public RegistryState Current;
        public int Writes;
        public bool IgnoreWrite;
        public bool DenyRead;
        public Exception? WriteFailure;
        public FakeStore(RegistryState current) => Current = current;
        public RegistryState Read(LegacyRegistryPath path, string name, RegistryView view) => DenyRead ? throw new UnauthorizedAccessException("Synthetic denial") : Current;
        public void Apply(RegistryState desired)
        {
            if (WriteFailure != null)
                throw WriteFailure;
            Writes++;
            if (!IgnoreWrite)
                Current = desired;
        }
    }
}
