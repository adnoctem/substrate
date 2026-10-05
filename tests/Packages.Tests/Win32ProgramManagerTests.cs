using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using AdNoctem.Substrate.Registry;
using Xunit;

namespace AdNoctem.Substrate.Packages.Tests;

public sealed class Win32ProgramManagerTests
{
    [Fact]
    public void InventoryDistinguishesAbsentBlankVisibleAndHiddenRegistrations()
    {
        using (var fixture = new ProgramFixture())
        {
            Assert.Empty(fixture.Manager.GetInventory().Programs);
            fixture.Write("empty", "DisplayName", RegistryValue.String(" "));
            fixture.Write("unnamed", "Publisher", RegistryValue.String("Synthetic"));
            fixture.Write("visible", "DisplayName", RegistryValue.String("Synthetic visible"));
            fixture.Write("hidden", "DisplayName", RegistryValue.String("Synthetic hidden"));
            fixture.Write("hidden", "SystemComponent", RegistryValue.DWord(1));
            Assert.Equal("Synthetic visible", Assert.Single(fixture.Manager.GetInventory().Programs).DisplayName);
            Assert.Equal(2, fixture.Manager.GetInventory(true).Programs.Count);
            Assert.True(fixture.Manager.GetProgram(fixture.Entry("hidden"))!.IsSystemComponent);
            Assert.Null(fixture.Manager.GetProgram(fixture.Entry("absent")));
        }
    }

    [Fact]
    public void SnapshotPreservesRawTypesAndDoesNotExpandEnvironmentOrExecuteCommands()
    {
        using (var fixture = new ProgramFixture())
        {
            fixture.Write("one", "DisplayName", RegistryValue.String("Synthetic"));
            fixture.Write("one", "InstallLocation", RegistryValue.ExpandString(@"%TEMP%\synthetic"));
            fixture.Write("one", "InstallDate", RegistryValue.String("20260228"));
            fixture.Write("one", "EstimatedSize", RegistryValue.DWord(uint.MaxValue));
            fixture.Write("one", "UninstallString", RegistryValue.String("nonexistent.exe --never-execute"));
            fixture.Write("one", "DisplayVersion", RegistryValue.Binary(new byte[] { 1, 2 }));
            var program = fixture.Manager.GetProgram(fixture.Entry("one"))!;
            Assert.Equal(@"%TEMP%\synthetic", program.InstallLocation);
            Assert.Equal(new DateTime(2026, 2, 28), program.InstallDate);
            Assert.Equal(uint.MaxValue, program.EstimatedSizeKiB);
            Assert.Null(program.DisplayVersion);
            var bytes = program.Values["DisplayVersion"].GetData<byte[]>();
            bytes[0] = 9;
            Assert.Equal(1, program.Values["DisplayVersion"].GetData<byte[]>()[0]);
            fixture.Write("one", "DisplayName", RegistryValue.String("Changed"));
            Assert.Equal("Synthetic", program.DisplayName);
            Assert.Equal("nonexistent.exe --never-execute", program.UninstallCommand);
        }
    }

