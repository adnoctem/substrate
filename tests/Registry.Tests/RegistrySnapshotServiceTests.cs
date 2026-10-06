using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using Xunit;

namespace AdNoctem.Substrate.Registry.Tests;

public sealed class RegistrySnapshotServiceTests
{
    [Fact]
    public void SnapshotsDistinguishAbsenceAndRejectInvalidTrees()
    {
        using (var fixture = new RegistryFixture())
        {
            var snapshots = fixture.Manager.Snapshots;
            Assert.False(snapshots.CaptureValue(fixture.Root).KeyExists);
            Assert.False(snapshots.CaptureTree(fixture.Root).Exists);
            fixture.Manager.CreateKey(fixture.Root);
            var absent = snapshots.CaptureValue(fixture.Root);
            Assert.True(absent.KeyExists);
            Assert.False(absent.Exists);
            fixture.Manager.SetValue(fixture.Root, "", RegistryValue.String(""));
            Assert.True(snapshots.CaptureValue(fixture.Root).Exists);
            Assert.Throws<ArgumentException>(() =>
                new RegistryValueSnapshot(
                    fixture.Root,
                    "",
                    fixture.Manager.View,
                    false,
                    RegistryValue.String("bad")
                )
            );
            Assert.Throws<ArgumentException>(() =>
                new RegistryTreeSnapshot(
                    fixture.Root,
                    fixture.Manager.View,
                    new[]
                    {
                        new RegistryKeySnapshot(
                            fixture.Root.Combine("MissingParent"),
                            Array.Empty<RegistryValueEntry>()
                        ),
                    }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                new RegistryKeySnapshot(
                    fixture.Root,
                    new[]
                    {
                        new RegistryValueEntry("a", RegistryValue.String("a")),
                        new RegistryValueEntry("A", RegistryValue.String("b")),
                    }
                )
            );
        }
    }

    [Theory]
    [InlineData(RegistryRestoreMode.Merge)]
    [InlineData(RegistryRestoreMode.Replace)]
    public void PlansRestoreValuesEmptyKeysAndSelectedRemoval(RegistryRestoreMode mode)
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root.Combine("Empty"));
            manager.SetValue(fixture.Root, "Value", RegistryValue.String("original"));
            var desired = manager.Snapshots.CaptureTree(fixture.Root);
            manager.SetValue(fixture.Root, "Value", RegistryValue.String("modified"));
            manager.SetValue(fixture.Root, "Extra", RegistryValue.String("extra"));
            manager.DeleteKey(fixture.Root.Combine("Empty"));
            manager.CreateKey(fixture.Root.Combine("Other"));
            var plan = manager.Snapshots.PlanRestore(desired, mode);
            Assert.Equal("modified", manager.GetValue(fixture.Root, "Value")!.GetString());
            var result = manager.Snapshots.Apply(plan);
            Assert.Equal(RegistryApplyStatus.Completed, result.Status);
            Assert.Equal("original", manager.GetValue(fixture.Root, "Value")!.GetString());
            Assert.True(manager.KeyExists(fixture.Root.Combine("Empty")));
            Assert.Equal(
                mode == RegistryRestoreMode.Merge,
                manager.ValueExists(fixture.Root, "Extra")
            );
            Assert.Equal(
                mode == RegistryRestoreMode.Merge,
                manager.KeyExists(fixture.Root.Combine("Other"))
            );
            Assert.Empty(manager.Snapshots.PlanRestore(desired, mode).Changes);
        }
    }

    [Fact]
    public void StalePlansAndMidApplyRacesNeverOverwriteInterveningValues()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root);
            manager.SetValue(fixture.Root, "A", RegistryValue.String("original"));
            manager.SetValue(fixture.Root, "B", RegistryValue.String("original"));
            var desired = manager.Snapshots.CaptureTree(fixture.Root);
            manager.SetValue(fixture.Root, "A", RegistryValue.String("changed"));
            manager.SetValue(fixture.Root, "B", RegistryValue.String("changed"));
            var plan = manager.Snapshots.PlanRestore(desired);
            manager.SetValue(fixture.Root, "Extra", RegistryValue.String("intervening"));
            Assert.Equal(RegistryApplyStatus.Conflict, manager.Snapshots.Apply(plan).Status);
            Assert.Equal("changed", manager.GetValue(fixture.Root, "A")!.GetString());
            plan = manager.Snapshots.PlanRestore(desired);
            var result = manager.Snapshots.Apply(
                plan,
                progress: new InlineProgress(_ =>
                    manager.SetValue(fixture.Root, "B", RegistryValue.String("raced"))
                )
            );
            Assert.Equal(RegistryApplyStatus.Conflict, result.Status);
            Assert.Single(result.Completed);
            Assert.Equal("raced", manager.GetValue(fixture.Root, "B")!.GetString());
        }
    }

    [Fact]
    public void CancellationRetainsCompletedWritesAndDoesNotPretendToRollBack()
    {
        using (var fixture = new RegistryFixture())
        using (var cancellation = new CancellationTokenSource())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root);
            var desired = new RegistryTreeSnapshot(
                fixture.Root,
                manager.View,
                new[]
                {
                    new RegistryKeySnapshot(
                        fixture.Root,
                        new[]
                        {
                            new RegistryValueEntry("A", RegistryValue.String("one")),
                            new RegistryValueEntry("B", RegistryValue.String("two")),
                        }
                    ),
                }
            );
            var result = manager.Snapshots.Apply(
                manager.Snapshots.PlanRestore(desired),
                cancellation.Token,
                new InlineProgress(_ => cancellation.Cancel())
            );
            Assert.Equal(RegistryApplyStatus.Cancelled, result.Status);
            Assert.Single(result.Completed);
            Assert.True(manager.ValueExists(fixture.Root, "A"));
            Assert.False(manager.ValueExists(fixture.Root, "B"));
        }
    }

    [Fact]
    public void RestoreOneValueChecksIdentityViewAndExpectedStateWithoutPruningOthers()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root);
            manager.SetValue(fixture.Root, "A", RegistryValue.String("original"));
            var original = manager.Snapshots.CaptureValue(fixture.Root, "A");
            manager.SetValue(fixture.Root, "A", RegistryValue.String("new"));
            manager.SetValue(fixture.Root, "B", RegistryValue.String("keep"));
            Assert.Equal(
                RegistryApplyStatus.Conflict,
                manager.Snapshots.RestoreValue(original, original).Status
            );
            var expected = manager.Snapshots.CaptureValue(fixture.Root, "A");
            Assert.Equal(
                RegistryApplyStatus.Completed,
                manager.Snapshots.RestoreValue(original, expected).Status
            );
            Assert.Equal("original", manager.GetValue(fixture.Root, "A")!.GetString());
            Assert.Equal("keep", manager.GetValue(fixture.Root, "B")!.GetString());
            Assert.Throws<ArgumentException>(() =>
                manager.Snapshots.RestoreValue(
                    original,
                    manager.Snapshots.CaptureValue(fixture.Root, "B")
                )
            );
            var wrongView = new RegistryManager(RegistryView.Registry32);
            Assert.Throws<ArgumentException>(() => wrongView.Snapshots.RestoreValue(original));
            var absent = new RegistryValueSnapshot(fixture.Root, "A", manager.View, true, null);
            Assert.Equal(
                RegistryApplyStatus.Completed,
                manager.Snapshots.RestoreValue(absent).Status
            );
            Assert.False(manager.ValueExists(fixture.Root, "A"));
            Assert.True(manager.ValueExists(fixture.Root, "B"));
        }
    }

    [Fact]
    public void ReplaceCanRemoveACompleteTreeAndRefusesRootHivePlans()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.SetValue(
                fixture.Root.Combine(@"Child\Grandchild"),
                "A",
                RegistryValue.String("x"),
                true
            );
            var absent = new RegistryTreeSnapshot(
                fixture.Root,
                manager.View,
                Array.Empty<RegistryKeySnapshot>()
            );
            Assert.Equal(
                RegistryApplyStatus.Completed,
                manager
                    .Snapshots.Apply(
                        manager.Snapshots.PlanRestore(absent, RegistryRestoreMode.Replace)
                    )
                    .Status
            );
            Assert.False(manager.KeyExists(fixture.Root));
            var root = new RegistryTreeSnapshot(
                new RegistryPath(RegistryHive.CurrentUser),
                manager.View,
                Array.Empty<RegistryKeySnapshot>()
            );
            Assert.Throws<InvalidOperationException>(() => manager.Snapshots.PlanRestore(root));
        }
    }

    internal sealed class InlineProgress : IProgress<RegistryChange>
    {
        private readonly Action<RegistryChange> action;

        public InlineProgress(Action<RegistryChange> action) => this.action = action;

        public void Report(RegistryChange value) => action(value);
    }

    [Fact]
    public void UnsupportedDataIntroducedDuringApplyDoesNotLoseCompletedWriteEvidence()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root);
            manager.SetValue(fixture.Root, "A", RegistryValue.String("desired"));
            manager.SetValue(fixture.Root, "B", RegistryValue.String("desired"));
            var desired = manager.Snapshots.CaptureTree(fixture.Root);
            manager.SetValue(fixture.Root, "A", RegistryValue.String("before"));
            manager.SetValue(fixture.Root, "B", RegistryValue.String("before"));
            var plan = manager.Snapshots.PlanRestore(desired);
            var result = manager.Snapshots.Apply(
                plan,
                progress: new InlineProgress(_ =>
                {
                    using (var key = manager.OpenKey(fixture.Root, true))
                        NativeRegistry.ThrowIfError(
                            RegistryManagerTests.RegSetValueExW(
                                key!.Handle,
                                "B",
                                0,
                                5,
                                new byte[] { 0, 0, 0, 1 },
                                4
                            )
                        );
                })
            );
            Assert.Equal(RegistryApplyStatus.Failed, result.Status);
            Assert.IsType<NotSupportedException>(result.Error);
            Assert.Single(result.Completed);
            Assert.Equal("desired", manager.GetValue(fixture.Root, "A")!.GetString());
        }
    }
}
