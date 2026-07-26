using System.Text.RegularExpressions;

namespace OkfParsingLibrary;

public static partial class OkfParser
{
    // Negative lookbehind excludes image syntax ![alt](path).
    private static readonly Regex MarkdownLink = new(@"(?<!!)\[([^\]]*)\]\(([^)]*)\)", RegexOptions.Compiled);

    private static readonly string[] ExternalSchemes = { "http://", "https://", "mailto:" };

    /// <summary>Extracts markdown inline links from a concept body, classifying each by target form.</summary>
    public static LinkRef[] ExtractLinks(string body)
    {
        var result = new List<LinkRef>();
        foreach (Match m in MarkdownLink.Matches(body))
        {
            string text = m.Groups[1].Value;
            string path = m.Groups[2].Value;

            if (ExternalSchemes.Any(scheme => path.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
                continue;

            var link = new LinkRef { LinkText = text, RawPath = path };

            if (path.StartsWith("okf://", StringComparison.OrdinalIgnoreCase))
            {
                string afterScheme = path.Substring("okf://".Length);
                int slash = afterScheme.IndexOf('/');
                link.IsCrossBundle = true;
                if (slash >= 0)
                {
                    link.TargetBundleName = afterScheme.Substring(0, slash);
                    link.TargetPath = StripMdSuffix(afterScheme.Substring(slash + 1));
                }
                else
                {
                    link.TargetBundleName = afterScheme;
                    link.TargetPath = "";
                }
            }
            else if (path.StartsWith('/'))
            {
                link.IsCrossBundle = false;
                link.TargetBundleName = "";
                link.TargetPath = StripMdSuffix(path.Substring(1));
            }
            else
            {
                link.IsCrossBundle = false;
                link.TargetBundleName = "";
                string relative = path.StartsWith("./") ? path.Substring(2) : path;
                link.TargetPath = StripMdSuffix(relative);
            }

            result.Add(link);
        }
        return result.ToArray();
    }

    private static string StripMdSuffix(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? path.Substring(0, path.Length - 3) : path;

    /// <summary>
    /// Returns the content of the section under `# {heading}` (any level), up to
    /// but excluding the next heading at the same or a shallower level. Deeper
    /// nested headings inside the section are included, not treated as terminators.
    /// </summary>
    public static string ExtractBodySection(string body, string heading)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var headingLine = new Regex(@"^(#+)\s+" + Regex.Escape(heading) + @"\s*$");
        var anyHeadingLine = new Regex(@"^(#+)\s+");

        int startIndex = -1, level = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = headingLine.Match(lines[i]);
            if (m.Success)
            {
                startIndex = i + 1;
                level = m.Groups[1].Value.Length;
                break;
            }
        }
        if (startIndex == -1) return "";

        int endIndex = lines.Length;
        for (int i = startIndex; i < lines.Length; i++)
        {
            var m = anyHeadingLine.Match(lines[i]);
            if (m.Success && m.Groups[1].Value.Length <= level)
            {
                endIndex = i;
                break;
            }
        }

        return string.Join("\n", lines[startIndex..endIndex]);
    }

    private static readonly Regex CitationLine = new(@"^\[\d+\]\s+\[([^\]]*)\]\(([^)]*)\)\s*$");

    /// <summary>
    /// Parses a legacy v0.1 `# Citations` body block (`[1] [Title](url)` lines)
    /// into a JSON array shaped like SourcesJson, minus the fields that don't
    /// exist in v0.1 citations (id, author, usage_count, last_modified).
    /// Lines that don't match are skipped, not treated as errors.
    /// </summary>
    public static string ParseCitationsSection(string citationsBlock)
    {
        var array = new System.Text.Json.Nodes.JsonArray();
        foreach (var rawLine in citationsBlock.Replace("\r\n", "\n").Split('\n'))
        {
            var m = CitationLine.Match(rawLine.Trim());
            if (!m.Success) continue;
            var obj = new System.Text.Json.Nodes.JsonObject
            {
                ["title"] = m.Groups[1].Value,
                ["resource"] = m.Groups[2].Value
            };
            array.Add(obj);
        }
        return array.ToJsonString();
    }
}
