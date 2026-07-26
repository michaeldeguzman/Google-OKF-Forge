namespace OkfParsingLibrary;

public static partial class OkfParser
{
    // Illustrative example values from SPEC.md §4.1. The spec explicitly does not
    // maintain a central type registry ("Type values are not registered
    // centrally"), so "unrecognised" here means "outside this file's own
    // illustrative list" -- a soft, informational signal only, matching
    // SPEC-4.1's "warning" severity.
    private static readonly HashSet<string> RecognizedTypes = new()
    {
        "BigQuery Table", "BigQuery Dataset", "API Endpoint", "Metric",
        "Playbook", "Reference", "Attested Computation"
    };

    /// <summary>
    /// Runs the small set of conformance checks in the task spec's rule table
    /// (SPEC-9.1, SPEC-9.2, SPEC-4.1, SPEC-6). Deliberately does not check
    /// anything beyond that table -- per §11 of SPEC.md, missing optional
    /// fields, unknown types, unknown extra keys, and broken links are all
    /// explicitly non-failures.
    /// </summary>
    public static ConformanceIssue[] ValidateConformance(OkfFileEntry[] files)
    {
        var issues = new List<ConformanceIssue>();

        foreach (var file in files)
        {
            if (file.ReservedKind == "")
            {
                var split = SplitFrontmatter(file.Content);
                if (!split.HasFrontmatter)
                {
                    issues.Add(new ConformanceIssue
                    {
                        FilePath = file.Path,
                        Severity = "error",
                        Rule = "SPEC-9.1",
                        Message = split.ParseError.Length > 0
                            ? $"frontmatter block is not parseable: {split.ParseError}"
                            : "file has no frontmatter block"
                    });
                    continue;
                }

                var fm = ParseFrontmatter(split.FrontmatterRaw);
                if (fm.Type.Trim().Length == 0)
                {
                    issues.Add(new ConformanceIssue
                    {
                        FilePath = file.Path,
                        Severity = "error",
                        Rule = "SPEC-9.2",
                        Message = "frontmatter block has no non-empty 'type' field"
                    });
                }
                else if (!RecognizedTypes.Contains(fm.Type.Trim()))
                {
                    issues.Add(new ConformanceIssue
                    {
                        FilePath = file.Path,
                        Severity = "warning",
                        Rule = "SPEC-4.1",
                        Message = $"type '{fm.Type}' is not one of the spec's illustrative example types"
                    });
                }
            }
            else if (file.ReservedKind == "index")
            {
                var split = SplitFrontmatter(file.Content);
                if (!split.HasFrontmatter) continue;

                bool isBundleRoot = file.Path.Equals("index.md", StringComparison.OrdinalIgnoreCase);
                if (!isBundleRoot)
                {
                    issues.Add(new ConformanceIssue
                    {
                        FilePath = file.Path,
                        Severity = "warning",
                        Rule = "SPEC-6",
                        Message = "only the bundle-root index.md is permitted a frontmatter block"
                    });
                    continue;
                }

                var fm = ParseFrontmatter(split.FrontmatterRaw);
                bool carriesOnlyOkfVersion =
                    fm.Type.Length == 0 && fm.Title.Length == 0 && fm.Description.Length == 0 &&
                    fm.Resource.Length == 0 && fm.TagsCsv.Length == 0 && fm.Status.Length == 0 &&
                    fm.StaleAfter.Length == 0 && fm.GeneratedBy.Length == 0 && fm.GeneratedAt.Length == 0 &&
                    fm.LegacyTimestamp.Length == 0 && fm.VerifiedJson == "[]" && fm.SourcesJson == "[]" &&
                    fm.ExtraJson == "{}";

                if (!carriesOnlyOkfVersion)
                {
                    issues.Add(new ConformanceIssue
                    {
                        FilePath = file.Path,
                        Severity = "warning",
                        Rule = "SPEC-6",
                        Message = "bundle-root index.md frontmatter may only carry 'okf_version'"
                    });
                }
            }
            // "log" reserved files carry no conformance checks in the task's rule table.
        }

        return issues.ToArray();
    }
}
