using System.Collections.Generic;
using Xunit;

namespace AdNoctem.Substrate.Core.Tests;

public sealed class DataWorkflowTests
{
    [Fact]
    public void OverridesUpdateOnlyExistingFieldsOfTheFirstMatchingRecord()
    {
        var first = new Dictionary<string, object?>
        {
            ["Name"] = "item",
            ["Path"] = "A",
            ["Value"] = 1,
        };
        var second = new Dictionary<string, object?>
        {
            ["Name"] = "item",
            ["Path"] = "B",
            ["Value"] = 2,
        };
        new RecordMerger().ApplyOverrides(
            new[] { first, second },
            new[]
            {
                new Dictionary<string, object?>
                {
                    ["Name"] = "ITEM",
                    ["Path"] = "B",
                    ["Value"] = 3,
                    ["Extra"] = true,
                },
            }
        );
        Assert.Equal(1, first["Value"]);
        Assert.Equal(3, second["Value"]);
        Assert.False(second.ContainsKey("Extra"));
        Assert.Equal("\"quoted\"", TextConverter.ConvertQuotes("'quoted'", QuoteStyle.Double));
    }
}
