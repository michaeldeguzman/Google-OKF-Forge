using OutSystems.ExternalLibraries.SDK;

namespace OkfParsingLibrary.OdcExternalLogic.Structures;

// Mirrors of OkfParsingLibrary.Models.cs, decorated for ODC. OSStructure requires
// properties (not public fields), so these are kept as thin, field-for-field
// property mirrors -- OkfConverters.cs does the mapping in both directions.
// Keeping OkfParsingLibrary itself free of any ODC/SDK reference is deliberate:
// it stays a plain, unit-testable library; this project is the only place that
// knows about OutSystems.

[OSStructure(Description = "One file extracted from a bundle archive.")]
public struct OkfFileEntryOs
{
    [OSStructureField(Description = "Normalised path, forward slashes, root wrapper folder stripped.")]
    public string Path { get; set; }

    [OSStructureField(Description = "UTF-8 decoded file content.")]
    public string Content { get; set; }

    [OSStructureField(Description = "\"\", \"index\", or \"log\".")]
    public string ReservedKind { get; set; }
}

[OSStructure(Description = "Result of splitting a concept file into its frontmatter block and body.")]
public struct FrontmatterSplitResultOs
{
    [OSStructureField(Description = "Whether a well-formed --- ... --- frontmatter block was found.")]
    public bool HasFrontmatter { get; set; }

    [OSStructureField(Description = "Raw YAML text between the delimiters, empty if none.")]
    public string FrontmatterRaw { get; set; }

    [OSStructureField(Description = "Everything after the closing delimiter (or the whole file if none).")]
    public string Body { get; set; }

    [OSStructureField(Description = "Set only when an opening --- was found with no closing ---.")]
    public string ParseError { get; set; }
}

[OSStructure(Description = "The fields recognised out of a concept's YAML frontmatter block.")]
public struct ParsedFrontmatterOs
{
    [OSStructureField] public string Type { get; set; }
    [OSStructureField] public string Title { get; set; }
    [OSStructureField] public string Description { get; set; }
    [OSStructureField] public string Resource { get; set; }
    [OSStructureField(Description = "Comma-separated tags.")] public string TagsCsv { get; set; }
    [OSStructureField] public string Status { get; set; }
    [OSStructureField(Description = "ISO date string, empty if absent.")] public string StaleAfter { get; set; }
    [OSStructureField] public string GeneratedBy { get; set; }
    [OSStructureField(Description = "ISO datetime string.")] public string GeneratedAt { get; set; }
    [OSStructureField(Description = "Legacy v0.1 ISO datetime string.")] public string LegacyTimestamp { get; set; }
    [OSStructureField] public string OkfVersion { get; set; }
    [OSStructureField(Description = "JSON array of {by, at}.")] public string VerifiedJson { get; set; }
    [OSStructureField(Description = "JSON array of source objects.")] public string SourcesJson { get; set; }
    [OSStructureField(Description = "JSON object of all unrecognised keys.")] public string ExtraJson { get; set; }
    [OSStructureField(Description = "Set when the frontmatter could not be fully parsed.")] public string ParseError { get; set; }
}

[OSStructure(Description = "A markdown link found in a concept body.")]
public struct LinkRefOs
{
    [OSStructureField] public string LinkText { get; set; }
    [OSStructureField] public string RawPath { get; set; }
    [OSStructureField] public bool IsCrossBundle { get; set; }
    [OSStructureField(Description = "Populated only for okf:// links.")] public string TargetBundleName { get; set; }
    [OSStructureField(Description = "Concept path, .md suffix stripped.")] public string TargetPath { get; set; }
}

[OSStructure(Description = "One conformance finding produced by ValidateConformance.")]
public struct ConformanceIssueOs
{
    [OSStructureField] public string FilePath { get; set; }
    [OSStructureField(Description = "\"error\" or \"warning\".")] public string Severity { get; set; }
    [OSStructureField(Description = "Spec section reference, e.g. \"SPEC-9.2\".")] public string Rule { get; set; }
    [OSStructureField] public string Message { get; set; }
}
