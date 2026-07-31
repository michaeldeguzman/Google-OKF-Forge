using System.Text.Json;
using System.Text.Json.Nodes;
using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

/// <summary>
/// Corpus-level acceptance tests for the YamlSubset.cs block-style fixes
/// (okf-parser-fix-prompt.md), run against all four canonical Google bundles:
/// acme_retail, crypto_bitcoin, ga4, stackoverflow (53 concept files total).
///
/// Fixtures:
/// - Fixtures/acme_retail (pre-existing, shared with RoundTripTests)
/// - Fixtures/canonical_bundles/{crypto_bitcoin,ga4,stackoverflow}
/// - Fixtures/canonical_bundles/expected-frontmatter.json (six golden files)
/// - Fixtures/canonical_bundles/expected-folded-scalars.txt (29 folded descriptions)
///
/// Gate 1: every concept file parses with no ParseError.
/// Gate 2: differential comparison against golden values, byte-identical for folded scalars.
/// Gate 3: acme_retail's flow-style regression cases keep routing to ExtraJson.
/// Gate 4: parse -> serialize -> reparse round trip across all 53 files.
/// </summary>
public class CanonicalBundleFixtureTests
{
    private static readonly string[] BundleNames = { "acme_retail", "crypto_bitcoin", "ga4", "stackoverflow" };

    private static readonly Dictionary<string, int> ExpectedConceptCounts = new()
    {
        ["acme_retail"] = 9,
        ["crypto_bitcoin"] = 9,
        ["ga4"] = 9,
        ["stackoverflow"] = 26,
    };

    private static string BundleFixturePath(string bundleName) =>
        bundleName == "acme_retail"
            ? TestHelpers.FixturePath("acme_retail")
            : TestHelpers.FixturePath("canonical_bundles", bundleName);

    private static OkfFileEntry[] LoadBundle(string bundleName) =>
        OkfParser.UnzipBundle(TestHelpers.ZipDirectory(BundleFixturePath(bundleName)));

    private static IEnumerable<OkfFileEntry> ConceptFiles(string bundleName) =>
        LoadBundle(bundleName).Where(e => e.ReservedKind == "");

    private static (FrontmatterSplitResult split, ParsedFrontmatter fm) ParseEntry(string bundleName, string path)
    {
        var entry = LoadBundle(bundleName).Single(e => e.Path == path);
        var split = OkfParser.SplitFrontmatter(entry.Content);
        var fm = OkfParser.ParseFrontmatter(split.FrontmatterRaw);
        return (split, fm);
    }

    public static IEnumerable<object[]> AllBundles() => BundleNames.Select(b => new object[] { b });

    // ---- Gate 1: every concept file parses, zero ParseError ----

    [Theory]
    [MemberData(nameof(AllBundles))]
    public void Gate1_EveryConceptFile_ParsesWithoutError(string bundleName)
    {
        var concepts = ConceptFiles(bundleName).ToList();
        Assert.Equal(ExpectedConceptCounts[bundleName], concepts.Count);

        var failures = new List<string>();
        foreach (var entry in concepts)
        {
            var split = OkfParser.SplitFrontmatter(entry.Content);
            Assert.True(split.HasFrontmatter, $"{bundleName}/{entry.Path}: expected frontmatter");
            var fm = OkfParser.ParseFrontmatter(split.FrontmatterRaw);
            if (fm.ParseError.Length > 0)
                failures.Add($"{entry.Path}: {fm.ParseError}");
        }
        Assert.True(failures.Count == 0, $"{bundleName} had parse failures:\n" + string.Join("\n", failures));
    }

    // ---- Gate 2: differential comparison against golden values ----

