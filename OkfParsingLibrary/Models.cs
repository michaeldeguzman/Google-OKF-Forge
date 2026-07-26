namespace OkfParsingLibrary;

/// <summary>One file extracted from a bundle archive.</summary>
public struct OkfFileEntry
{
    public string Path;          // normalised, forward slashes, root folder stripped
    public string Content;       // UTF-8 decoded
    public string ReservedKind;  // "", "index", or "log"
}

/// <summary>Result of splitting a concept file into its frontmatter block and body.</summary>
public struct FrontmatterSplitResult
{
    public bool HasFrontmatter;
    public string FrontmatterRaw;
    public string Body;
    public string ParseError;
}

/// <summary>
/// The fields recognised out of a concept's YAML frontmatter block.
/// VerifiedJson, SourcesJson and ExtraJson are always valid JSON (an empty
/// array "[]" or empty object "{}" when the corresponding data is absent),
/// so callers never need a null/empty-string special case before parsing them.
/// </summary>
public struct ParsedFrontmatter
{
    public string Type;
    public string Title;
    public string Description;
    public string Resource;
    public string TagsCsv;
    public string Status;
    public string StaleAfter;        // ISO date string, empty if absent
    public string GeneratedBy;
    public string GeneratedAt;       // ISO datetime string
    public string LegacyTimestamp;   // ISO datetime string
    public string OkfVersion;
    public string VerifiedJson;      // JSON array of {by, at}
    public string SourcesJson;       // JSON array of source objects
    public string ExtraJson;         // JSON object of all unrecognised keys
    public string ParseError;
}

/// <summary>A markdown link found in a concept body.</summary>
public struct LinkRef
{
    public string LinkText;
    public string RawPath;
    public bool IsCrossBundle;
    public string TargetBundleName;  // populated only for okf:// links
    public string TargetPath;        // concept path, .md suffix stripped
}

/// <summary>One conformance finding produced by ValidateConformance.</summary>
public struct ConformanceIssue
{
    public string FilePath;
    public string Severity;   // "error" or "warning"
    public string Rule;       // spec section reference, e.g. "SPEC-9.2"
    public string Message;
}
