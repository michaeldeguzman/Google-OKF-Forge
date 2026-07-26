using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

public class FrontmatterEdgeCaseTests
{
    [Fact]
    public void SplitFrontmatter_NoFrontmatterAtAll()
    {
        var result = OkfParser.SplitFrontmatter("# Just a heading\n\nSome text.");
        Assert.False(result.HasFrontmatter);
        Assert.Equal("# Just a heading\n\nSome text.", result.Body);
        Assert.Equal("", result.FrontmatterRaw);
        Assert.Equal("", result.ParseError);
    }

    [Fact]
    public void SplitFrontmatter_OpeningWithNoClosing()
    {
        var content = "---\ntype: Metric\ntitle: Something\n\n# Body starts but frontmatter never closed";
        var result = OkfParser.SplitFrontmatter(content);
        Assert.False(result.HasFrontmatter);
        Assert.Equal(content, result.Body);
        Assert.NotEqual("", result.ParseError);
    }

    [Fact]
    public void SplitFrontmatter_LeadingByteOrderMark()
    {
        string content = "﻿---\ntype: Metric\n---\nBody text.";
        var result = OkfParser.SplitFrontmatter(content);
        // The BOM is stripped by UnzipBundle before content reaches SplitFrontmatter,
        // so here it becomes the first character of the (non-blank) first line and
        // that line no longer equals "---" -- SplitFrontmatter degrades gracefully.
        Assert.False(result.HasFrontmatter);

        string stripped = content.TrimStart('﻿');
        var strippedResult = OkfParser.SplitFrontmatter(stripped);
        Assert.True(strippedResult.HasFrontmatter);
        Assert.Equal("Body text.", strippedResult.Body);
    }

    [Fact]
    public void ParseFrontmatter_EmptyTypeValue()
    {
        var fm = OkfParser.ParseFrontmatter("type:\ntitle: Something");
        Assert.Equal("", fm.Type);
        Assert.Equal("Something", fm.Title);
        Assert.Equal("", fm.ParseError);
    }

    [Fact]
    public void ParseFrontmatter_UnterminatedFlowMapping_SetsParseError()
    {
        var fm = OkfParser.ParseFrontmatter("type: Metric\ngenerated: { by: human:x, at: 2026-01-01T00:00:00Z\ntitle: Never reached");
        Assert.Equal("Metric", fm.Type); // parsed before the failure
        Assert.Equal("", fm.Title);      // never reached
        Assert.NotEqual("", fm.ParseError);
    }

    [Fact]
    public void ParseFrontmatter_UnknownKeyWithDeeplyNestedBlockSequence_SurvivesInExtraJsonAndRoundTrips()
    {
        var raw = "type: Metric\n" +
                  "not:\n" +
                  "  - term: \"revenue minus product cost only\"\n" +
                  "    why: \"that is the pre-FY2026 definition\"\n" +
                  "    instead: \"revenue minus full COGS\"\n";
        var fm = OkfParser.ParseFrontmatter(raw);
        Assert.Equal("", fm.ParseError);

        var extra = System.Text.Json.Nodes.JsonNode.Parse(fm.ExtraJson)!.AsObject();
        Assert.True(extra.ContainsKey("not"));
        var notArr = extra["not"]!.AsArray();
        Assert.Single(notArr);
        var item = notArr[0]!.AsObject();
        Assert.Equal("revenue minus product cost only", item["term"]!.GetValue<string>());
        Assert.Equal("that is the pre-FY2026 definition", item["why"]!.GetValue<string>());
        Assert.Equal("revenue minus full COGS", item["instead"]!.GetValue<string>());

        // Round-trip through SerializeConcept -> SplitFrontmatter -> ParseFrontmatter.
        var conceptJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = fm.Type,
            title = "",
            description = "",
            resource = "",
            tagsCsv = "",
            status = "",
            staleAfter = "",
            generatedBy = "",
            generatedAt = "",
            verifiedJson = fm.VerifiedJson,
            sourcesJson = fm.SourcesJson,
            extraJson = fm.ExtraJson,
            body = "body text"
        });
        var serialized = OkfParser.SerializeConcept(conceptJson);
        var split = OkfParser.SplitFrontmatter(serialized);
        Assert.True(split.HasFrontmatter);
        var reparsed = OkfParser.ParseFrontmatter(split.FrontmatterRaw);
        TestHelpers.AssertParsedFrontmatterEqual(fm, reparsed, "unknown-key-round-trip");
    }
}
