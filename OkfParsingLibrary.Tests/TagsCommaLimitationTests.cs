using System.Text.Json;
using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

/// <summary>
/// TagsCsv behaviour for tag values containing a literal comma. Parsing was
/// already quote-aware (SplitTopLevel/FindMatchingBracket in YamlSubset.cs
/// don't split on a comma inside quotes), so a quoted flow/block tag with an
/// embedded comma always parsed correctly as one tag. SerializeConcept's emit
/// side didn't match that: it re-split TagsCsv on every comma with no
/// quote-awareness, silently turning the one tag into two on any round trip.
/// See okf-tag-emit-quoting-prompt.md.
///
/// The plain-scalar form (`tags: hello, world`) remains genuinely unable to
/// express a comma-containing tag -- under that convention the comma IS the
/// delimiter, so `hello, world` unavoidably means two tags. That is inherent
/// to the form, not a defect, and is unaffected by this fix.
/// </summary>
public class TagsCommaLimitationTests
{
    [Fact]
    public void FlowSequence_QuotedCommaTag_ParsesAsSingleTagWithCommaIntact()
    {
        var fm = OkfParser.ParseFrontmatter("tags: [\"hello, world\"]");

        Assert.Equal("", fm.ParseError);
        Assert.Equal("hello, world", fm.TagsCsv);
    }

    [Fact]
    public void BlockSequence_QuotedCommaTag_ParsesAsSingleTagWithCommaIntact()
    {
        var fm = OkfParser.ParseFrontmatter("tags:\n- \"hello, world\"");

        Assert.Equal("", fm.ParseError);
        Assert.Equal("hello, world", fm.TagsCsv);
    }

    [Fact]
    public void PlainScalar_UnquotedCommaTag_SplitsIntoTwoTags()
    {
        var fm = OkfParser.ParseFrontmatter("tags: hello, world");

        Assert.Equal("", fm.ParseError);
        // Inherent to the plain-scalar form: the comma IS the delimiter here,
        // there is no quoting convention that lets it mean anything else.
        Assert.Equal("hello,world", fm.TagsCsv);
    }

    private static string RoundTripTagsCsv(string tagsCsv)
    {
        string conceptJson = JsonSerializer.Serialize(new
        {
            type = "",
            title = "",
            description = "",
            resource = "",
            tagsCsv,
            status = "",
            staleAfter = "",
            generatedBy = "",
            generatedAt = "",
            verifiedJson = "[]",
            sourcesJson = "[]",
            extraJson = "{}",
            body = ""
        });

        string serialized = OkfParser.SerializeConcept(conceptJson);
        var reSplit = OkfParser.SplitFrontmatter(serialized);
        Assert.True(reSplit.HasFrontmatter);
        var reparsed = OkfParser.ParseFrontmatter(reSplit.FrontmatterRaw);
        Assert.Equal("", reparsed.ParseError);
        return reparsed.TagsCsv;
    }

    private static string ParseThenRoundTrip(string frontmatter)
    {
        var original = OkfParser.ParseFrontmatter(frontmatter);
        Assert.Equal("", original.ParseError);
        return RoundTripTagsCsv(original.TagsCsv);
    }

    [Fact]
    public void FlowSequence_QuotedCommaTag_RoundTrip_SurvivesAsSingleTag()
    {
        string result = ParseThenRoundTrip("tags: [\"hello, world\"]");
        Assert.Equal("hello, world", result);
    }

    [Fact]
    public void BlockSequence_QuotedCommaTag_RoundTrip_SurvivesAsSingleTag()
    {
        string result = ParseThenRoundTrip("tags:\n- \"hello, world\"");
        Assert.Equal("hello, world", result);
    }

    [Fact]
    public void MultipleTagsWithOneCommaContainingTag_RoundTrip_PreservesAllThree()
    {
        // "a" and "d" are plain delimiter-separated tags; "b, c" is one tag
        // with its own embedded comma. TagsCsv for this shape (as ParseFrontmatter
        // would build it from `tags: [a, "b, c", d]`) is "a,b, c,d" -- the
        // only reliable signal left for where the real delimiters are is that
        // a genuine delimiter is never followed by a space (every real tag is
        // pre-trimmed before being joined with a bare comma).
        string result = RoundTripTagsCsv("a,b, c,d");
        Assert.Equal("a,b, c,d", result);
    }

    [Fact]
    public void TagWithLeadingAndTrailingSpace_RoundTrip_Survives()
    {
        // Only reachable via an explicitly-quoted flow/block item, since
        // Unquote strips the surrounding quote characters but never trims
        // the content inside them (e.g. `tags: [" hi ", world]`).
        string result = RoundTripTagsCsv(" hi ,world");
        Assert.Equal(" hi ,world", result);
    }

    [Fact]
    public void TagStartingWithQuoteCharacter_RoundTrip_Survives()
    {
        string result = RoundTripTagsCsv("\"quoted-looking,world");
        Assert.Equal("\"quoted-looking,world", result);
    }

    [Fact]
    public void EmptyTagBetweenTwoOthers_RoundTrip_Survives()
    {
        string result = RoundTripTagsCsv("a,,b");
        Assert.Equal("a,,b", result);
    }

    [Fact]
    public void EmbeddedCommaWithNoFollowingSpace_ParsesIndistinguishablyFromTwoTags()
    {
        var fm = OkfParser.ParseFrontmatter("tags: [\"hello,world\"]");

        Assert.Equal("", fm.ParseError);
        Assert.Equal("hello,world", fm.TagsCsv);
    }

    [Fact]
    public void EmbeddedCommaWithNoFollowingSpace_RoundTrip()
    {
        string result = ParseThenRoundTrip("tags: [\"hello,world\"]");
        Assert.Equal("hello,world", result);
    }

    [Fact]
    public void TrailingCommaInsideQuotedTag_ParsesIndistinguishablyFromEmptyTrailingTag()
    {
        var fm = OkfParser.ParseFrontmatter("tags: [\"foo,\"]");

        Assert.Equal("", fm.ParseError);
        Assert.Equal("foo,", fm.TagsCsv);
    }

    [Fact]
    public void TrailingCommaInsideQuotedTag_RoundTrip()
    {
        string result = ParseThenRoundTrip("tags: [\"foo,\"]");
        Assert.Equal("foo,", result);
    }
}
