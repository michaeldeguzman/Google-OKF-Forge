using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

public class ConformanceTests
{
    [Fact]
    public void NoFrontmatter_ProducesSpec91Error()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "loose.md", Content = "# No frontmatter here\n\nJust prose.", ReservedKind = "" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Contains(issues, i => i.FilePath == "loose.md" && i.Rule == "SPEC-9.1" && i.Severity == "error");
    }

    [Fact]
    public void EmptyType_ProducesSpec92Error()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "empty-type.md", Content = "---\ntype:\ntitle: Something\n---\nbody", ReservedKind = "" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Contains(issues, i => i.FilePath == "empty-type.md" && i.Rule == "SPEC-9.2" && i.Severity == "error");
    }

    [Fact]
    public void UnrecognisedType_ProducesSpec41Warning()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "policy.md", Content = "---\ntype: Policy\ntitle: Something\n---\nbody", ReservedKind = "" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Contains(issues, i => i.FilePath == "policy.md" && i.Rule == "SPEC-4.1" && i.Severity == "warning");
    }

    [Fact]
    public void RecognisedType_ProducesNoIssues()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "orders.md", Content = "---\ntype: BigQuery Table\ntitle: Orders\n---\nbody", ReservedKind = "" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Empty(issues);
    }

    [Fact]
    public void BundleRootIndexWithOnlyOkfVersion_ProducesNoIssue()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "index.md", Content = "---\nokf_version: \"0.2\"\n---\n# Subdirectories", ReservedKind = "index" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Empty(issues);
    }

    [Fact]
    public void BundleRootIndexWithExtraFields_ProducesSpec6Warning()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "index.md", Content = "---\nokf_version: \"0.2\"\ntitle: Not allowed here\n---\n# Subdirectories", ReservedKind = "index" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Contains(issues, i => i.FilePath == "index.md" && i.Rule == "SPEC-6" && i.Severity == "warning");
    }

    [Fact]
    public void NonRootIndexWithFrontmatter_ProducesSpec6Warning()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "tables/index.md", Content = "---\nokf_version: \"0.2\"\n---\n# Tables", ReservedKind = "index" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Contains(issues, i => i.FilePath == "tables/index.md" && i.Rule == "SPEC-6" && i.Severity == "warning");
    }

    [Fact]
    public void NonRootIndexWithoutFrontmatter_ProducesNoIssue()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "tables/index.md", Content = "# Tables\n\n* [Orders](orders.md)", ReservedKind = "index" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Empty(issues);
    }

    [Fact]
    public void LogFile_NeverProducesIssues()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "log.md", Content = "no frontmatter, just prose", ReservedKind = "log" }
        };
        var issues = OkfParser.ValidateConformance(files);
        Assert.Empty(issues);
    }

    [Fact]
    public void MixedBundle_ProducesExactExpectedIssueSet()
    {
        var files = new[]
        {
            new OkfFileEntry { Path = "no-frontmatter.md", Content = "just prose", ReservedKind = "" },
            new OkfFileEntry { Path = "empty-type.md", Content = "---\ntype:\n---\nbody", ReservedKind = "" },
            new OkfFileEntry { Path = "unrecognised-type.md", Content = "---\ntype: Wombat\n---\nbody", ReservedKind = "" },
            new OkfFileEntry { Path = "index.md", Content = "---\nokf_version: \"0.2\"\n---\n# Root", ReservedKind = "index" }
        };

        var issues = OkfParser.ValidateConformance(files);

        Assert.Equal(3, issues.Length);
        Assert.Contains(issues, i => i.FilePath == "no-frontmatter.md" && i.Rule == "SPEC-9.1");
        Assert.Contains(issues, i => i.FilePath == "empty-type.md" && i.Rule == "SPEC-9.2");
        Assert.Contains(issues, i => i.FilePath == "unrecognised-type.md" && i.Rule == "SPEC-4.1");
    }
}
