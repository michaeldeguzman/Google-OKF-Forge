using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

/// <summary>
/// TagsCsv contract: a comma-separated list of trimmed, non-empty tag values,
/// independent of the YAML form (flow sequence, block sequence, plain scalar)
/// it was authored in. Flow and block sequences already trimmed each item
/// before this fix (YamlSubset.cs's ParseFlowSequence/ParseBlockSequence); the
/// plain-scalar path in OkfParser.Frontmatter.cs's tags case did not, so the
/// same logical tags produced a different TagsCsv depending purely on source
/// style. See okf-tags-normalisation-prompt.md.
/// </summary>
public class TagsNormalizationTests
{
    [Theory]
    [InlineData("tags: [a, b, c]")]
    [InlineData("tags:\n- a\n- b\n- c")]
    [InlineData("tags: a, b, c")]
    public void AllThreeForms_ProduceIdenticalTagsCsv(string frontmatter)
    {
        var fm = OkfParser.ParseFrontmatter(frontmatter);

        Assert.Equal("", fm.ParseError);
        Assert.Equal("a,b,c", fm.TagsCsv);
    }

    [Fact]
    public void PlainScalar_DoubleComma_DropsEmptySegment()
    {
        var fm = OkfParser.ParseFrontmatter("tags: a,,b");

        Assert.Equal("", fm.ParseError);
        Assert.Equal("a,b", fm.TagsCsv);
    }

    [Fact]
    public void PlainScalar_TrailingComma_DropsEmptySegment()
    {
        var fm = OkfParser.ParseFrontmatter("tags: a, b,");

        Assert.Equal("", fm.ParseError);
        Assert.Equal("a,b", fm.TagsCsv);
    }

    [Fact]
    public void StackoverflowUsers_YieldsExactNormalizedCsv()
    {
        var fm = OkfParser.ParseFrontmatter("tags: stackoverflow, users, community, reputation");

        Assert.Equal("", fm.ParseError);
        Assert.Equal("stackoverflow,users,community,reputation", fm.TagsCsv);
    }

    [Fact]
    public void StackoverflowDataset_InternalSpaceInTagValueSurvives()
    {
        var fm = OkfParser.ParseFrontmatter("tags: Stack Overflow, Q&A, developer, programming, public dataset");

        Assert.Equal("", fm.ParseError);
        // "Stack Overflow" keeps its internal space -- only the comma-delimiters are trimmed.
        Assert.Equal("Stack Overflow,Q&A,developer,programming,public dataset", fm.TagsCsv);
    }
}
