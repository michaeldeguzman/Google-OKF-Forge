using System.Text.Json;
using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

/// <summary>
/// The single most important test in the suite (per the task spec): parse every
/// file in the real, published acme_retail v0.2 bundle (pulled directly from
/// GoogleCloudPlatform/knowledge-catalog's okf/bundles/acme_retail), serialise
/// each one back out via SerializeConcept, re-parse the serialised output, and
/// assert the two ParsedFrontmatter results are equal field-by-field.
/// </summary>
public class RoundTripTests
{
    private static OkfFileEntry[] LoadAcmeRetailBundle()
    {
        var bytes = TestHelpers.ZipDirectory(TestHelpers.FixturePath("acme_retail"));
        return OkfParser.UnzipBundle(bytes);
    }

    [Fact]
    public void AcmeRetailBundle_HasExpectedFileCount()
    {
        var entries = LoadAcmeRetailBundle();
        // 17 .md files in the real bundle (viz.html and the .py attester are dropped).
        Assert.Equal(17, entries.Length);
    }

    public static IEnumerable<object[]> AcmeRetailMdFilesWithFrontmatter()
    {
        foreach (var entry in LoadAcmeRetailBundle())
        {
            var split = OkfParser.SplitFrontmatter(entry.Content);
            if (split.HasFrontmatter)
                yield return new object[] { entry.Path };
        }
    }

    [Theory]
    [MemberData(nameof(AcmeRetailMdFilesWithFrontmatter))]
    public void EveryConcept_RoundTripsThroughSerializeConcept(string path)
    {
        var entries = LoadAcmeRetailBundle();
        var entry = entries.Single(e => e.Path == path);

        var split = OkfParser.SplitFrontmatter(entry.Content);
        Assert.True(split.HasFrontmatter, $"{path}: expected frontmatter");
        var original = OkfParser.ParseFrontmatter(split.FrontmatterRaw);
        Assert.Equal("", original.ParseError);

        string conceptJson = JsonSerializer.Serialize(new
        {
            type = original.Type,
            title = original.Title,
            description = original.Description,
            resource = original.Resource,
            tagsCsv = original.TagsCsv,
            status = original.Status,
            staleAfter = original.StaleAfter,
            generatedBy = original.GeneratedBy,
            generatedAt = original.GeneratedAt,
            verifiedJson = original.VerifiedJson,
            sourcesJson = original.SourcesJson,
            extraJson = original.ExtraJson,
            body = split.Body
        });

        string serialized = OkfParser.SerializeConcept(conceptJson);

        var reSplit = OkfParser.SplitFrontmatter(serialized);
        Assert.True(reSplit.HasFrontmatter, $"{path}: serialized output lost its frontmatter block");
        var reparsed = OkfParser.ParseFrontmatter(reSplit.FrontmatterRaw);

        TestHelpers.AssertParsedFrontmatterEqual(original, reparsed, path);
        Assert.Equal(split.Body, reSplit.Body);
    }

    [Fact]
    public void OrdersTable_ParsesAllExpectedFields()
    {
        var entries = LoadAcmeRetailBundle();
        var entry = entries.Single(e => e.Path == "tables/orders.md");
        var split = OkfParser.SplitFrontmatter(entry.Content);
        var fm = OkfParser.ParseFrontmatter(split.FrontmatterRaw);

        Assert.Equal("BigQuery Table", fm.Type);
        Assert.Equal("Customer Orders", fm.Title);
        Assert.Equal("sales,orders,revenue", fm.TagsCsv);
        Assert.Equal("reference_agent/gemini-2.5-pro", fm.GeneratedBy);
        Assert.Equal("2026-06-30T14:00:00Z", fm.GeneratedAt);
        Assert.Equal("stable", fm.Status);
        Assert.Equal("2026-12-31", fm.StaleAfter);

        var verified = System.Text.Json.Nodes.JsonNode.Parse(fm.VerifiedJson)!.AsArray();
        Assert.Single(verified);
        Assert.Equal("human:kliu@acme", verified[0]!["by"]!.GetValue<string>());

        var sources = System.Text.Json.Nodes.JsonNode.Parse(fm.SourcesJson)!.AsArray();
        Assert.Equal(2, sources.Count);
        Assert.Equal("warehouse-schema", sources[0]!["id"]!.GetValue<string>());
        Assert.Equal("1240", sources[0]!["usage_count"]!.GetValue<string>());

        // usage_window is a sibling of sources, not in the recognised key list, so it lands in ExtraJson.
        var extra = System.Text.Json.Nodes.JsonNode.Parse(fm.ExtraJson)!.AsObject();
        Assert.True(extra.ContainsKey("usage_window"));
        Assert.Equal("2026-04-01", extra["usage_window"]!["from"]!.GetValue<string>());
    }

    [Fact]
    public void AttestedComputation_ExtraKeysPreserveRuntimeExecutorAttester()
    {
        var entries = LoadAcmeRetailBundle();
        var entry = entries.Single(e => e.Path == "computations/revenue-ytd.md");
        var split = OkfParser.SplitFrontmatter(entry.Content);
        var fm = OkfParser.ParseFrontmatter(split.FrontmatterRaw);

        Assert.Equal("Attested Computation", fm.Type);
        var extra = System.Text.Json.Nodes.JsonNode.Parse(fm.ExtraJson)!.AsObject();
        Assert.Equal("bigquery", extra["runtime"]!.GetValue<string>());
        Assert.Equal("skills/run-on-bq.md", extra["executor"]!["resource"]!.GetValue<string>());
        Assert.Equal("attesters/sql_equality.py", extra["attester"]!["resource"]!.GetValue<string>());
        var parameters = extra["parameters"]!.AsArray();
        Assert.Single(parameters);
        Assert.Equal("year", parameters[0]!["name"]!.GetValue<string>());
        Assert.Equal("integer", parameters[0]!["type"]!.GetValue<string>());
        Assert.Equal("true", parameters[0]!["required"]!.GetValue<string>());
    }

    [Fact]
    public void ExtractLinks_WorksAcrossRealBodies()
    {
        var entries = LoadAcmeRetailBundle();
        var revenueDoc = entries.Single(e => e.Path == "metrics/revenue.md");
        var split = OkfParser.SplitFrontmatter(revenueDoc.Content);
        var links = OkfParser.ExtractLinks(split.Body);

        // The real body links via "../computations/revenue-ytd.md" -- resolving ".." against
        // the containing directory is explicitly the caller's job per the spec, so ExtractLinks
        // only strips the ".md" suffix and leaves the relative path otherwise untouched.
        Assert.Contains(links, l => l.TargetPath == "../computations/revenue-ytd" && !l.IsCrossBundle);
    }
}
