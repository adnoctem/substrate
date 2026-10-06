using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AdNoctem.Substrate.Core.Compatibility;
using Xunit;

namespace AdNoctem.Substrate.Core.Tests;

public sealed class OperationResultTests
{
    [Fact]
    public void DistinguishesOmissionFromNullAndFalse()
    {
        var absent = OperationResult.Create("x", "set", "done", new Dictionary<string, object?>());
        Assert.Equal(new[] { "Target", "Action", "Status" }, absent.Fields.Select(p => p.Key));
        var explicitValues = OperationResult.Create(
            "x",
            "set",
            "done",
            new Dictionary<string, object?> { ["Before"] = null, ["Changed"] = false }
        );
        Assert.Null(explicitValues.Fields.Single(p => p.Key == "Before").Value);
        Assert.Equal(false, explicitValues.Fields.Single(p => p.Key == "Changed").Value);
    }

    [Fact]
    public void ExtraPropertiesCannotReplaceCoreFieldsIgnoringCase()
    {
        var result = OperationResult.Create(
            "x",
            "set",
            "done",
            new Dictionary<string, object?>(),
            new Hashtable { ["target"] = "bad", ["Extra"] = 42 }
        );
        Assert.Equal("x", result.Fields.Single(p => p.Key == "Target").Value);
        Assert.Equal(4, result.Fields.Count);
    }
}
