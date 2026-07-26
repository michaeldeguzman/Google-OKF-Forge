using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

public class FrontmatterConstructTests
{
    [Fact]
    public void Scalar_ParsesTypeTitleDescriptionResourceStatus()
    {
        var fm = OkfParser.ParseFrontmatter("type: BigQuery Table\ntitle: Customer Orders\ndescription: One row per order.\nresource: https://example.com/x\nstatus: stable");
        Assert.Equal("BigQuery Table", fm.Type);
        Assert.Equal("Customer Orders", fm.Title);
        Assert.Equal("One row per order.", fm.Description);
        Assert.Equal("https://example.com/x", fm.Resource);
        Assert.Equal("stable", fm.Status);
        Assert.Equal("", fm.ParseError);
    }

    [Fact]
    public void QuotedScalar_StripsQuotesForOkfVersion()
    {
        var fm = OkfParser.ParseFrontmatter("okf_version: \"0.2\"");
        Assert.Equal("0.2", fm.OkfVersion);
    }

    [Fact]
    public void FlowSequence_TagsBecomeCommaSeparated()
    {
        var fm = OkfParser.ParseFrontmatter("tags: [sales, orders, revenue]");
        Assert.Equal("sales,orders,revenue", fm.TagsCsv);
    }

    [Fact]
    public void FlowMapping_GeneratedByAndAt()
    {
        var fm = OkfParser.ParseFrontmatter("generated: { by: reference_agent/gemini-2.5-pro, at: 2026-06-30T14:00:00Z }");
        Assert.Equal("reference_agent/gemini-2.5-pro", fm.GeneratedBy);
        Assert.Equal("2026-06-30T14:00:00Z", fm.GeneratedAt);
    }

    [Fact]
    public void BlockSequenceOfFlowMappings_Verified()
    {
        var fm = OkfParser.ParseFrontmatter("verified:\n  - { by: human:kliu@acme, at: 2026-07-01T16:00:00Z }\n  - { by: process:finance-nightly, at: 2026-07-02T02:00:00Z }");
        var arr = System.Text.Json.Nodes.JsonNode.Parse(fm.VerifiedJson)!.AsArray();
        Assert.Equal(2, arr.Count);
        Assert.Equal("human:kliu@acme", arr[0]!["by"]!.GetValue<string>());
        Assert.Equal("2026-07-01T16:00:00Z", arr[0]!["at"]!.GetValue<string>());
        Assert.Equal("process:finance-nightly", arr[1]!["by"]!.GetValue<string>());
    }

    [Fact]
    public void BareVerifiedMapping_NormalizesToOneElementList()
    {
        var fm = OkfParser.ParseFrontmatter("verified: { by: human:ahormati, at: 2026-06-25T09:00:00Z }");
        var arr = System.Text.Json.Nodes.JsonNode.Parse(fm.VerifiedJson)!.AsArray();
        Assert.Single(arr);
        Assert.Equal("human:ahormati", arr[0]!["by"]!.GetValue<string>());
    }

    [Fact]
    public void BlockSequenceOfBlockMappings_Sources()
    {
        var raw = "sources:\n  - id: x\n    resource: y\n    title: z\n    author: team:data-platform\n    usage_count: 1240\n    last_modified: 2026-06-15";
        var fm = OkfParser.ParseFrontmatter(raw);
        var arr = System.Text.Json.Nodes.JsonNode.Parse(fm.SourcesJson)!.AsArray();
        Assert.Single(arr);
        var entry = arr[0]!.AsObject();
        Assert.Equal("x", entry["id"]!.GetValue<string>());
        Assert.Equal("y", entry["resource"]!.GetValue<string>());
        Assert.Equal("z", entry["title"]!.GetValue<string>());
        Assert.Equal("team:data-platform", entry["author"]!.GetValue<string>());
        Assert.Equal("1240", entry["usage_count"]!.GetValue<string>());
        Assert.Equal("2026-06-15", entry["last_modified"]!.GetValue<string>());
    }

    [Fact]
    public void SourcesEntry_OmitsAbsentFieldsRatherThanNull()
    {
        var raw = "sources:\n  - id: minimal\n    resource: https://example.com";
        var fm = OkfParser.ParseFrontmatter(raw);
        var entry = System.Text.Json.Nodes.JsonNode.Parse(fm.SourcesJson)!.AsArray()[0]!.AsObject();
        Assert.Equal(2, entry.Count);
        Assert.False(entry.ContainsKey("title"));
        Assert.False(entry.ContainsKey("author"));
        Assert.False(entry.ContainsKey("usage_count"));
        Assert.False(entry.ContainsKey("last_modified"));
    }

    [Fact]
    public void BareDateScalar_StaleAfter()
    {
        var fm = OkfParser.ParseFrontmatter("stale_after: 2026-12-31");
        Assert.Equal("2026-12-31", fm.StaleAfter);
    }

    [Fact]
    public void BareDatetimeScalar_LegacyTimestamp()
    {
        var fm = OkfParser.ParseFrontmatter("timestamp: 2026-05-28T00:00:00Z");
        Assert.Equal("2026-05-28T00:00:00Z", fm.LegacyTimestamp);
    }
}
