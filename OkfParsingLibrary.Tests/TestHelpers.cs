using System.IO.Compression;
using OkfParsingLibrary;

namespace OkfParsingLibrary.Tests;

internal static class TestHelpers
{
    /// <summary>Zips a directory on disk (recursively) into bytes, entries rooted at the directory's own contents.</summary>
    internal static byte[] ZipDirectory(string directoryPath)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(directoryPath, file).Replace(Path.DirectorySeparatorChar, '/');
                zip.CreateEntryFromFile(file, relative);
            }
        }
        return ms.ToArray();
    }

    /// <summary>Wraps a directory's contents inside one extra top-level folder before zipping, simulating a GitHub archive download.</summary>
    internal static byte[] ZipDirectoryWithWrapper(string directoryPath, string wrapperName)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(directoryPath, file).Replace(Path.DirectorySeparatorChar, '/');
                zip.CreateEntryFromFile(file, $"{wrapperName}/{relative}");
            }
        }
        return ms.ToArray();
    }

    internal static string FixturePath(params string[] segments) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", Path.Combine(segments));

    /// <summary>Asserts two ParsedFrontmatter values are field-by-field equal, with a readable diff on mismatch.</summary>
    internal static void AssertParsedFrontmatterEqual(ParsedFrontmatter expected, ParsedFrontmatter actual, string context)
    {
        Assert.True(expected.Type == actual.Type, $"{context}: Type differs. expected='{expected.Type}' actual='{actual.Type}'");
        Assert.True(expected.Title == actual.Title, $"{context}: Title differs. expected='{expected.Title}' actual='{actual.Title}'");
        Assert.True(expected.Description == actual.Description, $"{context}: Description differs. expected='{expected.Description}' actual='{actual.Description}'");
        Assert.True(expected.Resource == actual.Resource, $"{context}: Resource differs. expected='{expected.Resource}' actual='{actual.Resource}'");
        Assert.True(expected.TagsCsv == actual.TagsCsv, $"{context}: TagsCsv differs. expected='{expected.TagsCsv}' actual='{actual.TagsCsv}'");
        Assert.True(expected.Status == actual.Status, $"{context}: Status differs. expected='{expected.Status}' actual='{actual.Status}'");
        Assert.True(expected.StaleAfter == actual.StaleAfter, $"{context}: StaleAfter differs. expected='{expected.StaleAfter}' actual='{actual.StaleAfter}'");
        Assert.True(expected.GeneratedBy == actual.GeneratedBy, $"{context}: GeneratedBy differs. expected='{expected.GeneratedBy}' actual='{actual.GeneratedBy}'");
        Assert.True(expected.GeneratedAt == actual.GeneratedAt, $"{context}: GeneratedAt differs. expected='{expected.GeneratedAt}' actual='{actual.GeneratedAt}'");
        Assert.True(expected.LegacyTimestamp == actual.LegacyTimestamp, $"{context}: LegacyTimestamp differs. expected='{expected.LegacyTimestamp}' actual='{actual.LegacyTimestamp}'");
        JsonEquivalent(expected.VerifiedJson, actual.VerifiedJson, context, "VerifiedJson");
        JsonEquivalent(expected.SourcesJson, actual.SourcesJson, context, "SourcesJson");
        JsonEquivalent(expected.ExtraJson, actual.ExtraJson, context, "ExtraJson");
        Assert.True(expected.ParseError == actual.ParseError, $"{context}: ParseError differs. expected='{expected.ParseError}' actual='{actual.ParseError}'");
    }

    private static void JsonEquivalent(string expectedJson, string actualJson, string context, string field)
    {
        var e = System.Text.Json.Nodes.JsonNode.Parse(expectedJson);
        var a = System.Text.Json.Nodes.JsonNode.Parse(actualJson);
        Assert.True(
            JsonNodeEquals(e, a),
            $"{context}: {field} differs.\nexpected={expectedJson}\nactual={actualJson}");
    }

    private static bool JsonNodeEquals(System.Text.Json.Nodes.JsonNode? a, System.Text.Json.Nodes.JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return a.ToJsonString() == b.ToJsonString();
    }
}