    [Fact]
    public void PartialInventoryIsExplicitAndRetainsInvalidDataEvidence()
    {
        using (var fixture = new ProgramFixture())
        {
            fixture.Write("bad", "DisplayName", RegistryValue.DWord(5));
            fixture.Write("good", "DisplayName", RegistryValue.String("Synthetic"));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.GetInventory());
            var partial = fixture.Manager.GetInventory(continueOnError: true);
            Assert.False(partial.IsComplete);
            Assert.Single(partial.Programs);
            var failure = Assert.Single(partial.Errors);
            Assert.Equal(fixture.Entry("bad").Path, failure.Location.Path);
            Assert.IsType<InvalidDataException>(failure.Exception);
        }
    }

    [Fact]
    public void OptionalMalformedFieldsRemainRawAndInvalidDatesAreNotInvented()
    {
        using (var fixture = new ProgramFixture())
        {
            fixture.Write("one", "DisplayName", RegistryValue.String("Synthetic"));
            fixture.Write("one", "InstallDate", RegistryValue.String("20260230"));
            fixture.Write("one", "EstimatedSize", RegistryValue.String("not a size"));
            var program = fixture.Manager.GetProgram(fixture.Entry("one"))!;
            Assert.Null(program.InstallDate);
            Assert.Null(program.EstimatedSizeKiB);
            Assert.Equal("not a size", program.Values["estimatedsize"].GetString());
        }
    }

    [Fact]
    public void LocationsAreCopiedViewsResolveAndDuplicateRootsDoNotDuplicatePrograms()
    {
        using (var fixture = new ProgramFixture())
        {
            fixture.Write("one", "DisplayName", RegistryValue.String("Synthetic"));
            var roots = new[] { fixture.Location, fixture.Location };
            var manager = new Win32ProgramManager(roots);
            roots[0] = new ProgramRegistryLocation(fixture.Root.Combine("missing"), RegistryView.Default, ProgramRegistrationScope.Other);
            Assert.Equal(fixture.Root, manager.Locations[0].Path);
            Assert.NotEqual(RegistryView.Default, manager.Locations[0].View);
            Assert.Single(manager.GetInventory().Programs);
            Assert.Throws<ArgumentException>(() => new Win32ProgramManager(new ProgramRegistryLocation[] { null! }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgramRegistryLocation(fixture.Root, (RegistryView)999, ProgramRegistrationScope.Other));
        }
    }

    [Fact]
    public async Task AsyncInventoryIsCancellableAndMatchesSynchronousReads()
    {
        using (var fixture = new ProgramFixture())
        {
            fixture.Write("one", "DisplayName", RegistryValue.String("Synthetic"));
            Assert.Equal("Synthetic", Assert.Single((await fixture.Manager.GetInventoryAsync()).Programs).DisplayName);
            Assert.Equal("Synthetic", (await fixture.Manager.GetProgramAsync(fixture.Entry("one")))!.DisplayName);
            var cancelled = new CancellationToken(true);
            Assert.Throws<OperationCanceledException>(() => fixture.Manager.GetInventory(cancellationToken: cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Manager.GetInventoryAsync(cancellationToken: cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Manager.GetProgramAsync(fixture.Entry("one"), cancelled));
        }
    }

    [Fact]
    public void DefaultInventoryUsesExplicitMachineViewsAndNoPowerShellDependency()
    {
        var manager = new Win32ProgramManager();
        var machine = manager.Locations.Where(root => root.Scope == ProgramRegistrationScope.Machine).ToArray();
        Assert.Equal(Environment.Is64BitOperatingSystem ? 2 : 1, machine.Length);
        Assert.Contains(machine, root => root.View == RegistryView.Registry32);
        if (Environment.Is64BitOperatingSystem)
            Assert.Contains(machine, root => root.View == RegistryView.Registry64);
        Assert.Single(manager.Locations, root => root.Scope == ProgramRegistrationScope.CurrentUser);
        Assert.DoesNotContain(typeof(Win32ProgramManager).Assembly.GetReferencedAssemblies(), assembly => assembly.Name!.Contains("PowerShell") || assembly.Name.Contains("Management.Automation"));
    }
}

internal sealed class ProgramFixture : IDisposable
{
    internal RegistryPath Root { get; } = RegistryPath.Parse(@"HKCU\Software\AdNoctem.Substrate.Tests\" + Guid.NewGuid().ToString("N"));
    internal RegistryManager Registry { get; } = new RegistryManager();
    internal ProgramRegistryLocation Location { get; }
    internal Win32ProgramManager Manager { get; }
    internal ProgramFixture()
    {
        Location = new ProgramRegistryLocation(Root, Registry.View, ProgramRegistrationScope.Other);
        Manager = new Win32ProgramManager(new[] { Location });
    }
    internal ProgramRegistryLocation Entry(string name) => new ProgramRegistryLocation(Root.Combine(name), Registry.View, ProgramRegistrationScope.Other);
    internal void Write(string entry, string name, RegistryValue value) => Registry.SetValue(Root.Combine(entry), name, value, true);
    public void Dispose() => Registry.DeleteKey(Root, true);
}
