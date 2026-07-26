using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

public class BodySectionTests
{
    [Fact]
    public void HeadingPresentFollowedByAnotherHeading()
    {
        var body = "# Computation\n\nSELECT 1\n\n# Notes\n\nSome notes.";
        var section = OkfParser.ExtractBodySection(body, "Computation");
        Assert.Equal("\nSELECT 1\n", section);
    }

    [Fact]
    public void HeadingPresentAtEndOfDocument()
    {
        var body = "# Intro\n\nHello.\n\n# Citations\n\n[1] [Title](https://example.com)";
        var section = OkfParser.ExtractBodySection(body, "Citations");
        Assert.Equal("\n[1] [Title](https://example.com)", section);
    }

    [Fact]
    public void HeadingAbsent_ReturnsEmpty()
    {
        var body = "# Intro\n\nNo such section here.";
        var section = OkfParser.ExtractBodySection(body, "Computation");
        Assert.Equal("", section);
    }

    [Fact]
    public void NestedDeeperHeadings_AreIncludedNotTerminating()
    {
        var body = "# Schema\n\n## Columns\n\norder_id\n\n## Notes\n\nmore detail\n\n# Joins\n\nnext section";
        var section = OkfParser.ExtractBodySection(body, "Schema");
        Assert.Contains("## Columns", section);
        Assert.Contains("order_id", section);
        Assert.Contains("## Notes", section);
        Assert.Contains("more detail", section);
        Assert.DoesNotContain("# Joins", section);
        Assert.DoesNotContain("next section", section);
    }

    [Fact]
    public void SameLevelHeadingTerminatesSection()
    {
        var body = "## Alpha\ncontent-a\n## Beta\ncontent-b";
        var section = OkfParser.ExtractBodySection(body, "Alpha");
        Assert.Equal("content-a", section);
    }
}
