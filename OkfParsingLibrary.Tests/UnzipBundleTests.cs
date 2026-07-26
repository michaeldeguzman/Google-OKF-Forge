using System.IO.Compression;
using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

public class UnzipBundleTests
{
    [Fact]
    public void SingleWrappingTopLevelDirectory_IsStripped()
    {
        var bytes = TestHelpers.ZipDirectoryWithWrapper(TestHelpers.FixturePath("acme_retail"), "knowledge-catalog-main");
        var entries = OkfParser.UnzipBundle(bytes);

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.DoesNotContain("knowledge-catalog-main", e.Path));
        Assert.Contains(entries, e => e.Path == "tables/orders.md");
        Assert.Contains(entries, e => e.Path == "index.md");
    }

    [Fact]
    public void MixedMdAndNonMdFiles_OnlyMdSurvives()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "concept.md", "---\ntype: Metric\n---\nbody");
            AddEntry(zip, "viz.html", "<html></html>");
            AddEntry(zip, "attesters/sql_equality.py", "def attest(): pass");
            AddEntry(zip, "notes.txt", "plain text");
        }

        var entries = OkfParser.UnzipBundle(ms.ToArray());

        Assert.Single(entries);
        Assert.Equal("concept.md", entries[0].Path);
    }

    [Fact]
    public void IndexAndLogAtVariousDepths_ReservedKindSetCorrectly()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "index.md", "root index");
            AddEntry(zip, "log.md", "root log");
            AddEntry(zip, "tables/index.md", "tables index");
            AddEntry(zip, "tables/deep/nested/index.md", "deep index");
            AddEntry(zip, "tables/orders.md", "---\ntype: BigQuery Table\n---\nbody");
        }

        var entries = OkfParser.UnzipBundle(ms.ToArray()).ToDictionary(e => e.Path, e => e.ReservedKind);

        Assert.Equal("index", entries["index.md"]);
        Assert.Equal("log", entries["log.md"]);
        Assert.Equal("index", entries["tables/index.md"]);
        Assert.Equal("index", entries["tables/deep/nested/index.md"]);
        Assert.Equal("", entries["tables/orders.md"]);
    }

    [Fact]
    public void EmptyZip_ReturnsEmptyArrayWithoutThrowing()
    {
        using var ms = new MemoryStream();
        using (new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) { }

        var entries = OkfParser.UnzipBundle(ms.ToArray());
        Assert.Empty(entries);
    }

    [Fact]
    public void CorruptZip_ReturnsEmptyArrayWithoutThrowing()
    {
        var garbage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var entries = OkfParser.UnzipBundle(garbage);
        Assert.Empty(entries);
    }

    [Fact]
    public void EmptyByteArray_ReturnsEmptyArrayWithoutThrowing()
    {
        var entries = OkfParser.UnzipBundle(Array.Empty<byte>());
        Assert.Empty(entries);
    }

    [Fact]
    public void FlatBundleWithNoWrapper_PathsUnchanged()
    {
        var bytes = TestHelpers.ZipDirectory(TestHelpers.FixturePath("acme_retail"));
        var entries = OkfParser.UnzipBundle(bytes);
        Assert.Contains(entries, e => e.Path == "tables/orders.md");
        Assert.Contains(entries, e => e.Path == "index.md");
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
