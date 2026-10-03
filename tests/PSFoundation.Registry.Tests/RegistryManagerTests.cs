using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Xunit;

namespace PSFoundation.Registry.Tests;

public sealed class RegistryManagerTests
{
    [Fact]
    public void LiteralPathsHaveNeutralIdentityAndDoNotInterpretDotOrWildcardNames()
    {
        var path = RegistryPath.Parse(@"HKCU\Software\\Synthetic\");
        Assert.Equal(@"HKEY_CURRENT_USER\Software\Synthetic", path.ToString());
        Assert.Equal(path, new RegistryPath(RegistryHive.CurrentUser, @"software\SYNTHETIC"));
        Assert.Equal(path.GetHashCode(), RegistryPath.Parse(@"HKCU\SOFTWARE\synthetic").GetHashCode());
        Assert.Equal(@"Software\Synthetic\..\*", path.Combine(@"..\*").SubKey);
        Assert.True(path.Combine("Child").IsDescendantOf(path));
        Assert.False(path.IsDescendantOf(path));
        Assert.False(RegistryPath.Parse(@"HKCU\Software\Synthetically").IsDescendantOf(path));
        Assert.Equal("Software", path.Parent!.Name);
        Assert.Null(new RegistryPath(RegistryHive.Users).Parent);
        Assert.False(RegistryPath.TryParse("Registry::HKCU:\\X", out _));
        Assert.False(RegistryPath.TryParse(null, out _));
        Assert.Throws<ArgumentException>(() => path.Combine(@"\Other"));
        Assert.Throws<ArgumentException>(() => RegistryPath.Parse("HKCU\\x\0y"));
    }

    [Fact]
    public void TypedValuesAreImmutableAndValidateTheirRepresentation()
    {
        var bytes = new byte[] { 1, 255 };
        var value = RegistryValue.Binary(bytes);
        bytes[0] = 8;
        var read = value.GetData<byte[]>();
        read[0] = 9;
        Assert.Equal(new byte[] { 1, 255 }, value.GetData<byte[]>());
        var strings = new[] { "A", "b" };
        var multi = RegistryValue.MultiString(strings);
        strings[0] = "X";
        Assert.Equal(new[] { "A", "b" }, multi.GetData<string[]>());
        Assert.Equal(-1, RegistryValue.DWord(uint.MaxValue).GetData<int>());
        Assert.Equal(-1L, RegistryValue.QWord(ulong.MaxValue).GetData<long>());
        Assert.NotEqual(RegistryValue.String("A"), RegistryValue.String("a"));
        Assert.NotEqual(RegistryValue.String("A"), RegistryValue.ExpandString("A"));
        Assert.Equal(RegistryValue.MultiString("a", "b"), RegistryValue.MultiString("a", "b"));
        Assert.Throws<ArgumentException>(() => new RegistryValue(RegistryValueKind.DWord, 1L));
        Assert.Throws<ArgumentException>(() => RegistryValue.MultiString(""));
        Assert.Throws<ArgumentException>(() => RegistryValue.String("a\0b"));
        Assert.Throws<InvalidCastException>(() => value.GetData<string>());
    }

    [Theory]
    [InlineData(RegistryView.Registry32)]
    [InlineData(RegistryView.Registry64)]
    public void DirectConsumerRoundTripsAllSupportedKindsAndDefaultValues(RegistryView view)
    {
        using (var fixture = new RegistryFixture(view))
        {
            var manager = fixture.Manager;
            Assert.True(manager.CreateKey(fixture.Root));
            Assert.False(manager.CreateKey(fixture.Root));
            var values = new[] { RegistryValue.String(""), RegistryValue.ExpandString("%TEMP%\\raw"), RegistryValue.DWord(-1), RegistryValue.QWord(-1L),
                RegistryValue.Binary(new byte[] { 0, 255 }), RegistryValue.MultiString(), RegistryValue.MultiString("Alpha", "Beta"), new RegistryValue(RegistryValueKind.None, new byte[] { 9 }) };
            for (var index = 0; index < values.Length; index++)
            {
                var name = index == 0 ? "" : index.ToString();
                manager.SetValue(fixture.Root, name, values[index]);
                Assert.Equal(values[index], manager.GetValue(fixture.Root, name));
            }
            Assert.Equal(values.Length, manager.GetValues(fixture.Root).Count);
            Assert.False(manager.TryGetValue(fixture.Root, "missing", out var absent));
            Assert.Null(absent);
            Assert.Equal("%TEMP%\\raw", manager.GetValue(fixture.Root, "1")!.GetString());
            Assert.Equal(Environment.ExpandEnvironmentVariables("%TEMP%\\raw"), manager.GetValue(fixture.Root, "1")!.GetString(true));
            Assert.True(manager.DeleteValue(fixture.Root));
            Assert.False(manager.DeleteValue(fixture.Root));
            var missing = fixture.Root.Combine("Missing");
            Assert.Throws<IOException>(() => manager.SetValue(missing, "X", RegistryValue.String("x")));
            Assert.False(manager.KeyExists(missing));
            manager.SetValue(missing, "X", RegistryValue.String("x"), createKey: true);
            using (var handle = manager.OpenKey(missing))
                Assert.Equal("x", handle!.GetValue("X"));
            Assert.Throws<InvalidOperationException>(() => manager.DeleteKey(new RegistryPath(RegistryHive.CurrentUser), true));
            Assert.Throws<InvalidOperationException>(() => manager.DeleteKey(fixture.Root));
            Assert.True(manager.DeleteKey(fixture.Root, true));
            Assert.False(manager.DeleteKey(fixture.Root, true));
        }
    }

    [Fact]
    public async Task TraversalAndSearchRespectDepthTargetsAndCancellation()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            manager.CreateKey(fixture.Root.Combine(@"Alpha\Nested"));
            manager.SetValue(fixture.Root, "AlphaValue", RegistryValue.MultiString("one", "ALPHA"));
            manager.SetValue(fixture.Root, "Bytes", RegistryValue.Binary(new byte[] { 0xab, 0xcd }));
            Assert.Single(manager.EnumerateKeys(fixture.Root, 0));
            Assert.Equal(2, manager.EnumerateKeys(fixture.Root, 1).Count());
            var matches = await manager.SearchAsync(fixture.Root, "alpha");
            Assert.Equal(2, matches.Count);
            Assert.Contains(matches, m => m.Matched == (RegistrySearchTargets.ValueNames | RegistrySearchTargets.ValueData));
            Assert.Single(manager.Search(fixture.Root, "ABCD", new RegistrySearchOptions(RegistrySearchTargets.ValueData)));
            Assert.Empty(manager.Search(fixture.Root, "alpha", new RegistrySearchOptions(caseSensitive: true)));
            Assert.Throws<IOException>(() => manager.GetSubKeys(fixture.Root.Combine("Missing")));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Throws<OperationCanceledException>(() => manager.EnumerateKeys(fixture.Root, cancellationToken: cancelled.Token).ToArray());
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.SearchAsync(fixture.Root, "x", cancellationToken: cancelled.Token));
            }
        }
    }

    [Fact]
    public void RenameCopyAndMoveHaveExplicitDestinationAndOverlapRules()
    {
        using (var fixture = new RegistryFixture())
        {
            var manager = fixture.Manager;
            var source = fixture.Root.Combine("Source");
            var target = fixture.Root.Combine("Target");
            manager.CreateKey(source.Combine("Empty"));
            manager.SetValue(source, "Name", RegistryValue.String("original"));
            Assert.Equal(RegistryApplyStatus.Completed, manager.CopyKey(source, target).Status);
            Assert.True(manager.KeyExists(target.Combine("Empty")));
            Assert.True(manager.KeyExists(source));
            Assert.Throws<IOException>(() => manager.CopyKey(source, target));
            Assert.Throws<ArgumentException>(() => manager.CopyKey(source, source.Combine("Recursive")));
            manager.SetValue(target, "Extra", RegistryValue.String("keep"));
            Assert.Equal(RegistryApplyStatus.Completed, manager.CopyKey(source, target, RegistryCopyMode.Merge).Status);
            Assert.True(manager.ValueExists(target, "Extra"));
            Assert.Equal(RegistryApplyStatus.Completed, manager.CopyKey(source, target, RegistryCopyMode.Replace).Status);
            Assert.False(manager.ValueExists(target, "Extra"));
            var renamed = manager.RenameKey(target, "Renamed");
            Assert.False(manager.KeyExists(target));
            Assert.True(manager.KeyExists(renamed));
            var moved = manager.MoveKey(renamed, fixture.Root.Combine("Moved"));
            Assert.True(moved.SourceRemoved);
            Assert.False(manager.KeyExists(renamed));
            Assert.Equal("original", manager.GetValue(fixture.Root.Combine("Moved"), "Name")!.GetString());
        }
    }

    [Fact]
    public void ReusableSurfaceContainsNoCompatibilityOrHostContracts()
    {
        var assembly = typeof(RegistryManager).Assembly;
        Assert.DoesNotContain(assembly.GetExportedTypes(), type => type.Namespace!.Contains("Compatibility"));
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name!.Contains("PowerShell") || reference.Name.Contains("Management.Automation"));
        Assert.DoesNotContain(assembly.GetExportedTypes().SelectMany(t => t.GetMethods()), method => method.Name == "get_ProviderPath");
        Assert.Throws<ArgumentException>(() => new RegistryManager(machineName: @"\\host"));
        var remote = new RegistryManager(RegistryView.Registry64, "synthetic.invalid");
        Assert.Throws<NotSupportedException>(() => remote.OpenKey(new RegistryPath(RegistryHive.CurrentUser)));
        Assert.Throws<NotSupportedException>(() => remote.Watch(new RegistryPath(RegistryHive.LocalMachine)));
        Assert.Throws<NotSupportedException>(() => remote.Files.Export(new RegistryPath(RegistryHive.LocalMachine), "unused.reg"));
    }

    [Fact]
    public void UnsupportedNativeKindsAreNeverMisrepresentedAsMissing()
    {
        using (var fixture = new RegistryFixture())
        {
            fixture.Manager.CreateKey(fixture.Root);
            using (var key = fixture.Manager.OpenKey(fixture.Root, true))
                NativeRegistry.ThrowIfError(RegSetValueExW(key!.Handle, "BigEndian", 0, 5, new byte[] { 0, 0, 0, 1 }, 4));
            Assert.True(fixture.Manager.ValueExists(fixture.Root, "BigEndian"));
            Assert.Throws<NotSupportedException>(() => fixture.Manager.GetValue(fixture.Root, "BigEndian"));
        }
    }

    [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true)]
    internal static extern int RegSetValueExW(Microsoft.Win32.SafeHandles.SafeRegistryHandle key, string name, int reserved, int kind, byte[] value, int size);
}

internal sealed class RegistryFixture : IDisposable
{
    public RegistryManager Manager { get; }
    public RegistryPath Root { get; } = new RegistryPath(RegistryHive.CurrentUser, @"Software\PSFoundation.Tests\" + Guid.NewGuid().ToString("N"));
    public RegistryFixture(RegistryView view = RegistryView.Registry64) => Manager = new RegistryManager(view);
    public void Dispose() => Manager.DeleteKey(Root, true);
}
