using System;
using AdNoctem.Substrate.Windows;
using Xunit;

namespace AdNoctem.Substrate.Windows.Tests;

public sealed class PrerequisiteWorkflowTests
{
    [Fact]
    public void ReportRetainsAllChecksWhenDiscoveryFails()
    {
        var result = new PrerequisiteEvaluator().Evaluate(new[] {
            new PrerequisiteRequirement("Build", "fixture", 200, () => 201, a => (int)a! >= 200, "Update"),
            new PrerequisiteRequirement("Command", "fixture", true, () => false, a => (bool)a!, "Install"),
            new PrerequisiteRequirement("Service", "fixture", true, () => throw new InvalidOperationException("Synthetic failure"), a => true, "Inspect") });
        Assert.False(result.Applicable);
        Assert.Equal(3, result.Checks.Count);
        Assert.Equal("Satisfied", result.Checks[0].Reason);
        Assert.Equal("RequirementNotMet", result.Checks[1].Reason);
        Assert.Equal("DiscoveryFailed", result.Checks[2].Reason);
    }
}
