namespace OkfParsingLibrary.OdcExternalLogic.Structures;

internal static class OkfConverters
{
    internal static OkfFileEntryOs ToOs(this OkfFileEntry e) => new()
    {
        Path = e.Path,
        Content = e.Content,
        ReservedKind = e.ReservedKind
    };

    internal static OkfFileEntry ToLib(this OkfFileEntryOs e) => new()
    {
        Path = e.Path,
        Content = e.Content,
        ReservedKind = e.ReservedKind
    };

    internal static FrontmatterSplitResultOs ToOs(this FrontmatterSplitResult r) => new()
    {
        HasFrontmatter = r.HasFrontmatter,
        FrontmatterRaw = r.FrontmatterRaw,
        Body = r.Body,
        ParseError = r.ParseError
    };

    internal static ParsedFrontmatterOs ToOs(this ParsedFrontmatter f) => new()
    {
        Type = f.Type,
        Title = f.Title,
        Description = f.Description,
        Resource = f.Resource,
        TagsCsv = f.TagsCsv,
        Status = f.Status,
        StaleAfter = f.StaleAfter,
        GeneratedBy = f.GeneratedBy,
        GeneratedAt = f.GeneratedAt,
        LegacyTimestamp = f.LegacyTimestamp,
        OkfVersion = f.OkfVersion,
        VerifiedJson = f.VerifiedJson,
        SourcesJson = f.SourcesJson,
        ExtraJson = f.ExtraJson,
        ParseError = f.ParseError
    };

    internal static LinkRefOs ToOs(this LinkRef l) => new()
    {
        LinkText = l.LinkText,
        RawPath = l.RawPath,
        IsCrossBundle = l.IsCrossBundle,
        TargetBundleName = l.TargetBundleName,
        TargetPath = l.TargetPath
    };

    internal static ConformanceIssueOs ToOs(this ConformanceIssue c) => new()
    {
        FilePath = c.FilePath,
        Severity = c.Severity,
        Rule = c.Rule,
        Message = c.Message
    };
}
