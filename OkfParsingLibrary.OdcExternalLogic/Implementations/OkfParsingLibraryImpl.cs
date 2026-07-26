using Microsoft.Extensions.Logging;
using OkfParsingLibrary.OdcExternalLogic.Interfaces;
using OkfParsingLibrary.OdcExternalLogic.Structures;

namespace OkfParsingLibrary.OdcExternalLogic.Implementations;

/// <summary>
/// Thin ODC-facing adapter. Every action delegates straight to the pure,
/// unit-tested OkfParsingLibrary.OkfParser static functions -- this class's
/// only job is converting between the SDK-friendly OSStructure mirrors and
/// the library's own plain structs (see Structures/OkfConverters.cs).
/// </summary>
public class OkfParsingLibraryImpl : IOkfParsingLibrary
{
    private readonly ILogger _logger;

    public OkfParsingLibraryImpl(ILogger logger)
    {
        _logger = logger;
    }

    public IEnumerable<OkfFileEntryOs> UnzipBundle(byte[] archive)
    {
        _logger.LogInformation("UnzipBundle: {ByteCount} bytes", archive.Length);
        return OkfParser.UnzipBundle(archive).Select(e => e.ToOs()).ToList();
    }

    public byte[] BuildBundleArchive(string filesJson)
    {
        _logger.LogInformation("BuildBundleArchive");
        return OkfParser.BuildBundleArchive(filesJson);
    }

    public FrontmatterSplitResultOs SplitFrontmatter(string fileContent)
    {
        return OkfParser.SplitFrontmatter(fileContent).ToOs();
    }

    public ParsedFrontmatterOs ParseFrontmatter(string frontmatterRaw)
    {
        var result = OkfParser.ParseFrontmatter(frontmatterRaw);
        if (result.ParseError.Length > 0)
            _logger.LogWarning("ParseFrontmatter: {ParseError}", result.ParseError);
        return result.ToOs();
    }

    public IEnumerable<LinkRefOs> ExtractLinks(string body)
    {
        return OkfParser.ExtractLinks(body).Select(l => l.ToOs()).ToList();
    }

    public string ExtractBodySection(string body, string heading)
    {
        return OkfParser.ExtractBodySection(body, heading);
    }

    public string ParseCitationsSection(string citationsBlock)
    {
        return OkfParser.ParseCitationsSection(citationsBlock);
    }

    public string SerializeConcept(string conceptJson)
    {
        return OkfParser.SerializeConcept(conceptJson);
    }

    public IEnumerable<ConformanceIssueOs> ValidateConformance(IEnumerable<OkfFileEntryOs> files)
    {
        var libFiles = files.Select(f => f.ToLib()).ToArray();
        var issues = OkfParser.ValidateConformance(libFiles);
        _logger.LogInformation("ValidateConformance: {IssueCount} issue(s) across {FileCount} file(s)", issues.Length, libFiles.Length);
        return issues.Select(i => i.ToOs()).ToList();
    }
}
