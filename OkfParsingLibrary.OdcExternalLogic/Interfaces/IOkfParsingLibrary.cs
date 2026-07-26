using OkfParsingLibrary.OdcExternalLogic.Structures;
using OutSystems.ExternalLibraries.SDK;

namespace OkfParsingLibrary.OdcExternalLogic.Interfaces;

[OSInterface(
    Name = "OkfParsingLibrary",
    Description = "Parses and serialises Open Knowledge Format (OKF) v0.2 bundles. Stateless: every action is pure and never throws on malformed input, reporting problems through the returned structure's ParseError/Message fields instead.",
    IconResourceName = "OkfParsingLibrary.OdcExternalLogic.Resources.OKFODC.jpg"
)]
public interface IOkfParsingLibrary
{
    [OSAction(
        Description = "Extracts every .md entry from a zip archive. Never throws: a corrupt or empty zip yields an empty list. Strips a single shared top-level wrapper folder (e.g. a GitHub archive download) if present.",
        ReturnName = "Files"
    )]
    IEnumerable<OkfFileEntryOs> UnzipBundle(
        [OSParameter(Description = "The bundle archive as a zip file's raw bytes.")] byte[] archive
    );

    [OSAction(
        Description = "Builds a zip archive from a list of {path, content} objects, given as a JSON array.",
        ReturnName = "Archive"
    )]
    byte[] BuildBundleArchive(
        [OSParameter(Description = "JSON array of {\"path\": \"...\", \"content\": \"...\"} objects.")] string filesJson
    );

    [OSAction(
        Description = "Splits a concept file into its frontmatter block and body. Missing or unterminated frontmatter is reported via the result, never thrown.",
        ReturnName = "Result"
    )]
    FrontmatterSplitResultOs SplitFrontmatter(
        [OSParameter(Description = "The full raw content of a concept markdown file.")] string fileContent
    );

    [OSAction(
        Description = "Parses an OKF v0.2 frontmatter block (the constrained YAML subset OKF actually uses). Malformed constructs are reported via ParseError, with every field parsed before the failure point still populated.",
        ReturnName = "Frontmatter"
    )]
    ParsedFrontmatterOs ParseFrontmatter(
        [OSParameter(Description = "Raw YAML text between the --- delimiters, e.g. FrontmatterSplitResult.FrontmatterRaw.")] string frontmatterRaw
    );

    [OSAction(
        Description = "Extracts markdown inline links from a concept body. Excludes image syntax and external http(s)/mailto links, and classifies each remaining link as bundle-absolute, relative, or cross-bundle (okf://).",
        ReturnName = "Links"
    )]
    IEnumerable<LinkRefOs> ExtractLinks(
        [OSParameter(Description = "The concept body, e.g. FrontmatterSplitResult.Body.")] string body
    );

    [OSAction(
        Description = "Returns the content under a `# {heading}` line (any level), up to but excluding the next heading at the same or a shallower level. Returns empty text if the heading is not found.",
        ReturnName = "SectionContent"
    )]
    string ExtractBodySection(
        [OSParameter(Description = "The concept body to search.")] string body,
        [OSParameter(Description = "Heading text without the # characters, e.g. \"Computation\".")] string heading
    );

    [OSAction(
        Description = "Parses a legacy v0.1 `# Citations` block (\"[1] [Title](url)\" lines) into a JSON array shaped like ParsedFrontmatter.SourcesJson. Non-matching lines are skipped, not treated as errors.",
        ReturnName = "SourcesJson"
    )]
    string ParseCitationsSection(
        [OSParameter(Description = "Raw text under a # Citations heading.")] string citationsBlock
    );

    [OSAction(
        Description = "Serialises a concept's frontmatter fields plus body back into a complete markdown file. Fields left empty in the input are omitted from the output rather than emitted blank.",
        ReturnName = "MarkdownFile"
    )]
    string SerializeConcept(
        [OSParameter(Description = "JSON object mirroring ParsedFrontmatter field names (type, title, description, resource, tagsCsv, status, staleAfter, generatedBy, generatedAt, verifiedJson, sourcesJson, extraJson) plus a \"body\" string.")] string conceptJson
    );

    [OSAction(
        Description = "Runs the SPEC-9.1 / SPEC-9.2 / SPEC-4.1 / SPEC-6 conformance checks against a set of bundle files. Missing optional fields, unknown types, unknown extra keys, and broken links are never flagged -- only what those four rules cover.",
        ReturnName = "Issues"
    )]
    IEnumerable<ConformanceIssueOs> ValidateConformance(
        [OSParameter(Description = "The files to check, typically the output of UnzipBundle.")] IEnumerable<OkfFileEntryOs> files
    );
}