    private static readonly Lazy<JsonObject> GoldenFrontmatter = new(() =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(TestHelpers.FixturePath("canonical_bundles", "expected-frontmatter.json")))!);

    private static readonly HashSet<string> RecognizedGoldenKeys = new()
    {
        "type", "title", "description", "resource", "status", "stale_after", "tags", "generated", "sources", "verified"
    };

    /// <summary>
    /// The golden fixture was produced with a reference YAML parser. On the two
    /// REGRESSION fixtures below, that reference parser applied YAML's implicit
    /// timestamp resolver to the *unquoted* flow-style "at" scalars and
    /// re-rendered them in a different textual form (space separator, "+00:00"
    /// instead of "Z"). This project's parser deliberately never infers
    /// datetimes -- see YamlSubset.cs's doc comment, the explicit "Do not start
    /// inferring ... dates" instruction in okf-parser-fix-prompt.md, and the
    /// already-passing RoundTripTests.OrdersTable_ParsesAllExpectedFields, which
    /// asserts literal pass-through ("2026-06-30T14:00:00Z") for the same
    /// unquoted-flow-style pattern. These two entries are therefore compared
    /// against the literal source text, not the golden JSON's reformatted value.
    /// </summary>
    private static readonly Dictionary<(string file, string field), string> KnownGoldenDateArtifacts = new()
    {
        [("acme_retail/metrics/gross-margin.md", "generated.at")] = "2026-06-30T14:00:00Z",
        [("acme_retail/metrics/gross-margin.md", "verified[0].at")] = "2026-07-01T09:00:00Z",
        [("acme_retail/computations/gross-margin-period.md", "generated.at")] = "2026-06-30T14:00:00Z",
        [("acme_retail/computations/gross-margin-period.md", "verified[0].at")] = "2026-07-01T09:00:00Z",
    };

    public static IEnumerable<object[]> GoldenFrontmatterCases() =>
        GoldenFrontmatter.Value.Select(kv => new object[] { kv.Key });

    [Theory]
    [MemberData(nameof(GoldenFrontmatterCases))]
    public void Gate2_MatchesGoldenFrontmatter(string fullPath)
    {
        int slash = fullPath.IndexOf('/');
        string bundleName = fullPath.Substring(0, slash);
        string relativePath = fullPath.Substring(slash + 1);

        var (_, actual) = ParseEntry(bundleName, relativePath);
        Assert.Equal("", actual.ParseError);

        var expected = (JsonObject)GoldenFrontmatter.Value[fullPath]!["expected"]!;
        AssertMatchesGolden(fullPath, expected, actual);
    }

    private static void AssertMatchesGolden(string fullPath, JsonObject expected, ParsedFrontmatter actual)
    {
        string ExpectedStr(string key) => expected[key] is JsonValue v ? v.GetValue<string>() : "";

        Assert.True(ExpectedStr("type") == actual.Type, $"{fullPath}: type. expected='{ExpectedStr("type")}' actual='{actual.Type}'");
        Assert.True(ExpectedStr("title") == actual.Title, $"{fullPath}: title. expected='{ExpectedStr("title")}' actual='{actual.Title}'");
        Assert.True(ExpectedStr("description") == actual.Description, $"{fullPath}: description. expected='{ExpectedStr("description")}' actual='{actual.Description}'");
        Assert.True(ExpectedStr("resource") == actual.Resource, $"{fullPath}: resource. expected='{ExpectedStr("resource")}' actual='{actual.Resource}'");
        Assert.True(ExpectedStr("status") == actual.Status, $"{fullPath}: status. expected='{ExpectedStr("status")}' actual='{actual.Status}'");
        Assert.True(ExpectedStr("stale_after") == actual.StaleAfter, $"{fullPath}: stale_after. expected='{ExpectedStr("stale_after")}' actual='{actual.StaleAfter}'");

        string expectedTagsCsv = expected["tags"] switch
        {
            JsonArray arr => string.Join(",", arr.Select(n => (n as JsonValue)?.GetValue<string>() ?? "")),
            JsonValue v => v.GetValue<string>(),
            _ => "",
        };
        Assert.True(expectedTagsCsv == actual.TagsCsv, $"{fullPath}: tags. expected='{expectedTagsCsv}' actual='{actual.TagsCsv}'");

        string expectedGeneratedBy = "";
        string expectedGeneratedAt = "";
        if (expected["generated"] is JsonObject genObj)
        {
            expectedGeneratedBy = genObj["by"] is JsonValue bv ? bv.GetValue<string>() : "";
            expectedGeneratedAt = genObj["at"] is JsonValue av ? av.GetValue<string>() : "";
        }
        if (KnownGoldenDateArtifacts.TryGetValue((fullPath, "generated.at"), out var overrideGenAt))
            expectedGeneratedAt = overrideGenAt;
        Assert.True(expectedGeneratedBy == actual.GeneratedBy, $"{fullPath}: generated.by. expected='{expectedGeneratedBy}' actual='{actual.GeneratedBy}'");
        Assert.True(expectedGeneratedAt == actual.GeneratedAt, $"{fullPath}: generated.at. expected='{expectedGeneratedAt}' actual='{actual.GeneratedAt}'");

        var expectedSources = (expected["sources"] as JsonArray)?.DeepClone() as JsonArray ?? new JsonArray();
        AssertJsonEquivalent(fullPath, "sources", expectedSources, JsonNode.Parse(actual.SourcesJson));

        var expectedVerified = (expected["verified"] as JsonArray)?.DeepClone() as JsonArray ?? new JsonArray();
        if (expectedVerified.Count > 0 && KnownGoldenDateArtifacts.TryGetValue((fullPath, "verified[0].at"), out var overrideVerifiedAt))
            expectedVerified[0]!["at"] = overrideVerifiedAt;
        AssertJsonEquivalent(fullPath, "verified", expectedVerified, JsonNode.Parse(actual.VerifiedJson));

        var expectedExtra = new JsonObject();
        foreach (var kv in expected)
        {
            if (RecognizedGoldenKeys.Contains(kv.Key)) continue;
            expectedExtra[kv.Key] = kv.Value?.DeepClone();
        }
        AssertJsonEquivalent(fullPath, "extra", expectedExtra, JsonNode.Parse(actual.ExtraJson));
    }

    private static void AssertJsonEquivalent(string context, string field, JsonNode? expected, JsonNode? actual)
    {
        Assert.True(
            JsonDeepEquals(expected, actual),
            $"{context}: {field} differs.\nexpected={expected?.ToJsonString()}\nactual={actual?.ToJsonString()}");
    }

    private static bool JsonDeepEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is JsonValue av && b is JsonValue bv) return av.GetValue<string>() == bv.GetValue<string>();
        if (a is JsonArray aa && b is JsonArray ba)
        {
            if (aa.Count != ba.Count) return false;
            for (int i = 0; i < aa.Count; i++)
                if (!JsonDeepEquals(aa[i], ba[i])) return false;
            return true;
        }
        if (a is JsonObject ao && b is JsonObject bo)
        {
            if (ao.Count != bo.Count) return false;
            foreach (var kv in ao)
            {
                if (!bo.TryGetPropertyValue(kv.Key, out var bv2)) return false;
                if (!JsonDeepEquals(kv.Value, bv2)) return false;
            }
            return true;
        }
        return false;
    }

    // ---- Gate 2 (folded scalars): byte-identical, no trimming/normalising ----

    public static IEnumerable<object[]> FoldedScalarCases()
    {
        var lines = File.ReadAllLines(TestHelpers.FixturePath("canonical_bundles", "expected-folded-scalars.txt"));
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            string path = lines[i].Trim();
            string descLine = lines[i + 1];
            string raw = descLine.Substring(descLine.IndexOf('=') + 1).Trim();
            string value = raw.Substring(1, raw.Length - 2); // strip the single or double quote repr() chose
            yield return new object[] { path, value };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedScalarCases))]
    public void Gate2_FoldedScalar_ByteIdentical(string fullPath, string expectedDescription)
    {
        int slash = fullPath.IndexOf('/');
        string bundleName = fullPath.Substring(0, slash);
        string relativePath = fullPath.Substring(slash + 1);

        var (_, actual) = ParseEntry(bundleName, relativePath);
        Assert.Equal("", actual.ParseError);
        Assert.Equal(expectedDescription, actual.Description);
    }

    // ---- Gate 3: acme_retail regression (flow style + extension-key routing) ----
    // Both files are also covered generically by Gate2_MatchesGoldenFrontmatter;
    // these two are kept as dedicated, narrowly-named tests for traceability
    // against the fix prompt's explicit Gate 3 requirement.

    [Fact]
    public void Gate3_AcmeRetail_GrossMargin_NotExtensionKeyPreservedIntact()
    {
        var (_, fm) = ParseEntry("acme_retail", "metrics/gross-margin.md");
        Assert.Equal("", fm.ParseError);

        var extra = (JsonObject)JsonNode.Parse(fm.ExtraJson)!;
        Assert.True(extra.ContainsKey("not"));
        var notArr = extra["not"]!.AsArray();
        Assert.Single(notArr);
        Assert.Equal("revenue minus product cost only", notArr[0]!["term"]!.GetValue<string>());
        Assert.Equal(
            "revenue minus full COGS (product cost + inbound fulfillment + outbound shipping + payment fees)",
            notArr[0]!["instead"]!.GetValue<string>());
    }

    [Fact]
    public void Gate3_AcmeRetail_GrossMarginPeriod_AttestedComputationKeysPreserved()
    {
        var (_, fm) = ParseEntry("acme_retail", "computations/gross-margin-period.md");
        Assert.Equal("", fm.ParseError);

        var extra = (JsonObject)JsonNode.Parse(fm.ExtraJson)!;
        Assert.Equal("bigquery", extra["runtime"]!.GetValue<string>());
        Assert.Equal("skills/run-on-bq.md", extra["executor"]!["resource"]!.GetValue<string>());
        Assert.Equal("attesters/sql_equality.py", extra["attester"]!["resource"]!.GetValue<string>());

        var parameters = extra["parameters"]!.AsArray();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("period_start", parameters[0]!["name"]!.GetValue<string>());
        Assert.Equal("period_end", parameters[1]!["name"]!.GetValue<string>());
    }

    // ---- Gate 4: parse -> serialize -> reparse round trip, all 53 files ----

    /// <summary>
    /// These 8 stackoverflow files write "tags" as a plain comma-separated
    /// scalar (e.g. "tags: stackoverflow, posts, questions") instead of a YAML
    /// list. OKF SPEC.md Â§4.1 defines tags as "a YAML list of short strings",
    /// so this is a producer error in the source files, not a construct this
    /// library needs to preserve byte-for-byte. SerializeConcept always emits
    /// tags as a proper flow sequence, which is more spec-conformant than the
    /// input -- so for exactly these 8 files, Gate 4 asserts the round trip
    /// converges on the normalised (no-space, list-shaped) TagsCsv rather than
    /// literal input equality. Every other field on these files, and every
    /// field (including TagsCsv) on the other 45 concept files, is still
    /// asserted with full strict equality.
    /// </summary>
    private static readonly HashSet<string> TagsNormalizedOnEmit = new()
    {
        "stackoverflow/datasets/stackoverflow.md",
        "stackoverflow/tables/users.md",
        "stackoverflow/tables/posts_moderator_nomination.md",
        "stackoverflow/tables/votes.md",
        "stackoverflow/tables/posts_questions.md",
        "stackoverflow/tables/stackoverflow_posts.md",
        "stackoverflow/tables/posts_wiki_placeholder.md",
        "stackoverflow/tables/posts_answers.md",
    };

    public static IEnumerable<object[]> AllConceptFileCases()
    {
        foreach (var bundleName in BundleNames)
            foreach (var entry in ConceptFiles(bundleName))
                yield return new object[] { bundleName, entry.Path };
    }

    [Theory]
    [MemberData(nameof(AllConceptFileCases))]
    public void Gate4_RoundTrip_ParseSerializeReparse_PreservesFields(string bundleName, string path)
    {
        var entry = LoadBundle(bundleName).Single(e => e.Path == path);
        var split = OkfParser.SplitFrontmatter(entry.Content);
        Assert.True(split.HasFrontmatter, $"{bundleName}/{path}: expected frontmatter");
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
        Assert.True(reSplit.HasFrontmatter, $"{bundleName}/{path}: serialized output lost its frontmatter block");
        var reparsed = OkfParser.ParseFrontmatter(reSplit.FrontmatterRaw);

        string fullPath = $"{bundleName}/{path}";
        var expectedForComparison = original;
        if (TagsNormalizedOnEmit.Contains(fullPath))
            expectedForComparison.TagsCsv = string.Join(",", original.TagsCsv.Split(',').Select(t => t.Trim()));

        TestHelpers.AssertParsedFrontmatterEqual(expectedForComparison, reparsed, fullPath);
        Assert.Equal(split.Body, reSplit.Body);
    }
}
