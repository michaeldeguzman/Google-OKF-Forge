using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

public class CitationsTests
{
    [Fact]
    public void ParsesTwoOrThreeRealisticEntries()
    {
        var block =
            "[1] [FP&A reporting handbook](https://wiki.acme/finance/fpa-handbook)\n" +
            "[2] [Revenue recognition policy](https://wiki.acme/finance/revenue-recognition)\n" +
            "[3] [Cost allocation standard](https://wiki.acme/finance/cost-allocation)";

        var json = OkfParser.ParseCitationsSection(block);
        var arr = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsArray();

        Assert.Equal(3, arr.Count);
        Assert.Equal("FP&A reporting handbook", arr[0]!["title"]!.GetValue<string>());
        Assert.Equal("https://wiki.acme/finance/fpa-handbook", arr[0]!["resource"]!.GetValue<string>());
        Assert.Equal("Cost allocation standard", arr[2]!["title"]!.GetValue<string>());
        Assert.Equal("https://wiki.acme/finance/cost-allocation", arr[2]!["resource"]!.GetValue<string>());

        foreach (var entry in arr)
        {
            var obj = entry!.AsObject();
            Assert.False(obj.ContainsKey("id"));
            Assert.False(obj.ContainsKey("author"));
            Assert.False(obj.ContainsKey("usage_count"));
            Assert.False(obj.ContainsKey("last_modified"));
        }
    }

    [Fact]
    public void MalformedLine_IsSkippedNotAnError()
    {
        var block =
            "[1] [Good entry](https://example.com/good)\n" +
            "- https://example.com/bare-url-no-brackets\n" +
            "[2] [Second good entry](https://example.com/second)";

        var json = OkfParser.ParseCitationsSection(block);
        var arr = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsArray();

        Assert.Equal(2, arr.Count);
        Assert.Equal("Good entry", arr[0]!["title"]!.GetValue<string>());
        Assert.Equal("Second good entry", arr[1]!["title"]!.GetValue<string>());
    }
}
