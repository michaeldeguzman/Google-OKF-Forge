using System.Text.Json.Nodes;
using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

/// <summary>
/// Targeted unit tests for the two YamlSubset.cs gaps found in block-style OKF
/// bundles (see okf-parser-fix-prompt.md): a block sequence whose items sit at
/// the same indent as their own key (column 0 relative to the key), and a plain
/// scalar that folds onto an indented continuation line.
/// </summary>
public class BlockStyleConstructTests
{
    [Fact]
    public void BlockSequence_ItemsAtColumnZero_Parses()
    {
        string frontmatter = "tags:\n- join\n- posts\n- votes\n";

        var fm = OkfParser.ParseFrontmatter(frontmatter);

        Assert.Equal("", fm.ParseError);
        Assert.Equal("join,posts,votes", fm.TagsCsv);
    }

    [Fact]
    public void BlockSequenceOfMappings_ItemsAtColumnZero_Parses()
    {
        string frontmatter =
            "sources:\n" +
            "- resource: https://meta.stackexchange.com/questions/2677/database-schema-documentation\n" +
            "  id: meta_schema_doc\n" +
            "  title: Database schema documentation for the public data dump and SEDE\n";

        var fm = OkfParser.ParseFrontmatter(frontmatter);

        Assert.Equal("", fm.ParseError);
        var sources = JsonNode.Parse(fm.SourcesJson)!.AsArray();
        Assert.Single(sources);
        Assert.Equal("meta_schema_doc", sources[0]!["id"]!.GetValue<string>());
        Assert.Equal("Database schema documentation for the public data dump and SEDE", sources[0]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void FoldedScalar_TwoLines_JoinsWithSingleSpace()
    {
        string frontmatter =
            "description: Google Analytics 4 event-level daily sharded export tables containing\n" +
            "  user interaction logs.\n";

        var fm = OkfParser.ParseFrontmatter(frontmatter);

        Assert.Equal("", fm.ParseError);
        Assert.Equal(
            "Google Analytics 4 event-level daily sharded export tables containing user interaction logs.",
            fm.Description);
    }

    [Fact]
    public void FoldedScalar_ThreeLines_JoinsAllWithSingleSpace()
    {
        string frontmatter =
            "description: This dataset contains a public archive of Stack Overflow data, including\n" +
            "  posts, users, and tags. It was last updated on 2022-11-25 and is no longer actively\n" +
            "  updated.\n";

        var fm = OkfParser.ParseFrontmatter(frontmatter);

        Assert.Equal("", fm.ParseError);
        Assert.Equal(
            "This dataset contains a public archive of Stack Overflow data, including posts, users, and tags. It was last updated on 2022-11-25 and is no longer actively updated.",
            fm.Description);
    }

    [Fact]
    public void FoldedScalar_FollowedByNextKey_DoesNotSwallowIt()
    {
        string frontmatter =
            "description: wraps onto one continuation line\n" +
            "  and stops here.\n" +
            "status: stable\n";

        var fm = OkfParser.ParseFrontmatter(frontmatter);

        Assert.Equal("", fm.ParseError);
        Assert.Equal("wraps onto one continuation line and stops here.", fm.Description);
        Assert.Equal("stable", fm.Status);
    }

    [Fact]
    public void TagsAsPlainScalar_NormalizesToTrimmedNoSpaceCsv()
    {
        string frontmatter = "tags: Stack Overflow, Q&A, developer, programming, public dataset\n";

        var fm = OkfParser.ParseFrontmatter(frontmatter);

        Assert.Equal("", fm.ParseError);
        Assert.Equal("Stack Overflow,Q&A,developer,programming,public dataset", fm.TagsCsv);
    }
}
