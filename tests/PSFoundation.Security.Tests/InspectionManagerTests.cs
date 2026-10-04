using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using PSFoundation.Windows;
using Xunit;

namespace PSFoundation.Security.Tests;

public sealed class InspectionManagerTests
{
    [Fact]
    public async Task InspectionDetachesCimDataAndCancelsBeforeDefenderMutation()
    {
        CimRecord snapshot;
        using (var source = new CimInstance("Synthetic"))
        {
            source.CimInstanceProperties.Add(CimProperty.Create("Names", new[] { "one", "two" }, CimType.StringArray, CimFlags.None));
            snapshot = CimRecord.Capture(source);
        }
        Assert.Equal(new[] { "one", "two" }, ((IEnumerable<object>)snapshot.GetValue("Names")!).Cast<string>());
        Assert.Equal("https://www.microsoft.com/en-us/wdsi/threats/threat/Trojan%3aWin32%2fSynthetic+Name", DefenderManager.GetThreatDescriptionUrl("Trojan:Win32/Synthetic Name"));
        var token = new CancellationToken(true);
        var defender = new DefenderManager();
        Assert.Throws<OperationCanceledException>(() => defender.AddExclusions(DefenderExclusionKind.Path, new[] { @"C:\NeverChanged" }, TimeSpan.FromSeconds(15), token));
        Assert.Throws<OperationCanceledException>(() => defender.RemoveExclusions(DefenderExclusionKind.Path, new[] { @"C:\NeverChanged" }, TimeSpan.FromSeconds(15), token));
        var inventory = await new WmiPersistenceManager().ReadAsync(TimeSpan.FromSeconds(15), continueOnError: true);
        Assert.Equal(inventory.Errors.Count == 0, inventory.IsComplete);
        Assert.All(inventory.EventFilters.Concat(inventory.Consumers).Concat(inventory.Bindings), record => Assert.NotEmpty(record.ClassName));
    }
}
