using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

public class LinkExtractionTests
{
    [Fact]
    public void BundleAbsoluteLink()
    {
        var links = OkfParser.ExtractLinks("See the [customers table](/tables/customers.md) for the join key.");
        var link = Assert.Single(links);
        Assert.Equal("customers table", link.LinkText);
        Assert.Equal("/tables/customers.md", link.RawPath);
        Assert.False(link.IsCrossBundle);
        Assert.Equal("", link.TargetBundleName);
        Assert.Equal("tables/customers", link.TargetPath);
    }

    [Fact]
    public void RelativeLinkWithDotSlashPrefix()
    {
        var links = OkfParser.ExtractLinks("See [neighboring](./other.md).");
        var link = Assert.Single(links);
        Assert.False(link.IsCrossBundle);
        Assert.Equal("other", link.TargetPath);
    }

    [Fact]
    public void RelativeLinkWithoutDotSlashPrefix()
    {
        var links = OkfParser.ExtractLinks("See [gross margin](gross-margin.md).");
        var link = Assert.Single(links);
        Assert.False(link.IsCrossBundle);
        Assert.Equal("gross-margin", link.TargetPath);
    }

    [Fact]
    public void ImageSyntax_IsExcluded()
    {
        var links = OkfParser.ExtractLinks("![alt text](/images/diagram.png)");
        Assert.Empty(links);
    }

    [Fact]
    public void ExternalHttpsLink_IsExcluded()
    {
        var links = OkfParser.ExtractLinks("[dashboard](https://example.com/dash)");
        Assert.Empty(links);
    }

    [Fact]
    public void MailtoLink_IsExcluded()
    {
        var links = OkfParser.ExtractLinks("[contact](mailto:someone@example.com)");
        Assert.Empty(links);
    }

    [Fact]
    public void CrossBundleOkfLink()
    {
        var links = OkfParser.ExtractLinks("[other concept](okf://otherbundle/path/to/concept.md)");
        var link = Assert.Single(links);
        Assert.True(link.IsCrossBundle);
        Assert.Equal("otherbundle", link.TargetBundleName);
        Assert.Equal("path/to/concept", link.TargetPath);
    }

    [Fact]
    public void CrossBundleOkfLink_WithSpaceInBundleName()
    {
        var links = OkfParser.ExtractLinks("[margin policy](okf://Acme Retail/policies/margin-standard.md)");
        var link = Assert.Single(links);
        Assert.True(link.IsCrossBundle);
        Assert.Equal("Acme Retail", link.TargetBundleName);
        Assert.Equal("policies/margin-standard", link.TargetPath);
    }

    [Fact]
    public void LinkWithNoMdSuffix_DoesNotThrow()
    {
        var links = OkfParser.ExtractLinks("[weird link](/tables/orders)");
        var link = Assert.Single(links);
        Assert.Equal("tables/orders", link.TargetPath);
    }

    [Fact]
    public void MultipleLinksInOneBody_AllExtractedInOrder()
    {
        var body = "[a](/a.md) then ![img](/x.png) then [b](https://ext.com) then [c](./c.md)";
        var links = OkfParser.ExtractLinks(body);
        Assert.Equal(2, links.Length);
        Assert.Equal("a", links[0].TargetPath);
        Assert.Equal("c", links[1].TargetPath);
    }
}
